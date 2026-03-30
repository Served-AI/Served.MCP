using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Served.MCP.Tools;

/// <summary>
/// A named group of MCP tools that can be activated/deactivated at runtime.
/// Reduces context window waste by only exposing relevant tools to the model.
/// </summary>
public class McpToolGroup
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Active { get; set; }
    public bool AlwaysActive { get; set; }
    public string? Notes { get; set; }
    public List<string> Tools { get; set; } = new();
}

/// <summary>
/// Registry that manages tool groups and resolves which tools are active
/// for a given MCP session. Reads/writes configuration from .served/mcp-groups.unified.
///
/// When no config exists, falls back to exposing ALL tools (backwards compatible).
/// When config exists, only tools in active groups are visible to the model.
/// </summary>
public class ToolGroupRegistry
{
    private readonly Dictionary<string, McpToolGroup> _groups = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _configPath;
    private bool _initialized;

    // Meta-tool names that are never filtered
    public static readonly HashSet<string> MetaToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "activate_tool_group",
        "deactivate_tool_group",
        "list_tool_groups"
    };

    public ToolGroupRegistry(string? configPath = null)
    {
        _configPath = configPath ?? FindConfigPath();
        LoadOrCreateDefaults();
    }

    /// <summary>
    /// Get all tool names that should be visible to the model.
    /// Returns null if no groups are configured (= show everything).
    /// </summary>
    public HashSet<string>? GetActiveToolNames()
    {
        if (!_initialized || _groups.Count == 0)
            return null; // No filtering — backwards compatible

        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in _groups.Values.Where(g => g.Active))
        {
            foreach (var tool in group.Tools)
                active.Add(tool);
        }

        // Meta-tools are always active
        foreach (var meta in MetaToolNames)
            active.Add(meta);

        return active;
    }

    /// <summary>
    /// Activate a tool group. Returns the list of newly available tool names.
    /// </summary>
    public (bool Success, List<string> Tools, string? Error) ActivateGroup(string name)
    {
        if (!_groups.TryGetValue(name, out var group))
            return (false, new(), $"Unknown tool group: '{name}'. Use list_tool_groups to see available groups.");

        if (group.Active)
            return (true, group.Tools, null); // Already active, still return tools

        group.Active = true;
        SaveConfig();
        Console.Error.WriteLine($"[MCP] Tool group '{name}' activated ({group.Tools.Count} tools)");
        return (true, group.Tools, null);
    }

    /// <summary>
    /// Deactivate a tool group. Returns the removed tool names.
    /// </summary>
    public (bool Success, List<string> Tools, string? Error) DeactivateGroup(string name)
    {
        if (!_groups.TryGetValue(name, out var group))
            return (false, new(), $"Unknown tool group: '{name}'. Use list_tool_groups to see available groups.");

        if (group.AlwaysActive)
            return (false, new(), $"Cannot deactivate '{name}' — it is required for basic functionality.");

        if (!group.Active)
            return (true, new(), null); // Already inactive

        group.Active = false;
        SaveConfig();
        Console.Error.WriteLine($"[MCP] Tool group '{name}' deactivated");
        return (true, group.Tools, null);
    }

    /// <summary>
    /// Get description of all groups and their activation status.
    /// </summary>
    public List<McpToolGroup> ListGroups() => _groups.Values.OrderByDescending(g => g.Active).ThenBy(g => g.Name).ToList();

    private static string FindConfigPath()
    {
        // Walk up from CWD looking for .served/
        var dir = Directory.GetCurrentDirectory();
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, ".served", "mcp-groups.unified");
            if (File.Exists(candidate))
                return candidate;
            var servedDir = Path.Combine(dir, ".served");
            if (Directory.Exists(servedDir))
                return candidate; // .served/ exists but config doesn't yet
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        // Fallback: current directory
        return Path.Combine(Directory.GetCurrentDirectory(), ".served", "mcp-groups.unified");
    }

    private void LoadOrCreateDefaults()
    {
        if (File.Exists(_configPath))
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                var config = JObject.Parse(json);
                var groups = config["groups"] as JObject;
                if (groups != null)
                {
                    foreach (var prop in groups.Properties())
                    {
                        var g = prop.Value!.ToObject<McpToolGroup>()!;
                        g.Name = prop.Name;
                        if (prop.Name.Equals("core", StringComparison.OrdinalIgnoreCase))
                            g.AlwaysActive = true;
                        _groups[prop.Name] = g;
                    }
                    _initialized = true;
                    // Migrate: inject missing default groups into existing config
                    MigrateDefaults();
                    Console.Error.WriteLine($"[MCP] Loaded {_groups.Count} tool groups from {_configPath}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MCP] Failed to load tool groups: {ex.Message}. Using defaults.");
            }
        }

        // Create defaults
        CreateDefaultGroups();
        _initialized = true;
        SaveConfig();
        Console.Error.WriteLine($"[MCP] Created default tool groups ({_groups.Count} groups) at {_configPath}");
    }

    private void CreateDefaultGroups()
    {
        Add(new McpToolGroup
        {
            Name = "core",
            Description = "Essential context tools every agent needs",
            Active = true,
            AlwaysActive = true,
            Notes = "Always active. Cannot be deactivated.",
            Tools = new()
            {
                "GetUserContext", "GetTenantContext", "GetProjectContext",
                "BootstrapGetUser", "BootstrapGetTenant", "BootstrapGetWorkspace",
                "SearchProjects", "SearchTasks", "SearchCustomers",
                "create_plan", "get_current_plan"
            }
        });

        Add(new McpToolGroup
        {
            Name = "project-management",
            Description = "Project CRUD, bulk ops, grouping, sub-projects",
            Notes = "Activate when working on project management features",
            Tools = new()
            {
                "GetProjects", "GetProjectDetails", "CreateProject", "UpdateProject", "DeleteProject",
                "ProjectGetRange", "ProjectGetKeys", "ProjectGetGrouping", "ProjectGetSubProjects",
                "ProjectCreateBulk", "ProjectUpdateBulk", "ProjectDeleteBulk",
                "GetProjectGrouping"
            }
        });

        Add(new McpToolGroup
        {
            Name = "task-management",
            Description = "Task CRUD, status updates, bulk ops, grouping",
            Notes = "Activate when working on tasks or kanban boards",
            Tools = new()
            {
                "GetTasks", "GetTaskDetails", "CreateTask", "UpdateTask", "DeleteTask",
                "TaskGetRange", "TaskGetKeys", "TaskGetGrouping", "TaskGetByProject", "TaskGetByAssignee",
                "TaskGetSubTasks", "TaskUpdateStatus", "TaskUpdateStatusBulk",
                "TaskCreateBulk", "TaskUpdateBulk", "TaskDeleteBulk",
                "GetTaskGrouping"
            }
        });

        Add(new McpToolGroup
        {
            Name = "customer-management",
            Description = "Customer CRM — CRUD, search, bulk operations",
            Tools = new()
            {
                "GetCustomers", "GetCustomerDetails", "CreateCustomer", "UpdateCustomer", "DeleteCustomer",
                "SearchCustomers", "CustomerGetRange", "CustomerCreateBulk", "CustomerUpdateBulk", "CustomerDeleteBulk"
            }
        });

        Add(new McpToolGroup
        {
            Name = "time-tracking",
            Description = "Time registration CRUD, by date/project/task",
            Tools = new()
            {
                "GetTimeRegistrations", "GetTimeRegistrationDetails",
                "CreateTimeRegistration", "UpdateTimeRegistration", "DeleteTimeRegistration",
                "TimeRegistrationGetByDateRange", "TimeRegistrationGetByProject", "TimeRegistrationGetByTask"
            }
        });

        Add(new McpToolGroup
        {
            Name = "agreements",
            Description = "Agreement CRUD, by customer/date range",
            Tools = new()
            {
                "GetAgreements", "GetAgreementDetails", "CreateAgreement", "UpdateAgreement", "DeleteAgreement",
                "AgreementGetByCustomer", "AgreementGetByDateRange"
            }
        });

        Add(new McpToolGroup
        {
            Name = "devops",
            Description = "Repositories, pipelines, pull requests",
            Notes = "Activate for CI/CD, deploy, and pipeline work",
            Tools = new()
            {
                "DevOpGetRepositories", "DevOpGetRepository", "DevOpCreateRepository", "DevOpUpdateRepository", "DevOpDeleteRepository",
                "DevOpGetPipelineRuns", "DevOpGetLatestPipelineRun", "DevOpGetPipelineRunsByRepository",
                "DevOpGetPullRequests", "DevOpGetPullRequestsByRepository", "DevOpGetPullRequestsBySession", "DevOpGetPullRequestsByTask"
            }
        });

        Add(new McpToolGroup
        {
            Name = "infrastructure",
            Description = "Cluster health, K8s pods/deployments, Docker, Proxmox, resource metrics",
            Notes = "Activate for infrastructure monitoring and management",
            Tools = new()
            {
                "GetClusterHealth", "GetNodeStatus", "GetResourceMetrics",
                "GetKubernetesPods", "GetKubernetesDeployments", "RestartDeployment",
                "GetDockerContainers", "GetProxmoxVMs",
                "infra_status", "infra_plan", "infra_apply", "infra_recommend", "infra_cost"
            }
        });

        Add(new McpToolGroup
        {
            Name = "dashboards",
            Description = "Dashboard CRUD, widgets, layout management",
            Tools = new()
            {
                "GetDashboards", "GetDashboardDetails", "CreateDashboard", "UpdateDashboard", "DeleteDashboard",
                "DashboardGetDefault", "DashboardGetWidgets", "DashboardGetWidget",
                "DashboardCreateWidgetsBulk", "DashboardUpdateWidget", "DashboardUpdateWidgetsBulk",
                "DashboardDeleteWidget", "DashboardUpdateWidgetLayout"
            }
        });

        Add(new McpToolGroup
        {
            Name = "datasource",
            Description = "Query engine — entities, schemas, aggregations, raw queries",
            Tools = new()
            {
                "DatasourceExecuteQuery", "DatasourceGetCategories", "DatasourceGetEntities",
                "DatasourceGetEntitiesByCategory", "DatasourceGetEntitySchema",
                "DatasourceGetField", "DatasourceGetAggregationsForType", "DatasourceGetOperatorsForType"
            }
        });

        Add(new McpToolGroup
        {
            Name = "boards",
            Description = "Board/sheet/row/cell CRUD for structured data views",
            Tools = new()
            {
                "BoardGetBoards", "BoardGetBoard", "BoardCreateBoard", "BoardUpdateBoard", "BoardDeleteBoard",
                "BoardGetBoardKeys", "BoardGetSheets", "BoardGetSheet", "BoardGetSheetByClaimId",
                "BoardCreateSheet", "BoardUpdateSheet", "BoardDeleteSheet",
                "BoardGetRows", "BoardGetColumns", "BoardGetViews", "BoardDeleteView",
                "BoardGetCell", "BoardGetCellsForColumn", "BoardGetCellsForRow",
                "BoardDeleteColumn", "BoardDeleteRow"
            }
        });

        Add(new McpToolGroup
        {
            Name = "finance",
            Description = "Invoices, revenue metrics, SaaS plans, subscriptions, funnel analytics",
            Tools = new()
            {
                "FinanceGetInvoices", "GetRevenueMetrics",
                "GetSaasPlans", "GetSubscription",
                "GetFunnelDashboard", "GetFunnelSessions", "AnalyzeConversion"
            }
        });

        Add(new McpToolGroup
        {
            Name = "sales",
            Description = "Sales pipeline — deals, forecasts, analytics",
            Tools = new()
            {
                "SaleGetPipelines", "SaleGetPipeline", "SaleCreatePipeline", "SaleUpdatePipeline", "SaleDeletePipeline",
                "SaleGetDeal", "SaleCreateDeal", "SaleUpdateDeal", "SaleDeleteDeal",
                "SaleSearchDeals", "SaleGetForecast", "SaleGetPipelineAnalytics"
            }
        });

        Add(new McpToolGroup
        {
            Name = "atlas-control",
            Description = "Screen control — screenshots, mouse, keyboard, window management",
            Notes = "Activate for browser testing, UI verification, or desktop automation",
            Tools = new()
            {
                "AtlasControlHealth", "AtlasListWindows", "AtlasFocusWindow", "AtlasScreenshot",
                "AtlasMouseMove", "AtlasMouseClick", "AtlasType", "AtlasKeyPress", "AtlasKeyboardShortcut",
                "AtlasShowPointer", "AtlasHidePointer", "AtlasMovePointer"
            }
        });

        Add(new McpToolGroup
        {
            Name = "supervisor",
            Description = "Multi-agent orchestration — spawn, control, coordinate agents",
            Tools = new()
            {
                "StartSupervisor", "StopSupervisor", "GetSupervisorStatus",
                "SpawnSpecializedAgent", "AssignSupervisorTask", "ControlAgent",
                "ControlPlan", "GetExecutionPlan", "GetAgentCoordinationStatus"
            }
        });

        Add(new McpToolGroup
        {
            Name = "serva-marketing",
            Description = "Social media monitoring, AI marketing responses",
            Tools = new()
            {
                "GetServaQueue", "GetServaSettings", "ApproveServaItem", "RejectServaItem",
                "GetSocialMentions", "GetSocialAnalytics", "RespondToMention"
            }
        });

        Add(new McpToolGroup
        {
            Name = "media",
            Description = "Download, analyze audio/video, lyrics pipeline",
            Notes = "Activate for music, video, or media work",
            Tools = new()
            {
                "media_download", "media_get_metadata", "media_analyze_audio", "media_analyze_video", "media_analyze_full",
                "lyrics_fetch", "lyrics_analyze", "lyrics_generate",
                "suno_generate", "suno_generate_lyrics", "suno_status", "suno_download", "suno_extend", "suno_credits"
            }
        });

        Add(new McpToolGroup
        {
            Name = "vault",
            Description = "Obsidian vault — read, search, daily notes, tags, backlinks",
            Tools = new()
            {
                "VaultRead", "VaultSearch", "VaultDailyRead", "VaultDailyAppend",
                "VaultStatus", "VaultTasks", "VaultTags", "VaultBacklinks", "VaultUnresolved", "VaultCreate"
            }
        });

        Add(new McpToolGroup
        {
            Name = "tenant-management",
            Description = "Tenant CRUD, employee management, API keys, downloads",
            Tools = new()
            {
                "GetTenants", "TenantGetBySlug", "CreateTenant", "UpdateTenant",
                "GetEmployees", "EmployeeGetDetailed",
                "GetApiKeys", "ApiKeyGetScopes", "CreateApiKey",
                "DownloadGetManifest", "DownloadGetAudit", "DownloadCreateApiKey"
            }
        });

        Add(new McpToolGroup
        {
            Name = "plan",
            Description = "Plan notebook — create and track execution plans with subtasks",
            Notes = "Auto-activated when an agent creates a plan via create_plan",
            Tools = new()
            {
                "create_plan", "get_current_plan", "add_subtask",
                "update_subtask_status", "finish_subtask", "revise_plan"
            }
        });
    }

    /// <summary>
    /// Inject any new default groups that don't exist in the loaded config.
    /// Also ensures core group has required tools (e.g. create_plan).
    /// </summary>
    private void MigrateDefaults()
    {
        var migrated = false;
        var defaults = new Dictionary<string, McpToolGroup>();
        var temp = new ToolGroupRegistry.__DefaultGroupBuilder(defaults);
        temp.Build();

        foreach (var (name, defGroup) in defaults)
        {
            if (!_groups.ContainsKey(name))
            {
                _groups[name] = defGroup;
                migrated = true;
                Console.Error.WriteLine($"[MCP] Migrated new tool group: {name}");
            }
        }

        // Ensure core has plan entry points
        if (_groups.TryGetValue("core", out var core))
        {
            foreach (var tool in new[] { "create_plan", "get_current_plan" })
            {
                if (!core.Tools.Contains(tool))
                {
                    core.Tools.Add(tool);
                    migrated = true;
                }
            }
        }

        if (migrated) SaveConfig();
    }

    // Helper for migration — builds default groups into an external dictionary
    internal class __DefaultGroupBuilder
    {
        private readonly Dictionary<string, McpToolGroup> _target;
        public __DefaultGroupBuilder(Dictionary<string, McpToolGroup> target) => _target = target;
        public void Build()
        {
            // Only add groups that might be missing from old configs
            _target["plan"] = new McpToolGroup
            {
                Name = "plan",
                Description = "Plan notebook — create and track execution plans with subtasks",
                Notes = "Auto-activated when an agent creates a plan via create_plan",
                Tools = new()
                {
                    "create_plan", "get_current_plan", "add_subtask",
                    "update_subtask_status", "finish_subtask", "revise_plan"
                }
            };
        }
    }

    private void Add(McpToolGroup group)
    {
        _groups[group.Name] = group;
    }

    private void SaveConfig()
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var config = new JObject
            {
                ["version"] = 1,
                ["description"] = "MCP tool group configuration. Groups control which tools are visible to the AI model.",
                ["groups"] = new JObject()
            };

            foreach (var group in _groups.Values.OrderBy(g => g.Name))
            {
                var g = new JObject
                {
                    ["description"] = group.Description,
                    ["active"] = group.Active,
                    ["tools"] = new JArray(group.Tools)
                };
                if (group.AlwaysActive)
                    g["always_active"] = true;
                if (!string.IsNullOrEmpty(group.Notes))
                    g["notes"] = group.Notes;
                ((JObject)config["groups"]!)[group.Name] = g;
            }

            File.WriteAllText(_configPath, config.ToString(Formatting.Indented));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[MCP] Failed to save tool group config: {ex.Message}");
        }
    }
}
