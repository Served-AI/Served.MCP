using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using Newtonsoft.Json.Linq;
using Served.MCP;
using Served.SDK.Client;
using Served.SDK.Models.Projects;
using Served.SDK.Models.Dashboards;
using Served.SDK.Models.Datasource;
using Served.SDK.Tracing;
using Served.SDK.Utilities;

// Configuration - Load from env vars
var baseUrl = Environment.GetEnvironmentVariable("SERVED_API_URL") ?? "https://app.served.dk";
var token = Environment.GetEnvironmentVariable("SERVED_API_TOKEN") ?? "";
var tenant = Environment.GetEnvironmentVariable("SERVED_TENANT") ?? "";
var enableTracing = Environment.GetEnvironmentVariable("SERVED_TRACING_ENABLED")?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true;

// SDK Initialization with Tracing
var clientBuilder = new ServedClientBuilder()
    .WithBaseUrl(baseUrl)
    .WithToken(token)
    .WithTenant(tenant);

// Enable tracing if configured (default: on)
if (enableTracing)
{
    clientBuilder.WithTracing(options =>
    {
        options.ServiceName = "served-mcp-server";
        options.ServiceVersion = "2026.1.2";
        options.Environment = Environment.GetEnvironmentVariable("SERVED_ENVIRONMENT") ?? "development";

        // Enable Forge integration for Served-native observability
        options.EnableForge = true;

        // Error detection settings
        options.ErrorDetection.CaptureSlowRequests = true;
        options.ErrorDetection.SlowRequestThresholdMs = 3000;

        // Sample all MCP requests (important for debugging)
        options.SamplingRate = 1.0;
        options.AlwaysSampleErrors = true;
    });
}

using var client = clientBuilder.Build();
var server = new McpServer(client, baseUrl, token, tenant);

// Log tracing status
Console.Error.WriteLine($"[MCP] Tracing enabled: {client.IsTracingEnabled}");

// ----------------------------------------------------------------------
// Project Tools
// ----------------------------------------------------------------------

server.RegisterTool("GetProjects", async (args) =>
{
    var query = new ProjectQueryParams { Take = 50 };
    var projects = await client.Projects.GetAllAsync(query);
    return projects;
});

server.RegisterTool("CreateProject", async (args) =>
{
    var request = args.ToObject<CreateProjectRequest>()
                  ?? throw new ArgumentException("Invalid arguments");
    var project = await client.Projects.CreateAsync(request);
    return project;
});

server.RegisterTool("GetProjectDetails", async (args) =>
{
    var projectId = args.GetRequiredInt("projectId");
    var project = await client.Projects.GetAsync(projectId);
    return project;
});

// ----------------------------------------------------------------------
// API Key Tools
// ----------------------------------------------------------------------

server.RegisterTool("GetApiKeys", async (args) =>
{
    var apiKeys = await client.ApiKeys.ListAsync();

    return ResponseFormatter.Create()
        .CountHeaderDa(apiKeys.Count, "API nøgle", "API nøgler")
        .EntityList("apikey", apiKeys, k => k.Id, (f, key) =>
        {
            f.PropString("name", key.Name)
             .PropString("prefix", key.KeyHint)
             .PropList("scopes", key.Scopes)
             .PropDateTime("lastUsed", key.LastUsedAt, "Aldrig")
             .PropString("status", key.IsActive ? "Aktiv" : "Inaktiv");
        })
        .ToString();
});

server.RegisterTool("GetApiKeyScopes", async (args) =>
{
    var scopes = await client.ApiKeys.GetScopesAsync();
    return ResponseFormatter.Create()
        .Header("Tilgængelige API Key scopes:")
        .ForEach(scopes, (f, scope) => f.Bullet($"{scope.Scope}: {scope.Description}"))
        .ToString();
});

server.RegisterTool("CreateApiKey", async (args) =>
{
    // Use SDK utilities for parameter parsing
    var name = args.GetRequiredString("name");
    var scopes = args.GetStringList("scopes", required: true);
    var expiresInDays = args.GetOptionalInt("expiresInDays", 365);

    var expiresAt = DateTime.UtcNow.AddDays(expiresInDays);
    var result = await client.ApiKeys.CreateAsync(name, scopes, expiresAt);

    return ResponseFormatter.Create()
        .Success("API nøgle oprettet succesfuldt!")
        .Line()
        .Entity("apikey", result.ApiKey.Id)
            .PropString("name", result.ApiKey.Name)
            .PropString("prefix", result.ApiKey.KeyHint)
        .EndEntity()
        .Warning("Gem denne nøgle sikkert - den vises kun én gang:")
        .Line()
        .Line(result.PlainKey)
        .ToString();
});

server.RegisterTool("RevokeApiKey", async (args) =>
{
    var apiKeyId = args.GetRequiredInt("apiKeyId");
    await client.ApiKeys.DeactivateAsync(apiKeyId);
    return ResponseFormatter.DeletedDa("API nøgle", apiKeyId);
});

// ----------------------------------------------------------------------
// Dashboard Tools (using SDK)
// ----------------------------------------------------------------------

server.RegisterTool("GetDashboards", async (args) =>
{
    var dashboards = await client.Dashboards.GetAllAsync();

    return ResponseFormatter.Create()
        .CountHeaderDa(dashboards.Count, "dashboard", "dashboards")
        .EntityList("dashboard", dashboards, d => d.Id, (f, d) =>
        {
            f.PropString("name", d.Name)
             .PropStringIfNotEmpty("description", d.Description)
             .Prop("widgetCount", d.WidgetCount)
             .PropBool("isDefault", d.IsDefault)
             .PropString("scope", d.Scope.ToString());
        })
        .ToString();
});

server.RegisterTool("GetDashboardDetails", async (args) =>
{
    var dashboardId = args.GetRequiredInt("dashboardId");
    var dashboard = await client.Dashboards.GetAsync(dashboardId);

    var f = ResponseFormatter.Create()
        .Entity("dashboard", dashboard.Id)
            .PropString("name", dashboard.Name)
            .PropStringIfNotEmpty("description", dashboard.Description)
            .PropBool("isDefault", dashboard.IsDefault)
            .PropString("theme", dashboard.Theme ?? "light")
            .Prop("refreshInterval", dashboard.RefreshIntervalSeconds ?? 0)
            .PropString("scope", dashboard.Scope.ToString())
            .Line()
            .PropCount("widgets", dashboard.Widgets.Count);

    foreach (var w in dashboard.Widgets)
    {
        f.NestedEntity("widget", w.Id)
            .PropString("type", w.TypeName)
            .PropString("title", w.Title)
            .Prop("position", $"({w.GridX}, {w.GridY})")
            .Prop("size", $"{w.GridWidth}x{w.GridHeight}")
        .EndNestedEntity();
    }

    return f.EndPropCount().EndEntity().ToString();
});

server.RegisterTool("CreateDashboard", async (args) =>
{
    var request = new CreateDashboardRequest
    {
        Name = args.GetRequiredString("name"),
        Description = args.GetOptionalString("description"),
        Theme = args.GetOptionalString("theme"),
        RefreshIntervalSeconds = args.GetOptionalIntOrNull("refreshIntervalSeconds"),
        WorkspaceId = args.GetOptionalIntOrNull("workspaceId"),
        ProjectId = args.GetOptionalIntOrNull("projectId"),
        Scope = args.GetOptionalEnum("scope", DashboardScope.Personal)
    };

    var dashboard = await client.Dashboards.CreateAsync(request);
    return ResponseFormatter.CreatedDa("Dashboard", dashboard.Id, dashboard.Name);
});

server.RegisterTool("UpdateDashboard", async (args) =>
{
    var dashboardId = args.GetRequiredInt("dashboardId");
    var request = new UpdateDashboardRequest
    {
        Name = args.GetOptionalString("name"),
        Description = args.GetOptionalString("description"),
        Theme = args.GetOptionalString("theme"),
        RefreshIntervalSeconds = args.GetOptionalIntOrNull("refreshIntervalSeconds")
    };
    await client.Dashboards.UpdateAsync(dashboardId, request);
    return ResponseFormatter.UpdatedDa("Dashboard", dashboardId);
});

server.RegisterTool("DeleteDashboard", async (args) =>
{
    var dashboardId = args.GetRequiredInt("dashboardId");
    await client.Dashboards.DeleteAsync(dashboardId);
    return ResponseFormatter.DeletedDa("Dashboard", dashboardId);
});

server.RegisterTool("SetDefaultDashboard", async (args) =>
{
    var dashboardId = args.GetRequiredInt("dashboardId");
    await client.Dashboards.SetDefaultAsync(dashboardId);
    return $"Dashboard {dashboardId} er nu sat som standard.";
});

server.RegisterTool("DuplicateDashboard", async (args) =>
{
    var dashboardId = args.GetRequiredInt("dashboardId");
    var newName = args.GetOptionalString("newName");
    var dashboard = await client.Dashboards.DuplicateAsync(dashboardId, newName);
    return ResponseFormatter.CreatedDa("Dashboard", dashboard.Id, dashboard.Name);
});

// ----------------------------------------------------------------------
// Widget Tools (using SDK)
// ----------------------------------------------------------------------

server.RegisterTool("GetWidgets", async (args) =>
{
    var dashboardId = args.GetRequiredInt("dashboardId");
    var widgets = await client.Dashboards.GetWidgetsAsync(dashboardId);

    return ResponseFormatter.Create()
        .Header($"Dashboard {dashboardId} har {widgets.Count} widgets:")
        .EntityList("widget", widgets, w => w.Id, (f, w) =>
        {
            f.PropString("type", w.TypeName)
             .PropString("title", w.Title)
             .PropStringIfNotEmpty("subtitle", w.Subtitle)
             .Prop("position", $"({w.GridX}, {w.GridY})")
             .Prop("size", $"{w.GridWidth}x{w.GridHeight}");
        })
        .ToString();
});

server.RegisterTool("GetWidgetDetails", async (args) =>
{
    var dashboardId = args.GetRequiredInt("dashboardId");
    var widgetId = args.GetRequiredInt("widgetId");
    var widget = await client.Dashboards.GetWidgetAsync(dashboardId, widgetId);

    return ResponseFormatter.Create()
        .Entity("widget", widget.Id)
            .Prop("dashboardId", dashboardId)
            .PropString("type", widget.TypeName)
            .PropString("title", widget.Title)
            .PropStringIfNotEmpty("subtitle", widget.Subtitle)
            .PropStringIfNotEmpty("icon", widget.Icon)
            .Prop("position", $"({widget.GridX}, {widget.GridY})")
            .Prop("size", $"{widget.GridWidth}x{widget.GridHeight}")
            .Line()
            .Prop("config", widget.Config ?? "{}")
            .Prop("datasourceConfig", widget.DatasourceConfig ?? "{}")
            .Prop("styleConfig", widget.StyleConfig ?? "{}")
        .EndEntity()
        .ToString();
});

server.RegisterTool("CreateWidget", async (args) =>
{
    var dashboardId = args.GetRequiredInt("dashboardId");
    var request = new CreateWidgetRequest
    {
        Type = args.GetRequiredString("type"),
        Title = args.GetRequiredString("title"),
        Subtitle = args.GetOptionalString("subtitle"),
        Icon = args.GetOptionalString("icon"),
        GridX = args.GetOptionalInt("gridX", 0),
        GridY = args.GetOptionalInt("gridY", 0),
        GridWidth = args.GetOptionalInt("gridWidth", 3),
        GridHeight = args.GetOptionalInt("gridHeight", 2),
        Config = args["config"],
        DatasourceConfig = args["datasourceConfig"]
    };
    var widget = await client.Dashboards.AddWidgetAsync(dashboardId, request);
    return ResponseFormatter.CreatedDa("Widget", widget.Id, $"{widget.Title} ({widget.TypeName})");
});

server.RegisterTool("UpdateWidget", async (args) =>
{
    var dashboardId = args.GetRequiredInt("dashboardId");
    var widgetId = args.GetRequiredInt("widgetId");
    var request = new UpdateWidgetRequest
    {
        Title = args.GetOptionalString("title"),
        Subtitle = args.GetOptionalString("subtitle"),
        Icon = args.GetOptionalString("icon"),
        GridX = args.GetOptionalIntOrNull("gridX"),
        GridY = args.GetOptionalIntOrNull("gridY"),
        GridWidth = args.GetOptionalIntOrNull("gridWidth"),
        GridHeight = args.GetOptionalIntOrNull("gridHeight"),
        Config = args["config"],
        DatasourceConfig = args["datasourceConfig"],
        StyleConfig = args["styleConfig"]
    };
    await client.Dashboards.UpdateWidgetAsync(dashboardId, widgetId, request);
    return ResponseFormatter.UpdatedDa("Widget", widgetId);
});

server.RegisterTool("DeleteWidget", async (args) =>
{
    var dashboardId = args.GetRequiredInt("dashboardId");
    var widgetId = args.GetRequiredInt("widgetId");
    await client.Dashboards.DeleteWidgetAsync(dashboardId, widgetId);
    return ResponseFormatter.DeletedDa("Widget", $"{widgetId} (fra dashboard {dashboardId})");
});

server.RegisterTool("UpdateWidgetLayout", async (args) =>
{
    var dashboardId = args.GetRequiredInt("dashboardId");
    var layoutsArray = args.GetArray("layouts", required: true)!;

    var layouts = new List<WidgetLayoutItem>();
    foreach (var item in layoutsArray)
    {
        var itemObj = item as JObject ?? new JObject();
        layouts.Add(new WidgetLayoutItem
        {
            WidgetId = itemObj.GetOptionalInt("widgetId", 0),
            GridX = itemObj.GetOptionalInt("gridX", 0),
            GridY = itemObj.GetOptionalInt("gridY", 0),
            GridWidth = itemObj.GetOptionalInt("gridWidth", 3),
            GridHeight = itemObj.GetOptionalInt("gridHeight", 2)
        });
    }

    await client.Dashboards.UpdateWidgetLayoutAsync(dashboardId, layouts);
    return $"Layout opdateret for {layouts.Count} widgets.";
});

// ----------------------------------------------------------------------
// Datasource / Query Builder Tools (using SDK)
// ----------------------------------------------------------------------

server.RegisterTool("GetDatasourceEntities", async (args) =>
{
    var category = args.GetOptionalString("category");
    var entities = category != null
        ? await client.Datasource.GetEntitiesByCategoryAsync(category)
        : await client.Datasource.GetEntitiesAsync();

    var f = ResponseFormatter.Create()
        .CountHeader(entities.Count, "entity", "tilgængelige");

    var grouped = entities.GroupBy(e => e.Category ?? "Andet");
    foreach (var group in grouped)
    {
        f.Line($"## {group.Key}");
        foreach (var entity in group)
        {
            f.Bullet($"{entity.Name}: {entity.DisplayName}");
        }
        f.Line();
    }
    return f.ToString();
});

server.RegisterTool("GetEntitySchema", async (args) =>
{
    var entityName = args.GetRequiredString("entityName");
    var schema = await client.Datasource.GetEntitySchemaAsync(entityName);

    var f = ResponseFormatter.Create()
        .Entity("entity", schema.Name)
            .PropString("displayName", schema.DisplayName)
            .PropString("category", schema.Category)
            .Line()
            .PropCount("fields", schema.Fields.Count);

    foreach (var field in schema.Fields)
    {
        var flags = new List<string>();
        if (field.IsFilterable) flags.Add("filterable");
        if (field.IsSortable) flags.Add("sortable");
        if (field.IsGroupable) flags.Add("groupable");
        var flagStr = flags.Count > 0 ? $" [{string.Join(", ", flags)}]" : "";
        f.Line($"{field.Name} ({field.DataType}): \"{field.DisplayName}\"{flagStr}");
    }
    f.EndPropCount();

    if (schema.Relations.Count > 0)
    {
        f.Line()
         .PropCount("relations", schema.Relations.Count);
        foreach (var rel in schema.Relations)
        {
            f.Line($"{rel.Name} -> {rel.TargetEntity} ({rel.RelationType})");
        }
        f.EndPropCount();
    }

    return f.EndEntity().ToString();
});

server.RegisterTool("ExecuteDatasourceQuery", async (args) =>
{
    var entity = args.GetRequiredString("entity");

    // Build query using SDK helper
    var query = client.Datasource.CreateQuery(entity);
    query = client.Datasource.SetPagination(query,
        args.GetOptionalInt("limit", 50),
        args.GetOptionalInt("offset", 0));

    // Add fields if specified
    if (args["fields"] is JArray fieldsArray)
    {
        foreach (var f in fieldsArray)
        {
            var fieldName = f["name"]?.Value<string>() ?? f.Value<string>();
            if (!string.IsNullOrEmpty(fieldName))
                query = client.Datasource.AddField(query, fieldName, f["alias"]?.Value<string>());
        }
    }

    // Add filters if specified
    if (args["filters"] is JArray filtersArray)
    {
        foreach (var f in filtersArray)
        {
            query = client.Datasource.AddFilter(query,
                f["field"]?.Value<string>() ?? "",
                f["operator"]?.Value<string>() ?? "eq",
                f["value"],
                f["logicalOperator"]?.Value<string>());
        }
    }

    // Add sorting if specified
    if (args["sorting"] is JArray sortingArray)
    {
        foreach (var s in sortingArray)
        {
            query = client.Datasource.AddSort(query,
                s["field"]?.Value<string>() ?? "",
                s["direction"]?.Value<string>() ?? "asc");
        }
    }

    // Add groupBy if specified
    if (args["groupBy"] is JArray groupByArray)
    {
        foreach (var g in groupByArray)
        {
            query = client.Datasource.AddGroupBy(query,
                g["field"]?.Value<string>() ?? "",
                g["datePart"]?.Value<string>());
        }
    }

    // Add aggregations if specified
    if (args["aggregations"] is JArray aggArray)
    {
        foreach (var a in aggArray)
        {
            query = client.Datasource.AddAggregation(query,
                a["field"]?.Value<string>() ?? "",
                a["function"]?.Value<string>() ?? "count",
                a["alias"]?.Value<string>());
        }
    }

    var result = await client.Datasource.ExecuteQueryAsync(query);

    var sb = new StringBuilder();
    sb.AppendLine($"Query resultat: {result.Meta?.ReturnedCount} / {result.Meta?.TotalCount} rækker");
    sb.AppendLine($"Udført på {result.Meta?.ExecutionTimeMs}ms");
    sb.AppendLine();

    if (result.Meta?.Columns != null && result.Meta.Columns.Count > 0)
    {
        // Header
        sb.AppendLine(string.Join(" | ", result.Meta.Columns.Select(c => c.DisplayName)));
        sb.AppendLine(new string('-', 80));

        // Rows (max 20 for display)
        foreach (var row in result.Data.Take(20))
        {
            var values = result.Meta.Columns.Select(c => row.TryGetValue(c.Name, out var v) ? v?.ToString() ?? "" : "");
            sb.AppendLine(string.Join(" | ", values));
        }

        if (result.Data.Count > 20)
        {
            sb.AppendLine($"... og {result.Data.Count - 20} flere rækker");
        }
    }
    return sb.ToString();
});

server.RegisterTool("PreviewDatasourceQuery", async (args) =>
{
    var entity = args.GetRequiredString("entity");
    var maxRows = args.GetOptionalInt("maxRows", 10);

    var query = client.Datasource.CreateQuery(entity);
    query = client.Datasource.SetPagination(query, maxRows, 0);

    var result = await client.Datasource.PreviewQueryAsync(query, maxRows);
    return result;
});

server.RegisterTool("ValidateDatasourceQuery", async (args) =>
{
    var entity = args.GetRequiredString("entity");
    var query = client.Datasource.CreateQuery(entity);

    var result = await client.Datasource.ValidateQueryAsync(query);

    if (result.IsValid)
    {
        return "Query er valid og kan udføres.";
    }
    else
    {
        return ResponseFormatter.Create()
            .Header("Query validation fejlede:")
            .ForEach(result.Errors, (f, error) => f.Bullet(error))
            .ToString();
    }
});

server.RegisterTool("GetDatasourceCategories", async (args) =>
{
    var categories = await client.Datasource.GetCategoriesAsync();
    return ResponseFormatter.Create()
        .Header("Tilgængelige entity kategorier:")
        .ForEach(categories, (f, cat) => f.Bullet(cat))
        .ToString();
});

// ----------------------------------------------------------------------
// Task Tools (using SDK)
// ----------------------------------------------------------------------

server.RegisterTool("GetTasks", async (args) =>
{
    var projectId = args.GetOptionalIntOrNull("projectId");
    var includeCompleted = args.GetOptionalBool("includeCompleted", false);
    var limit = args.GetOptionalInt("limit", 50);

    List<Served.SDK.Models.Tasks.TaskSummary> tasks;
    if (projectId.HasValue)
    {
        tasks = await client.Tasks.GetByProjectAsync(projectId.Value, includeCompleted);
        if (tasks.Count > limit) tasks = tasks.Take(limit).ToList();
    }
    else
    {
        var query = new Served.SDK.Models.Tasks.TaskQueryParams
        {
            Take = limit,
            IncludeCompleted = includeCompleted
        };
        tasks = await client.Tasks.GetAllAsync(query);
    }

    return ResponseFormatter.Create()
        .CountHeaderDa(tasks.Count, "opgave", "opgaver")
        .EntityList("task", tasks, t => t.Id, (f, t) =>
        {
            f.PropString("name", t.Name)
             .PropStringIfNotEmpty("taskNo", t.TaskNo)
             .Prop("projectId", t.ProjectId)
             .PropString("status", t.Status.ToString())
             .PropDate("dueDate", t.DueDate)
             .PropBool("isCompleted", t.IsCompleted);
        })
        .ToString();
});

server.RegisterTool("GetTaskDetails", async (args) =>
{
    var taskId = args.GetRequiredInt("taskId");
    var task = await client.Tasks.GetAsync(taskId);

    return ResponseFormatter.Create()
        .Entity("task", task.Id)
            .PropString("name", task.Name)
            .PropStringIfNotEmpty("taskNo", task.TaskNo)
            .PropStringIfNotEmpty("description", task.Description)
            .Prop("projectId", task.ProjectId)
            .PropStringIfNotEmpty("projectName", task.ProjectName)
            .PropString("status", task.Status.ToString())
            .PropString("priority", task.Priority?.ToString() ?? "Normal")
            .PropIfNotEmpty("assignedTo", task.AssignedTo)
            .PropDate("dueDate", task.DueDate)
            .PropIfNotEmpty("estimatedHours", task.EstimatedHours)
            .Prop("progress", task.Progress)
            .PropBool("isCompleted", task.IsCompleted)
        .EndEntity()
        .ToString();
});

server.RegisterTool("CreateTask", async (args) =>
{
    var priority = args.GetOptionalEnumOrNull<Served.SDK.Models.Tasks.TaskPriority>("priority");

    var request = new Served.SDK.Models.Tasks.CreateTaskRequest
    {
        Name = args.GetRequiredString("name"),
        ProjectId = args.GetRequiredInt("projectId"),
        Description = args.GetOptionalString("description"),
        AssignedTo = args.GetOptionalIntOrNull("assignedTo"),
        Priority = priority,
        DueDate = args.GetOptionalDateTime("dueDate"),
        EstimatedHours = args["estimatedHours"]?.Value<double?>()
    };

    var task = await client.Tasks.CreateAsync(request);
    return ResponseFormatter.CreatedDa("Opgave", task.Id, task.Name);
});

server.RegisterTool("UpdateTask", async (args) =>
{
    var taskId = args.GetRequiredInt("taskId");
    var status = args.GetOptionalEnumOrNull<Served.SDK.Models.Tasks.TaskStatus>("status");
    var priority = args.GetOptionalEnumOrNull<Served.SDK.Models.Tasks.TaskPriority>("priority");

    var request = new Served.SDK.Models.Tasks.UpdateTaskRequest
    {
        Name = args.GetOptionalString("name"),
        Description = args.GetOptionalString("description"),
        Status = status,
        Priority = priority,
        AssignedTo = args.GetOptionalIntOrNull("assignedTo"),
        DueDate = args.GetOptionalDateTime("dueDate"),
        EstimatedHours = args["estimatedHours"]?.Value<double?>(),
        Progress = args["progress"]?.Value<double?>()
    };
    await client.Tasks.UpdateAsync(taskId, request);
    return ResponseFormatter.UpdatedDa("Opgave", taskId);
});

server.RegisterTool("DeleteTask", async (args) =>
{
    var taskId = args.GetRequiredInt("taskId");
    await client.Tasks.DeleteAsync(taskId);
    return ResponseFormatter.DeletedDa("Opgave", taskId);
});

server.RegisterTool("UpdateTaskStatus", async (args) =>
{
    var taskId = args.GetRequiredInt("taskId");
    var status = args.GetRequiredEnum<Served.SDK.Models.Tasks.TaskStatus>("status");
    var request = new Served.SDK.Models.Tasks.UpdateTaskStatusRequest { Status = status };
    await client.Tasks.UpdateStatusAsync(taskId, request);
    return $"Status for opgave {taskId} ændret til '{status}'.";
});

// ----------------------------------------------------------------------
// Time Registration Tools (using SDK)
// ----------------------------------------------------------------------

server.RegisterTool("GetTimeRegistrations", async (args) =>
{
    var startDate = args.GetOptionalDateTime("startDate");
    var endDate = args.GetOptionalDateTime("endDate");
    var projectId = args.GetOptionalIntOrNull("projectId");
    var limit = args.GetOptionalInt("limit", 50);

    List<Served.SDK.Models.TimeRegistration.TimeRegistrationDetail> registrations;

    if (startDate.HasValue && endDate.HasValue)
    {
        registrations = await client.TimeRegistrations.GetByDateRangeAsync(startDate.Value, endDate.Value, limit);
    }
    else if (projectId.HasValue)
    {
        registrations = await client.TimeRegistrations.GetByProjectAsync(projectId.Value, limit);
    }
    else
    {
        var query = new Served.SDK.Models.TimeRegistration.TimeRegistrationQueryParams { Take = limit };
        registrations = await client.TimeRegistrations.GetAllAsync(query);
    }

    return ResponseFormatter.Create()
        .CountHeaderDa(registrations.Count, "tidsregistrering", "tidsregistreringer")
        .EntityList("timereg", registrations, r => r.Id, (f, r) =>
        {
            f.PropDate("date", r.Date)
             .Prop("hours", $"{(r.Hours ?? 0):F2}")
             .Prop("projectId", r.ProjectId)
             .PropIfNotEmpty("taskId", r.TaskId)
             .PropStringIfNotEmpty("comment", r.Comment)
             .PropBool("billable", r.Billable);
        })
        .ToString();
});

server.RegisterTool("CreateTimeRegistration", async (args) =>
{
    var start = args.GetOptionalDateTime("start") ?? args.GetOptionalDateTime("date") ?? DateTime.Today;
    var end = args.GetOptionalDateTime("end") ?? start.AddHours(1);
    var hours = args["hours"]?.Value<double>() ?? 1;
    var minutes = args.GetOptionalIntOrNull("minutes") ?? (int)(hours * 60);
    var billable = args.GetOptionalBool("billable", true);

    var request = new Served.SDK.Models.TimeRegistration.CreateTimeRegistrationRequest
    {
        ProjectId = args.GetOptionalIntOrNull("projectId"),
        TaskId = args.GetOptionalIntOrNull("taskId"),
        Start = start,
        End = end,
        Minutes = minutes,
        Description = args.GetOptionalString("description"),
        Billable = billable
    };

    var reg = await client.TimeRegistrations.CreateAsync(request);
    return ResponseFormatter.CreatedDa("Tidsregistrering", reg.Id, $"{reg.Hours:F2} timer");
});

server.RegisterTool("UpdateTimeRegistration", async (args) =>
{
    var id = args.GetRequiredInt("id");
    var request = new Served.SDK.Models.TimeRegistration.UpdateTimeRegistrationRequest
    {
        TaskId = args.GetOptionalIntOrNull("taskId"),
        ProjectId = args.GetOptionalIntOrNull("projectId"),
        Start = args.GetOptionalDateTime("start"),
        End = args.GetOptionalDateTime("end"),
        Description = args.GetOptionalString("description"),
        Billable = args["billable"]?.Value<bool?>()
    };
    await client.TimeRegistrations.UpdateAsync(id, request);
    return ResponseFormatter.UpdatedDa("Tidsregistrering", id);
});

server.RegisterTool("DeleteTimeRegistration", async (args) =>
{
    var id = args.GetRequiredInt("id");
    await client.TimeRegistrations.DeleteAsync(id);
    return ResponseFormatter.DeletedDa("Tidsregistrering", id);
});

// ----------------------------------------------------------------------
// Customer Tools (using SDK)
// ----------------------------------------------------------------------

server.RegisterTool("GetCustomers", async (args) =>
{
    var search = args.GetOptionalString("search");
    var limit = args.GetOptionalInt("limit", 50);

    List<Served.SDK.Models.Customers.CustomerSummary> customers;
    if (!string.IsNullOrEmpty(search))
    {
        customers = await client.Customers.SearchAsync(search, limit);
    }
    else
    {
        var query = new Served.SDK.Models.Customers.CustomerQueryParams { Take = limit };
        customers = await client.Customers.GetAllAsync(query);
    }

    return ResponseFormatter.Create()
        .CountHeaderDa(customers.Count, "kunde", "kunder")
        .EntityList("customer", customers, c => c.Id, (f, c) =>
        {
            f.PropString("name", c.Name)
             .PropStringIfNotEmpty("customerNo", c.CustomerNo)
             .PropStringIfNotEmpty("email", c.Email)
             .PropStringIfNotEmpty("phone", c.Phone)
             .PropBool("isActive", c.IsActive);
        })
        .ToString();
});

server.RegisterTool("GetCustomerDetails", async (args) =>
{
    var customerId = args.GetRequiredInt("customerId");
    var customer = await client.Customers.GetAsync(customerId);

    return ResponseFormatter.Create()
        .Entity("customer", customer.Id)
            .PropString("name", customer.Name)
            .PropStringIfNotEmpty("customerNo", customer.CustomerNo)
            .PropStringIfNotEmpty("email", customer.Email)
            .PropStringIfNotEmpty("phone", customer.Phone)
            .PropStringIfNotEmpty("website", customer.Website)
            .PropStringIfNotEmpty("vatNumber", customer.VatNumber)
            .PropStringIfNotEmpty("address", customer.Address)
            .PropStringIfNotEmpty("city", customer.City)
            .PropStringIfNotEmpty("postalCode", customer.PostalCode)
            .PropStringIfNotEmpty("country", customer.Country)
            .PropBool("isActive", customer.IsActive)
        .EndEntity()
        .ToString();
});

// ----------------------------------------------------------------------
// Agent Discovery & Coordination Tools (for Atlas)
// ----------------------------------------------------------------------

server.RegisterTool("GetActiveAgents", async (args) =>
{
    var statusFilter = args["status"]?.Value<string>();
    var agentType = args["agentType"]?.Value<string>();
    var isOnline = args["isOnline"]?.Value<bool?>();

    var criteria = new JObject();
    if (!string.IsNullOrEmpty(statusFilter)) criteria["status"] = statusFilter;
    if (!string.IsNullOrEmpty(agentType)) criteria["agentType"] = agentType;
    if (isOnline.HasValue) criteria["isOnline"] = isOnline.Value;

    var response = await server.Http.PostAsJsonAsync("/api/agents/coordination/GetActiveAgents",
        new StringContent(criteria.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get active agents: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var agents = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"Fundet {agents.Count} aktive agenter:");
    sb.AppendLine();

    foreach (var agent in agents)
    {
        sb.AppendLine($"@agent[{agent["agentId"]}] {{");
        sb.AppendLine($"  name: \"{agent["agentName"]}\"");
        sb.AppendLine($"  type: \"{agent["agentType"] ?? ""}\"");
        sb.AppendLine($"  status: \"{agent["status"]}\"");
        sb.AppendLine($"  currentActivity: \"{agent["currentActivity"] ?? ""}\"");
        sb.AppendLine($"  progressPercent: {agent["progressPercent"] ?? 0}");
        sb.AppendLine($"  currentTaskId: \"{agent["currentTaskId"] ?? ""}\"");
        sb.AppendLine($"  currentBranch: \"{agent["currentBranch"] ?? ""}\"");
        sb.AppendLine($"  activeSessionId: {agent["activeSessionId"] ?? "null"}");
        sb.AppendLine($"  lastActivityAt: \"{agent["lastActivityAt"] ?? ""}\"");
        sb.AppendLine("}");
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetAgentContext", async (args) =>
{
    var agentId = args["agentId"]?.Value<int>() ?? throw new ArgumentException("agentId required");

    var response = await server.Http.PostAsJsonAsync($"/api/agents/coordination/GetAgentContext?agentId={agentId}",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get agent context: {response.StatusCode}");

    var context = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"@agentContext[{context["agentId"]}] {{");
    sb.AppendLine($"  agentName: \"{context["agentName"]}\"");
    sb.AppendLine($"  sessionId: {context["sessionId"] ?? "null"}");
    sb.AppendLine($"  taskId: \"{context["taskId"] ?? ""}\"");
    sb.AppendLine($"  taskName: \"{context["taskName"] ?? ""}\"");
    sb.AppendLine();

    // Active Files
    var files = context["activeFiles"] as JArray ?? new JArray();
    sb.AppendLine($"  activeFiles: [{files.Count}] {{");
    foreach (var file in files.Take(10))
    {
        sb.AppendLine($"    {file["lastOperation"]}: {file["filePath"]}");
    }
    if (files.Count > 10) sb.AppendLine($"    ... and {files.Count - 10} more");
    sb.AppendLine("  }");
    sb.AppendLine();

    // Todo List
    var todos = context["todoList"] as JArray ?? new JArray();
    sb.AppendLine($"  todoList: [{todos.Count}] {{");
    foreach (var todo in todos)
    {
        var status = todo["status"]?.ToString();
        var marker = status == "Completed" ? "[x]" : status == "InProgress" ? "[>]" : "[ ]";
        sb.AppendLine($"    {marker} {todo["content"]}");
    }
    sb.AppendLine("  }");
    sb.AppendLine();

    // Git State
    var git = context["gitState"];
    if (git != null)
    {
        sb.AppendLine($"  gitState: {{");
        sb.AppendLine($"    branch: \"{git["branch"] ?? ""}\"");
        sb.AppendLine($"    uncommittedChanges: {git["uncommittedChanges"] ?? 0}");
        sb.AppendLine($"    commitCount: {git["commitCount"] ?? 0}");
        sb.AppendLine($"    prUrl: \"{git["pullRequestUrl"] ?? ""}\"");
        sb.AppendLine($"    ciStatus: \"{git["ciStatus"] ?? ""}\"");
        sb.AppendLine("  }");
    }

    // Recent Actions
    var actions = context["recentActions"] as JArray ?? new JArray();
    sb.AppendLine($"  recentActions: [{actions.Count}] {{");
    foreach (var action in actions.Take(5))
    {
        sb.AppendLine($"    [{action["timestamp"]}] {action["actionType"]}: {action["description"]}");
    }
    sb.AppendLine("  }");

    sb.AppendLine("}");
    return sb.ToString();
});

server.RegisterTool("SearchAgentActivity", async (args) =>
{
    var query = args["query"]?.Value<string>() ?? throw new ArgumentException("query required");
    var limit = args["limit"]?.Value<int>() ?? 20;

    var response = await server.Http.PostAsJsonAsync($"/api/agents/coordination/SearchActivity?query={Uri.EscapeDataString(query)}&limit={limit}",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to search agent activity: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var results = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"Søgeresultater for '{query}': {results.Count} hits");
    sb.AppendLine();

    foreach (var r in results)
    {
        sb.AppendLine($"[{r["timestamp"]}] Agent #{r["agentId"]} ({r["agentName"]}):");
        sb.AppendLine($"  Type: {r["activityType"]}");
        sb.AppendLine($"  Description: {r["description"]}");
        if (r["filePath"] != null) sb.AppendLine($"  File: {r["filePath"]}");
        if (r["taskId"] != null) sb.AppendLine($"  Task: {r["taskId"]}");
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetCoordinationInfo", async (args) =>
{
    var response = await server.Http.PostAsJsonAsync("/api/agents/coordination/GetCoordinationInfo",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get coordination info: {response.StatusCode}");

    var info = JObject.Parse(await response.Content.ReadAsStringAsync());
    var agents = info["activeAgents"] as JArray ?? new JArray();
    var tasks = info["taskAssignments"] as JArray ?? new JArray();
    var files = info["filesInUse"] as JArray ?? new JArray();
    var conflicts = info["potentialConflicts"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine("=== Agent Coordination Overview ===");
    sb.AppendLine();

    sb.AppendLine($"## Active Agents ({agents.Count})");
    foreach (var agent in agents)
    {
        sb.AppendLine($"  - Agent #{agent["agentId"]} ({agent["agentName"]}): {agent["status"]} - {agent["currentActivity"] ?? "Idle"}");
    }
    sb.AppendLine();

    sb.AppendLine($"## Task Assignments ({tasks.Count})");
    foreach (var task in tasks)
    {
        sb.AppendLine($"  - Task {task["taskId"]} -> Agent #{task["agentId"]} ({task["agentName"]}) [{task["progressPercent"] ?? 0}%]");
    }
    sb.AppendLine();

    sb.AppendLine($"## Files In Use ({files.Count})");
    foreach (var file in files.Take(10))
    {
        sb.AppendLine($"  - {file["filePath"]} ({file["operation"]}) by Agent #{file["agentId"]}");
    }
    if (files.Count > 10) sb.AppendLine($"  ... and {files.Count - 10} more");
    sb.AppendLine();

    if (conflicts.Count > 0)
    {
        sb.AppendLine($"## ⚠️ Potential Conflicts ({conflicts.Count})");
        foreach (var conflict in conflicts)
        {
            sb.AppendLine($"  - [{conflict["severity"]}] {conflict["type"]}: {conflict["description"]}");
            sb.AppendLine($"    Agents: {string.Join(", ", (conflict["agentIds"] as JArray ?? new JArray()).Select(a => $"#{a}"))}");
        }
    }
    else
    {
        sb.AppendLine("## ✓ No conflicts detected");
    }

    return sb.ToString();
});

server.RegisterTool("CoordinateWithAgent", async (args) =>
{
    var fromAgentId = args["fromAgentId"]?.Value<int>() ?? throw new ArgumentException("fromAgentId required");
    var targetAgentId = args["targetAgentId"]?.Value<int>() ?? throw new ArgumentException("targetAgentId required");
    var action = args["action"]?.Value<string>() ?? throw new ArgumentException("action required");
    var message = args["message"]?.Value<string>();
    var reason = args["reason"]?.Value<string>();

    var request = new JObject
    {
        ["targetAgentId"] = targetAgentId,
        ["action"] = action,
        ["message"] = message,
        ["reason"] = reason
    };

    var response = await server.Http.PostAsJsonAsync($"/api/agents/coordination/Coordinate?fromAgentId={fromAgentId}",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to send coordination request: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine("Coordination Request Result:");
    sb.AppendLine($"  Success: {result["success"]}");
    sb.AppendLine($"  Status: {result["status"]}");
    sb.AppendLine($"  Message: {result["message"]}");
    return sb.ToString();
});

server.RegisterTool("GetFilesInUse", async (args) =>
{
    var pathFilter = args["pathFilter"]?.Value<string>();
    var url = "/api/agents/coordination/GetFilesInUse";
    if (!string.IsNullOrEmpty(pathFilter)) url += $"?pathFilter={Uri.EscapeDataString(pathFilter)}";

    var response = await server.Http.PostAsJsonAsync(url,
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get files in use: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var files = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"Filer i brug ({files.Count}):");
    sb.AppendLine();

    foreach (var file in files)
    {
        sb.AppendLine($"  {file["filePath"]}");
        sb.AppendLine($"    Operation: {file["operation"]} by Agent #{file["agentId"]} ({file["agentName"]})");
        sb.AppendLine($"    Since: {file["since"]}");
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("DetectConflicts", async (args) =>
{
    var response = await server.Http.PostAsJsonAsync("/api/agents/coordination/DetectConflicts",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to detect conflicts: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var conflicts = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    if (conflicts.Count == 0)
    {
        sb.AppendLine("✓ Ingen konflikter detekteret");
    }
    else
    {
        sb.AppendLine($"⚠️ {conflicts.Count} potentielle konflikter detekteret:");
        sb.AppendLine();

        foreach (var conflict in conflicts)
        {
            sb.AppendLine($"@conflict {{");
            sb.AppendLine($"  type: {conflict["type"]}");
            sb.AppendLine($"  severity: {conflict["severity"]}");
            sb.AppendLine($"  description: {conflict["description"]}");
            sb.AppendLine($"  agents: [{string.Join(", ", (conflict["agentIds"] as JArray ?? new JArray()).Select(a => $"#{a}"))}]");
            if (conflict["filePath"] != null) sb.AppendLine($"  file: {conflict["filePath"]}");
            if (conflict["taskId"] != null) sb.AppendLine($"  task: {conflict["taskId"]}");
            sb.AppendLine("}");
            sb.AppendLine();
        }
    }
    return sb.ToString();
});

// ----------------------------------------------------------------------
// Integration Management Tools (for Atlas)
// ----------------------------------------------------------------------

server.RegisterTool("GetAvailableIntegrations", async (args) =>
{
    var category = args["category"]?.Value<string>();

    var url = "/api/integrations/available";
    if (!string.IsNullOrEmpty(category)) url += $"?category={Uri.EscapeDataString(category)}";

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get integrations: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var integrations = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"Tilgængelige integrationer ({integrations.Count}):");
    sb.AppendLine();

    var grouped = integrations.GroupBy(i => i["category"]?.ToString() ?? "Other");
    foreach (var group in grouped)
    {
        sb.AppendLine($"## {group.Key}");
        foreach (var integration in group)
        {
            sb.AppendLine($"  @integration[{integration["integrationId"]}] {{");
            sb.AppendLine($"    name: \"{integration["name"]}\"");
            sb.AppendLine($"    displayName: \"{integration["displayName"]}\"");
            sb.AppendLine($"    description: \"{integration["description"] ?? ""}\"");
            sb.AppendLine($"    capabilities: [{string.Join(", ", (integration["capabilities"] as JArray ?? new JArray()).Select(c => c.ToString()))}]");
            sb.AppendLine($"    requiresApiKey: {integration["requiresApiKey"] ?? false}");
            sb.AppendLine($"    supportsOAuth: {integration["supportsOAuth"] ?? false}");
            sb.AppendLine($"  }}");
        }
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetConfiguredIntegrations", async (args) =>
{
    var category = args["category"]?.Value<string>();
    var onlyActive = args["onlyActive"]?.Value<bool>() ?? false;

    var url = "/api/integrations/configured";
    var queryParams = new List<string>();
    if (!string.IsNullOrEmpty(category)) queryParams.Add($"category={Uri.EscapeDataString(category)}");
    if (onlyActive) queryParams.Add("onlyActive=true");
    if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get configured integrations: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var handlers = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"Konfigurerede integrationer ({handlers.Count}):");
    sb.AppendLine();

    foreach (var handler in handlers)
    {
        var status = handler["status"];
        var statusIcon = status?["isConnected"]?.Value<bool>() == true ? "✓" : "✗";

        sb.AppendLine($"@integrationHandler[{handler["id"]}] {{");
        sb.AppendLine($"  name: \"{handler["name"] ?? handler["integrationId"]}\"");
        sb.AppendLine($"  integrationId: \"{handler["integrationId"]}\"");
        sb.AppendLine($"  displayName: \"{handler["displayName"]}\"");
        sb.AppendLine($"  category: \"{handler["category"] ?? ""}\"");
        sb.AppendLine($"  status: {statusIcon} {(status?["isConnected"]?.Value<bool>() == true ? "Connected" : "Disconnected")}");
        sb.AppendLine($"  isActivated: {handler["isActivated"] ?? false}");
        if (status?["hasError"]?.Value<bool>() == true)
        {
            sb.AppendLine($"  error: \"{status["errorMessage"]}\"");
        }
        sb.AppendLine($"  createdAt: \"{handler["createdAt"] ?? ""}\"");
        sb.AppendLine($"}}");
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetIntegrationStatus", async (args) =>
{
    var handlerId = args["handlerId"]?.Value<int>() ?? throw new ArgumentException("handlerId required");

    var response = await server.Http.GetAsync($"/api/integrations/handlers/{handlerId}/status");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get integration status: {response.StatusCode}");

    var status = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"@integrationStatus[{handlerId}] {{");
    sb.AppendLine($"  isConnected: {status["isConnected"] ?? false}");
    sb.AppendLine($"  isActivated: {status["isActivated"] ?? false}");
    sb.AppendLine($"  isConfigured: {status["isConfigured"] ?? false}");
    sb.AppendLine($"  hasError: {status["hasError"] ?? false}");
    if (status["errorMessage"] != null)
    {
        sb.AppendLine($"  errorMessage: \"{status["errorMessage"]}\"");
    }
    sb.AppendLine($"  lastCheckedAt: \"{status["lastCheckedAt"] ?? DateTime.UtcNow}\"");
    sb.AppendLine($"}}");
    return sb.ToString();
});

server.RegisterTool("TestIntegrationConnection", async (args) =>
{
    var handlerId = args["handlerId"]?.Value<int>() ?? throw new ArgumentException("handlerId required");

    var response = await server.Http.PostAsJsonAsync($"/api/integrations/handlers/{handlerId}/test",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to test integration: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"Integration Connection Test Result:");
    sb.AppendLine($"  Success: {(result["success"]?.Value<bool>() == true ? "✓ Yes" : "✗ No")}");
    sb.AppendLine($"  Message: {result["message"] ?? "No message"}");
    if (result["latencyMs"] != null)
    {
        sb.AppendLine($"  Latency: {result["latencyMs"]}ms");
    }
    return sb.ToString();
});

server.RegisterTool("GetIntegrationUsage", async (args) =>
{
    var handlerId = args["handlerId"]?.Value<int>();
    var startDate = args["startDate"]?.Value<DateTime?>() ?? DateTime.UtcNow.AddDays(-30);
    var endDate = args["endDate"]?.Value<DateTime?>() ?? DateTime.UtcNow;

    var url = handlerId.HasValue
        ? $"/api/integrations/handlers/{handlerId}/usage"
        : "/api/integrations/usage";
    url += $"?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}";

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get usage: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"Integration Usage ({startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd}):");
    sb.AppendLine();

    // Overall stats
    if (result["summary"] != null)
    {
        var summary = result["summary"];
        sb.AppendLine($"## Summary");
        sb.AppendLine($"  Total Calls: {summary["totalCalls"] ?? 0}");
        sb.AppendLine($"  Successful: {summary["successfulCalls"] ?? 0}");
        sb.AppendLine($"  Failed: {summary["failedCalls"] ?? 0}");
        sb.AppendLine($"  Avg Latency: {summary["avgLatencyMs"] ?? 0}ms");
        sb.AppendLine($"  Estimated Cost: ${summary["estimatedCost"] ?? 0:F4}");
        sb.AppendLine();
    }

    // Per-integration breakdown
    var byIntegration = result["byIntegration"] as JArray ?? new JArray();
    if (byIntegration.Count > 0)
    {
        sb.AppendLine($"## By Integration");
        foreach (var item in byIntegration)
        {
            sb.AppendLine($"  {item["integrationName"]}:");
            sb.AppendLine($"    Calls: {item["calls"]}, Success Rate: {item["successRate"] ?? 0:P1}");
            sb.AppendLine($"    Cost: ${item["estimatedCost"] ?? 0:F4}");
        }
        sb.AppendLine();
    }

    // Daily breakdown
    var daily = result["daily"] as JArray ?? new JArray();
    if (daily.Count > 0)
    {
        sb.AppendLine($"## Daily ({daily.Count} days)");
        foreach (var day in daily.Take(7))
        {
            sb.AppendLine($"  {day["date"]}: {day["calls"]} calls, ${day["cost"] ?? 0:F4}");
        }
        if (daily.Count > 7) sb.AppendLine($"  ... and {daily.Count - 7} more days");
    }

    return sb.ToString();
});

server.RegisterTool("GetIntegrationMetadataSchema", async (args) =>
{
    var integrationId = args["integrationId"]?.Value<string>() ?? throw new ArgumentException("integrationId required");

    var response = await server.Http.GetAsync($"/api/integrations/{integrationId}/schema");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get schema: {response.StatusCode}");

    var schema = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"@integrationSchema[{integrationId}] {{");

    var sections = schema["sections"] as JArray ?? new JArray();
    var fields = schema["fields"] as JArray ?? new JArray();

    foreach (var section in sections)
    {
        sb.AppendLine($"  ## {section["title"]} (id: {section["id"]})");
        var sectionFields = fields.Where(f => f["section"]?.ToString() == section["id"]?.ToString());
        foreach (var field in sectionFields)
        {
            var required = field["required"]?.Value<bool>() == true ? "*" : "";
            sb.AppendLine($"    {field["name"]}{required} ({field["fieldType"]}): \"{field["displayName"]}\"");
            if (field["description"] != null)
                sb.AppendLine($"      // {field["description"]}");
            if (field["options"] is JArray options && options.Count > 0)
            {
                sb.AppendLine($"      Options: [{string.Join(", ", options.Select(o => o["value"]?.ToString()))}]");
            }
        }
        sb.AppendLine();
    }

    sb.AppendLine($"}}");
    return sb.ToString();
});

server.RegisterTool("ActivateIntegration", async (args) =>
{
    var integrationId = args["integrationId"]?.Value<string>() ?? throw new ArgumentException("integrationId required");
    var settings = args["settings"] as JObject ?? new JObject();

    var request = new JObject
    {
        ["integrationId"] = integrationId,
        ["settings"] = settings
    };

    var response = await server.Http.PostAsJsonAsync("/api/integrations/activate",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to activate integration: {response.StatusCode} - {error}");
    }

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"Integration Activation Result:");
    sb.AppendLine($"  Success: {(result["success"]?.Value<bool>() == true ? "✓ Yes" : "✗ No")}");
    sb.AppendLine($"  Handler ID: {result["handlerId"]}");
    if (result["message"] != null)
        sb.AppendLine($"  Message: {result["message"]}");
    if (result["authorizationUrl"] != null)
        sb.AppendLine($"  OAuth URL: {result["authorizationUrl"]}");
    return sb.ToString();
});

server.RegisterTool("DeactivateIntegration", async (args) =>
{
    var handlerId = args["handlerId"]?.Value<int>() ?? throw new ArgumentException("handlerId required");

    var response = await server.Http.PostAsJsonAsync($"/api/integrations/handlers/{handlerId}/deactivate",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to deactivate integration: {response.StatusCode}");

    return $"Integration handler {handlerId} deactivated successfully.";
});

server.RegisterTool("UpdateIntegrationSettings", async (args) =>
{
    var handlerId = args["handlerId"]?.Value<int>() ?? throw new ArgumentException("handlerId required");
    var settings = args["settings"] as JObject ?? throw new ArgumentException("settings required");

    var response = await server.Http.PutAsync($"/api/integrations/handlers/{handlerId}/settings",
        new StringContent(settings.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to update settings: {response.StatusCode}");

    return $"Integration handler {handlerId} settings updated successfully.";
});

// AI Integration specific tools
server.RegisterTool("GetAIModels", async (args) =>
{
    var handlerId = args["handlerId"]?.Value<int>() ?? throw new ArgumentException("handlerId required");

    var response = await server.Http.GetAsync($"/api/integrations/ai/{handlerId}/models");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get AI models: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var models = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"AI Models ({models.Count}):");
    sb.AppendLine();

    foreach (var model in models)
    {
        sb.AppendLine($"@aiModel[{model["id"]}] {{");
        sb.AppendLine($"  name: \"{model["name"]}\"");
        if (model["description"] != null)
            sb.AppendLine($"  description: \"{model["description"]}\"");
        if (model["contextWindow"] != null)
            sb.AppendLine($"  contextWindow: {model["contextWindow"]}");
        if (model["maxTokens"] != null)
            sb.AppendLine($"  maxTokens: {model["maxTokens"]}");
        if (model["inputPricePerMillion"] != null)
            sb.AppendLine($"  inputPrice: ${model["inputPricePerMillion"]}/M tokens");
        if (model["outputPricePerMillion"] != null)
            sb.AppendLine($"  outputPrice: ${model["outputPricePerMillion"]}/M tokens");
        sb.AppendLine($"  supportsVision: {model["supportsVision"] ?? false}");
        sb.AppendLine($"  supportsTools: {model["supportsTools"] ?? false}");
        sb.AppendLine($"  supportsStreaming: {model["supportsStreaming"] ?? false}");
        sb.AppendLine($"}}");
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetAIUsageQuota", async (args) =>
{
    var handlerId = args["handlerId"]?.Value<int>() ?? throw new ArgumentException("handlerId required");

    var response = await server.Http.GetAsync($"/api/integrations/ai/{handlerId}/usage");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get AI usage: {response.StatusCode}");

    var usage = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"@aiUsage[{handlerId}] {{");
    sb.AppendLine($"  provider: \"{usage["provider"]}\"");
    if (usage["currentBalance"] != null)
        sb.AppendLine($"  currentBalance: ${usage["currentBalance"]:F2}");
    if (usage["spentThisMonth"] != null)
        sb.AppendLine($"  spentThisMonth: ${usage["spentThisMonth"]:F2}");
    if (usage["tokensUsedThisMonth"] != null)
        sb.AppendLine($"  tokensUsedThisMonth: {usage["tokensUsedThisMonth"]:N0}");
    if (usage["rateLimitRequestsPerMinute"] != null)
        sb.AppendLine($"  rateLimitRPM: {usage["rateLimitRequestsPerMinute"]}");
    if (usage["rateLimitTokensPerMinute"] != null)
        sb.AppendLine($"  rateLimitTPM: {usage["rateLimitTokensPerMinute"]}");
    if (usage["billingCycleStart"] != null)
        sb.AppendLine($"  billingPeriod: {usage["billingCycleStart"]} - {usage["billingCycleEnd"]}");
    sb.AppendLine($"}}");
    return sb.ToString();
});

// ----------------------------------------------------------------------
// Infrastructure Management Tools (Proxmox, Kubernetes, Docker, etc.)
// ----------------------------------------------------------------------

server.RegisterTool("GetInfrastructureConnections", async (args) =>
{
    var integrationType = args["integrationType"]?.Value<string>();
    var onlyActive = args["onlyActive"]?.Value<bool>() ?? true;

    var url = "/api/infrastructure/connections";
    var queryParams = new List<string>();
    if (!string.IsNullOrEmpty(integrationType)) queryParams.Add($"integrationType={Uri.EscapeDataString(integrationType)}");
    if (onlyActive) queryParams.Add("onlyActive=true");
    if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get infrastructure connections: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var connections = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"Infrastructure Connections ({connections.Count}):");
    sb.AppendLine();

    var grouped = connections.GroupBy(c => c["integrationType"]?.ToString() ?? "unknown");
    foreach (var group in grouped)
    {
        sb.AppendLine($"## {group.Key.ToUpper()}");
        foreach (var conn in group)
        {
            var statusIcon = conn["isConnected"]?.Value<bool>() == true ? "✓" : "✗";
            sb.AppendLine($"  @infraConnection[{conn["id"]}] {{");
            sb.AppendLine($"    name: \"{conn["name"]}\"");
            sb.AppendLine($"    slug: \"{conn["slug"]}\"");
            sb.AppendLine($"    endpoint: \"{conn["endpoint"]}:{conn["port"]}\"");
            sb.AppendLine($"    status: {statusIcon} {(conn["isConnected"]?.Value<bool>() == true ? "Connected" : "Disconnected")}");
            sb.AppendLine($"    environment: \"{conn["environment"] ?? ""}\"");
            if (conn["lastConnectedAt"] != null)
                sb.AppendLine($"    lastConnected: \"{conn["lastConnectedAt"]}\"");
            if (conn["lastError"] != null)
                sb.AppendLine($"    lastError: \"{conn["lastError"]}\"");
            sb.AppendLine($"  }}");
        }
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetInfrastructureConnectionDetails", async (args) =>
{
    var connectionId = args["connectionId"]?.Value<int>() ?? throw new ArgumentException("connectionId required");

    var response = await server.Http.GetAsync($"/api/infrastructure/connections/{connectionId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get connection details: {response.StatusCode}");

    var conn = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    var statusIcon = conn["isConnected"]?.Value<bool>() == true ? "✓" : "✗";
    sb.AppendLine($"@infraConnection[{conn["id"]}] {{");
    sb.AppendLine($"  name: \"{conn["name"]}\"");
    sb.AppendLine($"  slug: \"{conn["slug"]}\"");
    sb.AppendLine($"  integrationType: \"{conn["integrationType"]}\"");
    sb.AppendLine($"  description: \"{conn["description"] ?? ""}\"");
    sb.AppendLine($"  environment: \"{conn["environment"] ?? ""}\"");
    sb.AppendLine();
    sb.AppendLine($"  ## Connection Settings");
    sb.AppendLine($"  endpoint: \"{conn["endpoint"]}\"");
    sb.AppendLine($"  port: {conn["port"] ?? "default"}");
    sb.AppendLine($"  useSsl: {conn["useSsl"] ?? true}");
    sb.AppendLine($"  verifySsl: {conn["verifySsl"] ?? true}");
    sb.AppendLine($"  authType: \"{conn["authType"] ?? ""}\"");
    sb.AppendLine();
    sb.AppendLine($"  ## Status");
    sb.AppendLine($"  status: {statusIcon} {conn["status"] ?? "unknown"}");
    sb.AppendLine($"  isActive: {conn["isActive"] ?? false}");
    sb.AppendLine($"  isConnected: {conn["isConnected"] ?? false}");
    sb.AppendLine($"  lastConnectedAt: \"{conn["lastConnectedAt"] ?? "never"}\"");
    sb.AppendLine($"  lastSyncAt: \"{conn["lastSyncAt"] ?? "never"}\"");
    if (conn["lastError"] != null)
    {
        sb.AppendLine($"  lastError: \"{conn["lastError"]}\"");
        sb.AppendLine($"  lastErrorAt: \"{conn["lastErrorAt"]}\"");
    }
    sb.AppendLine();
    sb.AppendLine($"  ## Sync Settings");
    sb.AppendLine($"  syncIntervalSeconds: {conn["syncIntervalSeconds"] ?? 60}");
    sb.AppendLine($"  enableRealtime: {conn["enableRealtime"] ?? false}");
    sb.AppendLine($"  systemVersion: \"{conn["systemVersion"] ?? ""}\"");
    sb.AppendLine();

    // Configuration and metadata
    if (conn["enabledFeatures"] != null)
    {
        var features = conn["enabledFeatures"] as JArray ?? new JArray();
        sb.AppendLine($"  enabledFeatures: [{string.Join(", ", features.Select(f => f.ToString()))}]");
    }
    if (conn["capabilities"] != null)
    {
        var caps = conn["capabilities"] as JArray ?? new JArray();
        sb.AppendLine($"  capabilities: [{string.Join(", ", caps.Select(c => c.ToString()))}]");
    }
    if (conn["tags"] != null)
    {
        var tags = conn["tags"] as JArray ?? new JArray();
        sb.AppendLine($"  tags: [{string.Join(", ", tags.Select(t => t.ToString()))}]");
    }
    sb.AppendLine($"}}");
    return sb.ToString();
});

server.RegisterTool("TestInfrastructureConnection", async (args) =>
{
    var connectionId = args["connectionId"]?.Value<int>() ?? throw new ArgumentException("connectionId required");

    var response = await server.Http.PostAsJsonAsync($"/api/infrastructure/connections/{connectionId}/test",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to test connection: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"Infrastructure Connection Test Result:");
    sb.AppendLine($"  Success: {(result["success"]?.Value<bool>() == true ? "✓ Yes" : "✗ No")}");
    sb.AppendLine($"  Message: {result["message"] ?? "No message"}");
    if (result["latencyMs"] != null)
        sb.AppendLine($"  Latency: {result["latencyMs"]}ms");
    if (result["systemVersion"] != null)
        sb.AppendLine($"  System Version: {result["systemVersion"]}");
    if (result["nodeCount"] != null)
        sb.AppendLine($"  Nodes: {result["nodeCount"]}");
    if (result["vmCount"] != null)
        sb.AppendLine($"  VMs: {result["vmCount"]}");
    if (result["containerCount"] != null)
        sb.AppendLine($"  Containers: {result["containerCount"]}");
    return sb.ToString();
});

server.RegisterTool("SyncInfrastructureResources", async (args) =>
{
    var connectionId = args["connectionId"]?.Value<int>() ?? throw new ArgumentException("connectionId required");
    var fullSync = args["fullSync"]?.Value<bool>() ?? false;

    var request = new JObject { ["fullSync"] = fullSync };
    var response = await server.Http.PostAsJsonAsync($"/api/infrastructure/connections/{connectionId}/sync",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to sync resources: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"Infrastructure Sync Result:");
    sb.AppendLine($"  Success: {(result["success"]?.Value<bool>() == true ? "✓ Yes" : "✗ No")}");
    sb.AppendLine($"  Total Resources: {result["totalResources"] ?? 0}");
    sb.AppendLine($"  Added: {result["added"] ?? 0}");
    sb.AppendLine($"  Updated: {result["updated"] ?? 0}");
    sb.AppendLine($"  Removed: {result["removed"] ?? 0}");
    sb.AppendLine($"  Duration: {result["durationMs"] ?? 0}ms");
    return sb.ToString();
});

server.RegisterTool("GetInfrastructureResources", async (args) =>
{
    var connectionId = args["connectionId"]?.Value<int>() ?? throw new ArgumentException("connectionId required");
    var resourceType = args["resourceType"]?.Value<string>();
    var status = args["status"]?.Value<string>();
    var limit = args["limit"]?.Value<int>() ?? 50;

    var url = $"/api/infrastructure/connections/{connectionId}/resources";
    var queryParams = new List<string> { $"limit={limit}" };
    if (!string.IsNullOrEmpty(resourceType)) queryParams.Add($"resourceType={Uri.EscapeDataString(resourceType)}");
    if (!string.IsNullOrEmpty(status)) queryParams.Add($"status={Uri.EscapeDataString(status)}");
    url += "?" + string.Join("&", queryParams);

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get resources: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var resources = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"Infrastructure Resources ({resources.Count}):");
    sb.AppendLine();

    var grouped = resources.GroupBy(r => r["resourceType"]?.ToString() ?? "unknown");
    foreach (var group in grouped)
    {
        sb.AppendLine($"## {group.Key}s");
        foreach (var res in group)
        {
            var statusIcon = res["status"]?.ToString() == "running" ? "▶" :
                             res["status"]?.ToString() == "stopped" ? "⏹" :
                             res["status"]?.ToString() == "paused" ? "⏸" : "?";

            sb.AppendLine($"  @infraResource[{res["id"]}] {{");
            sb.AppendLine($"    externalId: \"{res["externalId"]}\"");
            sb.AppendLine($"    name: \"{res["name"]}\"");
            sb.AppendLine($"    status: {statusIcon} {res["status"]}");

            // Resource specs
            if (res["cpuCores"] != null)
                sb.AppendLine($"    cpu: {res["cpuCores"]} cores");
            if (res["memoryBytes"] != null)
            {
                var memGb = (res["memoryBytes"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0;
                sb.AppendLine($"    memory: {memGb:F1} GB");
            }
            if (res["storageBytes"] != null)
            {
                var storGb = (res["storageBytes"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0;
                sb.AppendLine($"    storage: {storGb:F1} GB");
            }

            // Resource usage
            if (res["cpuUsage"] != null)
                sb.AppendLine($"    cpuUsage: {res["cpuUsage"]:F1}%");
            if (res["memoryUsed"] != null && res["memoryBytes"] != null)
            {
                var memUsedGb = (res["memoryUsed"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0;
                var memTotalGb = (res["memoryBytes"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0;
                sb.AppendLine($"    memoryUsed: {memUsedGb:F1}/{memTotalGb:F1} GB");
            }

            if (res["hostname"] != null)
                sb.AppendLine($"    hostname: \"{res["hostname"]}\"");
            if (res["uptime"] != null)
            {
                var uptime = TimeSpan.FromSeconds(res["uptime"]?.Value<long>() ?? 0);
                sb.AppendLine($"    uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m");
            }
            if (res["projectId"] != null)
                sb.AppendLine($"    projectId: {res["projectId"]}");
            if (res["customerId"] != null)
                sb.AppendLine($"    customerId: {res["customerId"]}");

            sb.AppendLine($"  }}");
        }
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetInfrastructureResourceDetails", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");

    var response = await server.Http.GetAsync($"/api/infrastructure/resources/{resourceId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get resource details: {response.StatusCode}");

    var res = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    var statusIcon = res["status"]?.ToString() == "running" ? "▶" :
                     res["status"]?.ToString() == "stopped" ? "⏹" :
                     res["status"]?.ToString() == "paused" ? "⏸" : "?";

    sb.AppendLine($"@infraResource[{res["id"]}] {{");
    sb.AppendLine($"  externalId: \"{res["externalId"]}\"");
    sb.AppendLine($"  name: \"{res["name"]}\"");
    sb.AppendLine($"  resourceType: \"{res["resourceType"]}\"");
    sb.AppendLine($"  status: {statusIcon} {res["status"]}");
    sb.AppendLine($"  isTemplate: {res["isTemplate"] ?? false}");
    sb.AppendLine();

    sb.AppendLine($"  ## Specifications");
    if (res["cpuCores"] != null)
        sb.AppendLine($"  cpuCores: {res["cpuCores"]}");
    if (res["memoryBytes"] != null)
    {
        var memGb = (res["memoryBytes"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0;
        sb.AppendLine($"  memory: {memGb:F2} GB");
    }
    if (res["storageBytes"] != null)
    {
        var storGb = (res["storageBytes"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0;
        sb.AppendLine($"  storage: {storGb:F2} GB");
    }
    sb.AppendLine();

    sb.AppendLine($"  ## Current Usage");
    if (res["cpuUsage"] != null)
        sb.AppendLine($"  cpuUsage: {res["cpuUsage"]:F1}%");
    if (res["memoryUsed"] != null)
    {
        var memUsedGb = (res["memoryUsed"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0;
        sb.AppendLine($"  memoryUsed: {memUsedGb:F2} GB");
    }
    if (res["storageUsed"] != null)
    {
        var storUsedGb = (res["storageUsed"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0;
        sb.AppendLine($"  storageUsed: {storUsedGb:F2} GB");
    }
    if (res["networkIn"] != null)
    {
        var netInMb = (res["networkIn"]?.Value<long>() ?? 0) / 1024.0 / 1024.0;
        sb.AppendLine($"  networkIn: {netInMb:F2} MB");
    }
    if (res["networkOut"] != null)
    {
        var netOutMb = (res["networkOut"]?.Value<long>() ?? 0) / 1024.0 / 1024.0;
        sb.AppendLine($"  networkOut: {netOutMb:F2} MB");
    }
    if (res["uptime"] != null)
    {
        var uptime = TimeSpan.FromSeconds(res["uptime"]?.Value<long>() ?? 0);
        sb.AppendLine($"  uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s");
    }
    sb.AppendLine();

    sb.AppendLine($"  ## Network");
    if (res["hostname"] != null)
        sb.AppendLine($"  hostname: \"{res["hostname"]}\"");
    if (res["ipAddresses"] != null)
    {
        var ips = res["ipAddresses"] as JArray ?? new JArray();
        sb.AppendLine($"  ipAddresses: [{string.Join(", ", ips.Select(ip => ip.ToString()))}]");
    }
    sb.AppendLine();

    sb.AppendLine($"  ## Associations");
    if (res["connectionId"] != null)
        sb.AppendLine($"  connectionId: {res["connectionId"]}");
    if (res["parentId"] != null)
        sb.AppendLine($"  parentId: {res["parentId"]}");
    if (res["projectId"] != null)
        sb.AppendLine($"  projectId: {res["projectId"]}");
    if (res["customerId"] != null)
        sb.AppendLine($"  customerId: {res["customerId"]}");
    if (res["costCenter"] != null)
        sb.AppendLine($"  costCenter: \"{res["costCenter"]}\"");
    if (res["monthlyCost"] != null)
        sb.AppendLine($"  monthlyCost: ${res["monthlyCost"]:F2}");
    sb.AppendLine();

    if (res["tags"] != null || res["labels"] != null)
    {
        sb.AppendLine($"  ## Tags & Labels");
        if (res["tags"] != null)
        {
            var tags = res["tags"] as JArray ?? new JArray();
            sb.AppendLine($"  tags: [{string.Join(", ", tags.Select(t => t.ToString()))}]");
        }
        if (res["labels"] != null)
        {
            var labels = res["labels"] as JObject ?? new JObject();
            foreach (var prop in labels.Properties())
            {
                sb.AppendLine($"  {prop.Name}: \"{prop.Value}\"");
            }
        }
    }

    sb.AppendLine($"  lastSyncAt: \"{res["lastSyncAt"] ?? "never"}\"");
    sb.AppendLine($"}}");
    return sb.ToString();
});

server.RegisterTool("StartInfrastructureResource", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");

    var response = await server.Http.PostAsJsonAsync($"/api/infrastructure/resources/{resourceId}/start",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to start resource: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    return $"▶ Resource {resourceId} start initiated. Task ID: {result["taskId"] ?? "N/A"}";
});

server.RegisterTool("StopInfrastructureResource", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");
    var force = args["force"]?.Value<bool>() ?? false;

    var request = new JObject { ["force"] = force };
    var response = await server.Http.PostAsJsonAsync($"/api/infrastructure/resources/{resourceId}/stop",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to stop resource: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    return $"⏹ Resource {resourceId} stop initiated{(force ? " (forced)" : "")}. Task ID: {result["taskId"] ?? "N/A"}";
});

server.RegisterTool("RebootInfrastructureResource", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");

    var response = await server.Http.PostAsJsonAsync($"/api/infrastructure/resources/{resourceId}/reboot",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to reboot resource: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    return $"🔄 Resource {resourceId} reboot initiated. Task ID: {result["taskId"] ?? "N/A"}";
});

server.RegisterTool("GetInfrastructureResourceMetrics", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");
    var startDate = args["startDate"]?.Value<DateTime?>() ?? DateTime.UtcNow.AddHours(-24);
    var endDate = args["endDate"]?.Value<DateTime?>() ?? DateTime.UtcNow;
    var resolution = args["resolution"]?.Value<string>() ?? "5m";

    var url = $"/api/infrastructure/resources/{resourceId}/metrics";
    url += $"?startDate={startDate:yyyy-MM-ddTHH:mm:ss}Z&endDate={endDate:yyyy-MM-ddTHH:mm:ss}Z&resolution={resolution}";

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get metrics: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var metrics = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"Resource Metrics ({startDate:HH:mm} - {endDate:HH:mm}, {resolution} intervals):");
    sb.AppendLine();

    // Summary stats
    if (metrics.Count > 0)
    {
        var avgCpu = metrics.Average(m => m["cpuUsage"]?.Value<decimal?>() ?? 0);
        var maxCpu = metrics.Max(m => m["cpuUsage"]?.Value<decimal?>() ?? 0);
        var avgMem = metrics.Average(m => m["memoryUsed"]?.Value<long?>() ?? 0);
        var maxMem = metrics.Max(m => m["memoryUsed"]?.Value<long?>() ?? 0);

        sb.AppendLine($"## Summary ({metrics.Count} data points)");
        sb.AppendLine($"  CPU: avg {avgCpu:F1}%, max {maxCpu:F1}%");
        sb.AppendLine($"  Memory: avg {avgMem / 1024.0 / 1024.0 / 1024.0:F2} GB, max {maxMem / 1024.0 / 1024.0 / 1024.0:F2} GB");
        sb.AppendLine();

        sb.AppendLine($"## Recent Data Points (last 10)");
        foreach (var m in metrics.TakeLast(10))
        {
            var ts = DateTime.Parse(m["timestamp"]?.ToString() ?? DateTime.UtcNow.ToString());
            sb.AppendLine($"  [{ts:HH:mm}] CPU: {m["cpuUsage"] ?? 0:F1}% | " +
                         $"Mem: {(m["memoryUsed"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0:F2} GB | " +
                         $"Net In/Out: {(m["networkIn"]?.Value<long>() ?? 0) / 1024.0:F0}/{(m["networkOut"]?.Value<long>() ?? 0) / 1024.0:F0} KB");
        }
    }
    else
    {
        sb.AppendLine("No metrics data available for the specified period.");
    }
    return sb.ToString();
});

server.RegisterTool("CreateInfrastructureSnapshot", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");
    var name = args["name"]?.Value<string>() ?? $"snapshot-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
    var description = args["description"]?.Value<string>();
    var includeMemory = args["includeMemory"]?.Value<bool>() ?? false;

    var request = new JObject
    {
        ["name"] = name,
        ["description"] = description,
        ["includeMemory"] = includeMemory
    };

    var response = await server.Http.PostAsJsonAsync($"/api/infrastructure/resources/{resourceId}/snapshots",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to create snapshot: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    return $"📸 Snapshot created: {name}. Task ID: {result["taskId"] ?? "N/A"}";
});

server.RegisterTool("GetInfrastructureSnapshots", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");

    var response = await server.Http.GetAsync($"/api/infrastructure/resources/{resourceId}/snapshots");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get snapshots: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var snapshots = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"Snapshots for Resource {resourceId} ({snapshots.Count}):");
    sb.AppendLine();

    foreach (var snap in snapshots)
    {
        sb.AppendLine($"@snapshot[{snap["name"]}] {{");
        sb.AppendLine($"  description: \"{snap["description"] ?? ""}\"");
        sb.AppendLine($"  createdAt: \"{snap["createdAt"]}\"");
        if (snap["sizeBytes"] != null)
        {
            var sizeMb = (snap["sizeBytes"]?.Value<long>() ?? 0) / 1024.0 / 1024.0;
            sb.AppendLine($"  size: {sizeMb:F2} MB");
        }
        sb.AppendLine($"  includesMemory: {snap["includesMemory"] ?? false}");
        sb.AppendLine($"}}");
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("RestoreInfrastructureSnapshot", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");
    var snapshotName = args["snapshotName"]?.Value<string>() ?? throw new ArgumentException("snapshotName required");
    var startAfterRestore = args["startAfterRestore"]?.Value<bool>() ?? true;

    var request = new JObject
    {
        ["snapshotName"] = snapshotName,
        ["startAfterRestore"] = startAfterRestore
    };

    var response = await server.Http.PostAsJsonAsync($"/api/infrastructure/resources/{resourceId}/snapshots/restore",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to restore snapshot: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    return $"⏪ Restoring snapshot '{snapshotName}' for resource {resourceId}. Task ID: {result["taskId"] ?? "N/A"}";
});

server.RegisterTool("LinkResourceToProject", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");
    var projectId = args["projectId"]?.Value<int>() ?? throw new ArgumentException("projectId required");

    var request = new JObject { ["projectId"] = projectId };
    var response = await server.Http.PostAsJsonAsync($"/api/infrastructure/resources/{resourceId}/link",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to link resource: {response.StatusCode}");

    return $"Resource {resourceId} linked to project {projectId}.";
});

server.RegisterTool("LinkResourceToCustomer", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");
    var customerId = args["customerId"]?.Value<int>() ?? throw new ArgumentException("customerId required");
    var costCenter = args["costCenter"]?.Value<string>();
    var monthlyCost = args["monthlyCost"]?.Value<decimal?>();

    var request = new JObject
    {
        ["customerId"] = customerId,
        ["costCenter"] = costCenter,
        ["monthlyCost"] = monthlyCost
    };

    var response = await server.Http.PostAsJsonAsync($"/api/infrastructure/resources/{resourceId}/link",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to link resource: {response.StatusCode}");

    return $"Resource {resourceId} linked to customer {customerId}" +
           (costCenter != null ? $" (cost center: {costCenter})" : "") +
           (monthlyCost.HasValue ? $" (monthly cost: ${monthlyCost:F2})" : "") + ".";
});

server.RegisterTool("GetInfrastructureSummary", async (args) =>
{
    var connectionId = args["connectionId"]?.Value<int?>();

    var url = connectionId.HasValue
        ? $"/api/infrastructure/connections/{connectionId}/summary"
        : "/api/infrastructure/summary";

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get infrastructure summary: {response.StatusCode}");

    var summary = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine("=== Infrastructure Summary ===");
    sb.AppendLine();

    // Overall stats
    sb.AppendLine($"## Overview");
    sb.AppendLine($"  Connections: {summary["totalConnections"] ?? 0} ({summary["connectedCount"] ?? 0} connected)");
    sb.AppendLine($"  Total Resources: {summary["totalResources"] ?? 0}");
    sb.AppendLine();

    // Resource breakdown by type
    var byType = summary["resourcesByType"] as JObject ?? new JObject();
    if (byType.Count > 0)
    {
        sb.AppendLine($"## Resources by Type");
        foreach (var prop in byType.Properties())
        {
            sb.AppendLine($"  {prop.Name}: {prop.Value}");
        }
        sb.AppendLine();
    }

    // Status breakdown
    var byStatus = summary["resourcesByStatus"] as JObject ?? new JObject();
    if (byStatus.Count > 0)
    {
        sb.AppendLine($"## Resources by Status");
        foreach (var prop in byStatus.Properties())
        {
            var icon = prop.Name == "running" ? "▶" :
                       prop.Name == "stopped" ? "⏹" :
                       prop.Name == "paused" ? "⏸" : "?";
            sb.AppendLine($"  {icon} {prop.Name}: {prop.Value}");
        }
        sb.AppendLine();
    }

    // Total resource usage
    if (summary["totalCpuCores"] != null || summary["totalMemoryBytes"] != null)
    {
        sb.AppendLine($"## Total Allocated Resources");
        if (summary["totalCpuCores"] != null)
            sb.AppendLine($"  CPU: {summary["totalCpuCores"]} cores");
        if (summary["totalMemoryBytes"] != null)
        {
            var memGb = (summary["totalMemoryBytes"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0;
            sb.AppendLine($"  Memory: {memGb:F1} GB");
        }
        if (summary["totalStorageBytes"] != null)
        {
            var storTb = (summary["totalStorageBytes"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0 / 1024.0;
            sb.AppendLine($"  Storage: {storTb:F2} TB");
        }
        sb.AppendLine();
    }

    // Cost summary
    if (summary["totalMonthlyCost"] != null)
    {
        sb.AppendLine($"## Cost Summary");
        sb.AppendLine($"  Total Monthly Cost: ${summary["totalMonthlyCost"]:F2}");
        if (summary["linkedToProjects"] != null)
            sb.AppendLine($"  Linked to Projects: {summary["linkedToProjects"]}");
        if (summary["linkedToCustomers"] != null)
            sb.AppendLine($"  Linked to Customers: {summary["linkedToCustomers"]}");
    }

    return sb.ToString();
});

// ----------------------------------------------------------------------
// Cluster Health Monitoring Tools (Forge Lite)
// ----------------------------------------------------------------------

server.RegisterTool("GetClusterHealth", async (args) =>
{
    var includeDetails = args["includeDetails"]?.Value<bool>() ?? false;

    // Get all infrastructure connections and check their status
    var connectionsResponse = await server.Http.GetAsync("/api/infrastructure/connections?onlyActive=true");
    if (!connectionsResponse.IsSuccessStatusCode)
        throw new Exception($"Failed to get infrastructure status: {connectionsResponse.StatusCode}");

    var connectionsResult = JObject.Parse(await connectionsResponse.Content.ReadAsStringAsync());
    var connections = connectionsResult["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine("# Cluster Health Dashboard");
    sb.AppendLine($"Last checked: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
    sb.AppendLine();

    // Summary counts
    var totalConnections = connections.Count;
    var healthyConnections = connections.Count(c => c["isConnected"]?.Value<bool>() == true);
    var unhealthyConnections = totalConnections - healthyConnections;

    var overallStatus = unhealthyConnections == 0 ? "✅ HEALTHY" :
                        unhealthyConnections <= 2 ? "⚠️ DEGRADED" : "❌ CRITICAL";

    sb.AppendLine($"## Overall Status: {overallStatus}");
    sb.AppendLine($"- Healthy: {healthyConnections}/{totalConnections}");
    sb.AppendLine($"- Unhealthy: {unhealthyConnections}");
    sb.AppendLine();

    // Group by type
    var grouped = connections.GroupBy(c => c["integrationType"]?.ToString() ?? "unknown");

    foreach (var group in grouped.OrderBy(g => g.Key))
    {
        var groupHealthy = group.Count(c => c["isConnected"]?.Value<bool>() == true);
        var groupTotal = group.Count();
        var groupIcon = groupHealthy == groupTotal ? "✅" : groupHealthy == 0 ? "❌" : "⚠️";

        sb.AppendLine($"### {groupIcon} {group.Key.ToUpper()} ({groupHealthy}/{groupTotal})");

        if (includeDetails)
        {
            foreach (var conn in group)
            {
                var statusIcon = conn["isConnected"]?.Value<bool>() == true ? "✓" : "✗";
                sb.AppendLine($"  - {statusIcon} {conn["name"]} ({conn["endpoint"]})");
                if (conn["lastError"] != null && conn["isConnected"]?.Value<bool>() != true)
                    sb.AppendLine($"    Error: {conn["lastError"]}");
            }
        }
        sb.AppendLine();
    }

    // Alerts section
    var unhealthy = connections.Where(c => c["isConnected"]?.Value<bool>() != true).ToList();
    if (unhealthy.Count > 0)
    {
        sb.AppendLine("## ⚠️ Active Alerts");
        foreach (var conn in unhealthy)
        {
            sb.AppendLine($"- [{conn["integrationType"]}] {conn["name"]}: {conn["lastError"] ?? "Disconnected"}");
        }
    }

    return sb.ToString();
});

server.RegisterTool("GetKubernetesHealth", async (args) =>
{
    var connectionSlug = args["connectionSlug"]?.Value<string>() ?? "kubernetes-eden";
    var namespaces = args["namespaces"]?.ToObject<List<string>>() ?? new List<string> { "served", "served-redis", "served-kafka" };

    // Get connection by slug
    var connResponse = await server.Http.GetAsync($"/api/infrastructure/connections/slug/{connectionSlug}");
    if (!connResponse.IsSuccessStatusCode)
        throw new Exception($"Kubernetes connection '{connectionSlug}' not found");

    var conn = JObject.Parse(await connResponse.Content.ReadAsStringAsync());
    var connectionId = conn["id"]?.Value<int>() ?? throw new Exception("Invalid connection");

    var sb = new StringBuilder();
    sb.AppendLine($"# Kubernetes Health: {connectionSlug}");
    sb.AppendLine($"Endpoint: {conn["endpoint"]}");
    sb.AppendLine($"Version: {conn["systemVersion"] ?? "Unknown"}");
    sb.AppendLine();

    // Get resources for each namespace
    foreach (var ns in namespaces)
    {
        try
        {
            var resourcesResponse = await server.Http.GetAsync(
                $"/api/infrastructure/connections/{connectionId}/kubernetes/namespaces/{ns}/pods");

            if (resourcesResponse.IsSuccessStatusCode)
            {
                var podsResult = JObject.Parse(await resourcesResponse.Content.ReadAsStringAsync());
                var pods = podsResult["data"] as JArray ?? new JArray();

                var runningPods = pods.Count(p => p["status"]?.ToString() == "Running");
                var totalPods = pods.Count;
                var nsIcon = runningPods == totalPods ? "✅" : runningPods > 0 ? "⚠️" : "❌";

                sb.AppendLine($"## {nsIcon} Namespace: {ns}");
                sb.AppendLine($"Pods: {runningPods}/{totalPods} running");
                sb.AppendLine();

                foreach (var pod in pods)
                {
                    var podStatus = pod["status"]?.ToString() ?? "Unknown";
                    var podIcon = podStatus == "Running" ? "✓" : podStatus == "Pending" ? "◐" : "✗";
                    var restarts = pod["restartCount"]?.Value<int>() ?? 0;
                    var restartWarning = restarts > 5 ? $" ⚠️ {restarts} restarts" : "";

                    sb.AppendLine($"  {podIcon} {pod["name"]} ({podStatus}){restartWarning}");

                    if (podStatus != "Running" && pod["message"] != null)
                        sb.AppendLine($"    Reason: {pod["message"]}");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine($"## ❌ Namespace: {ns}");
                sb.AppendLine("  Failed to fetch pods");
                sb.AppendLine();
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"## ❌ Namespace: {ns}");
            sb.AppendLine($"  Error: {ex.Message}");
            sb.AppendLine();
        }
    }

    return sb.ToString();
});

server.RegisterTool("GetDatabaseHealth", async (args) =>
{
    var sb = new StringBuilder();
    sb.AppendLine("# Database Health");
    sb.AppendLine($"Checked: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
    sb.AppendLine();

    // Get all database connections
    var connResponse = await server.Http.GetAsync("/api/infrastructure/connections?onlyActive=true");
    if (!connResponse.IsSuccessStatusCode)
        throw new Exception($"Failed to get connections: {connResponse.StatusCode}");

    var connResult = JObject.Parse(await connResponse.Content.ReadAsStringAsync());
    var connections = connResult["data"] as JArray ?? new JArray();

    var dbTypes = new[] { "mysql", "postgresql", "mssql", "redis" };
    var dbConnections = connections.Where(c => dbTypes.Contains(c["integrationType"]?.ToString()?.ToLower())).ToList();

    foreach (var db in dbConnections)
    {
        var isConnected = db["isConnected"]?.Value<bool>() == true;
        var statusIcon = isConnected ? "✅" : "❌";
        var dbType = db["integrationType"]?.ToString()?.ToUpper() ?? "UNKNOWN";

        sb.AppendLine($"## {statusIcon} {db["name"]} ({dbType})");
        sb.AppendLine($"  Endpoint: {db["endpoint"]}:{db["port"]}");
        sb.AppendLine($"  Status: {(isConnected ? "Connected" : "Disconnected")}");

        if (db["lastConnectedAt"] != null)
            sb.AppendLine($"  Last Connected: {db["lastConnectedAt"]}");

        if (!isConnected && db["lastError"] != null)
            sb.AppendLine($"  Error: {db["lastError"]}");

        // Get database-specific metrics if connected
        if (isConnected)
        {
            var dbId = db["id"]?.Value<int>();
            try
            {
                var metricsResponse = await server.Http.GetAsync($"/api/infrastructure/connections/{dbId}/status");
                if (metricsResponse.IsSuccessStatusCode)
                {
                    var metrics = JObject.Parse(await metricsResponse.Content.ReadAsStringAsync());
                    if (metrics["activeConnections"] != null)
                        sb.AppendLine($"  Active Connections: {metrics["activeConnections"]}");
                    if (metrics["uptime"] != null)
                        sb.AppendLine($"  Uptime: {metrics["uptime"]}");
                    if (metrics["version"] != null)
                        sb.AppendLine($"  Version: {metrics["version"]}");
                }
            }
            catch { /* Ignore metric fetch errors */ }
        }

        sb.AppendLine();
    }

    if (dbConnections.Count == 0)
    {
        sb.AppendLine("No database connections configured.");
    }

    return sb.ToString();
});

server.RegisterTool("GetProxmoxHealth", async (args) =>
{
    var connectionSlug = args["connectionSlug"]?.Value<string>() ?? "proxmox-eden";

    // Get connection by slug
    var connResponse = await server.Http.GetAsync($"/api/infrastructure/connections/slug/{connectionSlug}");
    if (!connResponse.IsSuccessStatusCode)
        throw new Exception($"Proxmox connection '{connectionSlug}' not found");

    var conn = JObject.Parse(await connResponse.Content.ReadAsStringAsync());
    var connectionId = conn["id"]?.Value<int>() ?? throw new Exception("Invalid connection");

    var sb = new StringBuilder();
    sb.AppendLine($"# Proxmox Health: {connectionSlug}");
    sb.AppendLine($"Endpoint: {conn["endpoint"]}");
    sb.AppendLine($"Status: {(conn["isConnected"]?.Value<bool>() == true ? "✅ Connected" : "❌ Disconnected")}");
    sb.AppendLine();

    // Get VMs
    try
    {
        var vmsResponse = await server.Http.GetAsync($"/api/infrastructure/connections/{connectionId}/proxmox/vms");
        if (vmsResponse.IsSuccessStatusCode)
        {
            var vmsResult = JObject.Parse(await vmsResponse.Content.ReadAsStringAsync());
            var vms = vmsResult["data"] as JArray ?? new JArray();

            var runningVMs = vms.Count(v => v["status"]?.ToString() == "running");
            sb.AppendLine($"## Virtual Machines ({runningVMs}/{vms.Count} running)");

            foreach (var vm in vms)
            {
                var vmStatus = vm["status"]?.ToString() ?? "unknown";
                var vmIcon = vmStatus == "running" ? "✅" : vmStatus == "stopped" ? "⭕" : "⚠️";
                var cpuUsage = vm["cpu"]?.Value<double>() * 100 ?? 0;
                var memUsage = vm["mem"]?.Value<long>() ?? 0;
                var maxMem = vm["maxmem"]?.Value<long>() ?? 1;
                var memPct = (double)memUsage / maxMem * 100;

                sb.AppendLine($"  {vmIcon} {vm["name"]} (VMID: {vm["vmid"]})");
                sb.AppendLine($"     Status: {vmStatus}");
                if (vmStatus == "running")
                {
                    sb.AppendLine($"     CPU: {cpuUsage:F1}%");
                    sb.AppendLine($"     Memory: {memPct:F1}% ({memUsage / 1024 / 1024 / 1024:F1} GB)");
                }
            }
            sb.AppendLine();
        }
    }
    catch (Exception ex)
    {
        sb.AppendLine($"## Virtual Machines");
        sb.AppendLine($"  Error fetching VMs: {ex.Message}");
        sb.AppendLine();
    }

    // Get LXC containers
    try
    {
        var lxcResponse = await server.Http.GetAsync($"/api/infrastructure/connections/{connectionId}/proxmox/lxc");
        if (lxcResponse.IsSuccessStatusCode)
        {
            var lxcResult = JObject.Parse(await lxcResponse.Content.ReadAsStringAsync());
            var containers = lxcResult["data"] as JArray ?? new JArray();

            var runningContainers = containers.Count(c => c["status"]?.ToString() == "running");
            sb.AppendLine($"## LXC Containers ({runningContainers}/{containers.Count} running)");

            foreach (var lxc in containers)
            {
                var lxcStatus = lxc["status"]?.ToString() ?? "unknown";
                var lxcIcon = lxcStatus == "running" ? "✅" : lxcStatus == "stopped" ? "⭕" : "⚠️";

                sb.AppendLine($"  {lxcIcon} {lxc["name"]} (VMID: {lxc["vmid"]})");
                sb.AppendLine($"     Status: {lxcStatus}");
            }
            sb.AppendLine();
        }
    }
    catch (Exception ex)
    {
        sb.AppendLine($"## LXC Containers");
        sb.AppendLine($"  Error fetching containers: {ex.Message}");
        sb.AppendLine();
    }

    return sb.ToString();
});

server.RegisterTool("GetServiceHealth", async (args) =>
{
    var serviceName = args["serviceName"]?.Value<string>();

    var sb = new StringBuilder();
    sb.AppendLine("# Service Health");
    sb.AppendLine();

    // Check API health endpoints
    var services = new Dictionary<string, string>
    {
        { "ServedAPI", "/healthz/ready" },
        { "ServedAPI-Live", "/healthz/live" }
    };

    if (serviceName != null && services.ContainsKey(serviceName))
    {
        // Check specific service
        var endpoint = services[serviceName];
        try
        {
            var response = await server.Http.GetAsync(endpoint);
            var isHealthy = response.IsSuccessStatusCode;
            sb.AppendLine($"{(isHealthy ? "✅" : "❌")} {serviceName}: {(isHealthy ? "Healthy" : "Unhealthy")}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"❌ {serviceName}: Error - {ex.Message}");
        }
    }
    else
    {
        // Check all services
        foreach (var service in services)
        {
            try
            {
                var response = await server.Http.GetAsync(service.Value);
                var isHealthy = response.IsSuccessStatusCode;
                sb.AppendLine($"{(isHealthy ? "✅" : "❌")} {service.Key}: {(isHealthy ? "Healthy" : "Unhealthy")}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ {service.Key}: Error - {ex.Message}");
            }
        }
    }

    return sb.ToString();
});

// ----------------------------------------------------------------------
// Agent Monitoring Tools (Forge Lite)
// ----------------------------------------------------------------------

server.RegisterTool("GetActiveAgents", async (args) =>
{
    var agentType = args["agentType"]?.Value<string>();

    // Query DevOps agents endpoint
    var url = "/api/devops/agents";
    if (!string.IsNullOrEmpty(agentType))
        url += $"?type={Uri.EscapeDataString(agentType)}";

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get agents: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var agents = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine("# Active Agents");
    sb.AppendLine($"Total: {agents.Count}");
    sb.AppendLine();

    // Group by type
    var grouped = agents.GroupBy(a => a["type"]?.ToString() ?? "unknown");
    foreach (var group in grouped)
    {
        sb.AppendLine($"## {group.Key.ToUpper()}");
        foreach (var agent in group)
        {
            var statusIcon = agent["status"]?.ToString() switch
            {
                "Active" or "Running" => "🟢",
                "Idle" => "🟡",
                "Paused" => "⏸️",
                "Error" => "🔴",
                _ => "⚪"
            };

            sb.AppendLine($"  {statusIcon} @agent[{agent["id"]}] \"{agent["name"]}\"");
            sb.AppendLine($"     Status: {agent["status"]}");

            if (agent["currentTask"] != null)
                sb.AppendLine($"     Task: {agent["currentTask"]}");

            if (agent["filesInUse"] is JArray files && files.Count > 0)
            {
                sb.AppendLine($"     Files: {files.Count} in use");
                foreach (var file in files.Take(3))
                    sb.AppendLine($"       - {file}");
                if (files.Count > 3)
                    sb.AppendLine($"       ... and {files.Count - 3} more");
            }

            if (agent["progress"] != null)
            {
                var progress = agent["progress"]?.Value<double>() ?? 0;
                sb.AppendLine($"     Progress: {progress:F0}%");
            }

            if (agent["lastActivity"] != null)
                sb.AppendLine($"     Last Activity: {agent["lastActivity"]}");
        }
        sb.AppendLine();
    }

    if (agents.Count == 0)
    {
        sb.AppendLine("No active agents found.");
    }

    return sb.ToString();
});

server.RegisterTool("GetAgentDetails", async (args) =>
{
    var agentId = args["agentId"]?.Value<string>() ?? throw new ArgumentException("agentId required");

    var response = await server.Http.GetAsync($"/api/devops/agents/{agentId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Agent not found: {response.StatusCode}");

    var agent = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"@agent[{agent["id"]}] {{");
    sb.AppendLine($"  name: \"{agent["name"]}\"");
    sb.AppendLine($"  type: \"{agent["type"]}\"");
    sb.AppendLine($"  status: \"{agent["status"]}\"");
    sb.AppendLine();

    if (agent["currentTask"] != null)
    {
        sb.AppendLine($"  ## Current Task");
        sb.AppendLine($"  task: \"{agent["currentTask"]}\"");
        if (agent["taskStartedAt"] != null)
            sb.AppendLine($"  startedAt: \"{agent["taskStartedAt"]}\"");
        if (agent["progress"] != null)
            sb.AppendLine($"  progress: {agent["progress"]}%");
    }

    if (agent["filesInUse"] is JArray files && files.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine($"  ## Files In Use ({files.Count})");
        foreach (var file in files)
            sb.AppendLine($"  - {file}");
    }

    if (agent["todoItems"] is JArray todos && todos.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine($"  ## Todo List ({todos.Count})");
        foreach (var todo in todos)
        {
            var statusIcon = todo["status"]?.ToString() switch
            {
                "completed" => "✅",
                "in_progress" => "🔄",
                _ => "⭕"
            };
            sb.AppendLine($"  {statusIcon} {todo["content"]}");
        }
    }

    if (agent["recentCommits"] is JArray commits && commits.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine($"  ## Recent Commits ({commits.Count})");
        foreach (var commit in commits.Take(5))
        {
            sb.AppendLine($"  - [{commit["hash"]?.ToString()?.Substring(0, 7)}] {commit["message"]}");
        }
    }

    sb.AppendLine("}");
    return sb.ToString();
});

server.RegisterTool("SendAgentTask", async (args) =>
{
    var agentId = args["agentId"]?.Value<string>() ?? throw new ArgumentException("agentId required");
    var taskDescription = args["task"]?.Value<string>() ?? throw new ArgumentException("task required");
    var priority = args["priority"]?.Value<string>() ?? "normal";

    var payload = new JObject
    {
        ["agentId"] = agentId,
        ["task"] = taskDescription,
        ["priority"] = priority
    };

    var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
    var response = await server.Http.PostAsJsonAsync("/api/devops/agents/tasks", content);

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to send task: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    return $"✅ Task sent to agent {agentId}: \"{taskDescription}\"\nTask ID: {result["taskId"]}";
});

server.RegisterTool("PauseAgent", async (args) =>
{
    var agentId = args["agentId"]?.Value<string>() ?? throw new ArgumentException("agentId required");

    var response = await server.Http.PostAsync($"/api/devops/agents/{agentId}/pause", null);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to pause agent: {response.StatusCode}");

    return $"⏸️ Agent {agentId} paused";
});

server.RegisterTool("ResumeAgent", async (args) =>
{
    var agentId = args["agentId"]?.Value<string>() ?? throw new ArgumentException("agentId required");

    var response = await server.Http.PostAsync($"/api/devops/agents/{agentId}/resume", null);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to resume agent: {response.StatusCode}");

    return $"▶️ Agent {agentId} resumed";
});

// ----------------------------------------------------------------------
// Kill Your Darlings - Agent Lifecycle Management
// "Kill your darlings" - Steve Jobs: If it's not essential, eliminate it.
// ----------------------------------------------------------------------

server.RegisterTool("KillAgent", async (args) =>
{
    var agentId = args["agentId"]?.Value<string>() ?? throw new ArgumentException("agentId required");
    var force = args["force"]?.Value<bool>() ?? false;
    var timeout = args["timeoutSeconds"]?.Value<int>() ?? 30;

    var queryParams = $"?force={force}&timeoutSeconds={timeout}";
    var response = await server.Http.PostAsync($"/api/cli-agents/Kill{queryParams}&agentId={agentId}", null);

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to kill agent: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var method = result["method"]?.Value<string>() ?? "unknown";
    var duration = result["durationMs"]?.Value<long>() ?? 0;

    return $"💀 Agent {agentId} terminated ({method}) in {duration}ms";
});

server.RegisterTool("KillStaleAgents", async (args) =>
{
    var thresholdMinutes = args["staleThresholdMinutes"]?.Value<int>() ?? 5;
    var force = args["force"]?.Value<bool>() ?? false;

    var queryParams = $"?staleThresholdMinutes={thresholdMinutes}&force={force}";
    var response = await server.Http.PostAsync($"/api/cli-agents/KillStale{queryParams}", null);

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to kill stale agents: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var killed = result["killedCount"]?.Value<int>() ?? 0;
    var failed = result["failedCount"]?.Value<int>() ?? 0;

    var sb = new StringBuilder();
    sb.AppendLine("# Kill Stale Agents Result");
    sb.AppendLine();
    sb.AppendLine($"💀 Killed: {killed} agents");
    if (failed > 0)
        sb.AppendLine($"❌ Failed: {failed} agents");
    sb.AppendLine($"📊 Threshold: {thresholdMinutes} minutes without heartbeat");

    return sb.ToString();
});

server.RegisterTool("KillOverBudgetAgents", async (args) =>
{
    var force = args["force"]?.Value<bool>() ?? false;

    var queryParams = $"?force={force}";
    var response = await server.Http.PostAsync($"/api/cli-agents/KillOverBudget{queryParams}", null);

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to kill over-budget agents: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var killed = result["killedCount"]?.Value<int>() ?? 0;

    return $"💰 Killed {killed} over-budget agents";
});

server.RegisterTool("KillAgentsByType", async (args) =>
{
    var agentType = args["agentType"]?.Value<string>() ?? throw new ArgumentException("agentType required");
    var force = args["force"]?.Value<bool>() ?? false;

    var queryParams = $"?agentType={agentType}&force={force}";
    var response = await server.Http.PostAsync($"/api/cli-agents/KillByType{queryParams}", null);

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to kill agents by type: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var killed = result["killedCount"]?.Value<int>() ?? 0;

    return $"💀 Killed {killed} {agentType} agents";
});

server.RegisterTool("GetKillStatus", async (args) =>
{
    var response = await server.Http.PostAsync("/api/cli-agents/GetKillStatus", null);

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get kill status: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine("# Kill Status - Agent Overview");
    sb.AppendLine();
    sb.AppendLine($"📊 Total Agents: {result["totalAgents"]}");
    sb.AppendLine($"✅ Active: {result["activeCount"]}");
    sb.AppendLine($"⚠️ Stale: {result["staleCount"]}");
    sb.AppendLine($"💰 Over Budget: {result["overBudgetCount"]}");
    sb.AppendLine();

    var byType = result["byType"] as JObject;
    if (byType != null && byType.Count > 0)
    {
        sb.AppendLine("## By Type:");
        foreach (var prop in byType.Properties())
        {
            sb.AppendLine($"  - {prop.Name}: {prop.Value}");
        }
        sb.AppendLine();
    }

    var agents = result["agents"] as JArray;
    if (agents != null && agents.Count > 0)
    {
        sb.AppendLine("## Agent Details:");
        foreach (var agent in agents)
        {
            var status = agent["isOnline"]?.Value<bool>() == true ? "🟢" : "🔴";
            var stale = agent["isStale"]?.Value<bool>() == true ? " [STALE]" : "";
            var overBudget = agent["isOverBudget"]?.Value<bool>() == true ? " [OVER BUDGET]" : "";
            sb.AppendLine($"  {status} {agent["name"]} ({agent["agentType"]}){stale}{overBudget}");
            sb.AppendLine($"     ID: {agent["id"]}, Cost: ${agent["totalCost"]:F2}");
        }
    }

    return sb.ToString();
});

server.RegisterTool("WakeAgent", async (args) =>
{
    var agentType = args["agentType"]?.Value<string>() ?? throw new ArgumentException("agentType required");
    var task = args["task"]?.Value<string>();
    var repository = args["repository"]?.Value<string>();

    var payload = new JObject
    {
        ["type"] = agentType
    };
    if (!string.IsNullOrEmpty(task))
        payload["initialTask"] = task;
    if (!string.IsNullOrEmpty(repository))
        payload["repository"] = repository;

    var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
    var response = await server.Http.PostAsJsonAsync("/api/devops/agents/wake", content);

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to wake agent: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    return $"🚀 Agent {agentType} woken up\nAgent ID: {result["agentId"]}";
});

server.RegisterTool("CheckAgentConflicts", async (args) =>
{
    var response = await server.Http.GetAsync("/api/devops/agents/conflicts");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to check conflicts: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var conflicts = result["conflicts"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine("# Agent File Conflicts");
    sb.AppendLine();

    if (conflicts.Count == 0)
    {
        sb.AppendLine("✅ No conflicts detected");
    }
    else
    {
        sb.AppendLine($"⚠️ {conflicts.Count} conflict(s) detected:");
        sb.AppendLine();

        foreach (var conflict in conflicts)
        {
            sb.AppendLine($"## File: {conflict["file"]}");
            var agents = conflict["agents"] as JArray ?? new JArray();
            foreach (var agent in agents)
            {
                sb.AppendLine($"  - Agent: {agent["name"]} ({agent["id"]})");
                sb.AppendLine($"    Operation: {agent["operation"]}");
            }
            sb.AppendLine();
        }
    }

    return sb.ToString();
});

// ----------------------------------------------------------------------
// Workflow Management Tools (Forge Lite)
// ----------------------------------------------------------------------

server.RegisterTool("GetWorkflows", async (args) =>
{
    var workflowType = args["type"]?.Value<string>();
    var enabled = args["enabled"]?.Value<bool?>();

    var url = "/api/automation/workflows";
    var queryParams = new List<string>();
    if (!string.IsNullOrEmpty(workflowType)) queryParams.Add($"type={Uri.EscapeDataString(workflowType)}");
    if (enabled.HasValue) queryParams.Add($"enabled={enabled.Value}");
    if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get workflows: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var workflows = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine("# Workflows");
    sb.AppendLine($"Total: {workflows.Count}");
    sb.AppendLine();

    // Group by trigger type
    var grouped = workflows.GroupBy(w => w["trigger"]?["type"]?.ToString() ?? "manual");
    foreach (var group in grouped)
    {
        var triggerIcon = group.Key switch
        {
            "schedule" => "🕐",
            "file_change" => "📁",
            "webhook" => "🌐",
            "manual" => "👆",
            _ => "⚙️"
        };

        sb.AppendLine($"## {triggerIcon} {group.Key.ToUpper()} Triggers");
        foreach (var workflow in group)
        {
            var enabledIcon = workflow["enabled"]?.Value<bool>() == true ? "✅" : "⭕";
            sb.AppendLine($"  {enabledIcon} @workflow[{workflow["id"]}] \"{workflow["name"]}\"");
            sb.AppendLine($"     {workflow["description"] ?? "No description"}");

            // Show trigger details
            var trigger = workflow["trigger"];
            if (trigger != null)
            {
                if (trigger["schedule"] != null)
                    sb.AppendLine($"     Schedule: {trigger["schedule"]["cron"]}");
                if (trigger["filePatterns"] is JArray patterns)
                    sb.AppendLine($"     Patterns: {string.Join(", ", patterns.Select(p => p.ToString()))}");
            }
        }
        sb.AppendLine();
    }

    if (workflows.Count == 0)
    {
        sb.AppendLine("No workflows configured.");
    }

    return sb.ToString();
});

server.RegisterTool("GetWorkflowDetails", async (args) =>
{
    var workflowId = args["workflowId"]?.Value<string>() ?? throw new ArgumentException("workflowId required");

    var response = await server.Http.GetAsync($"/api/automation/workflows/{workflowId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Workflow not found: {response.StatusCode}");

    var workflow = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"@workflow[{workflow["id"]}] {{");
    sb.AppendLine($"  name: \"{workflow["name"]}\"");
    sb.AppendLine($"  description: \"{workflow["description"] ?? ""}\"");
    sb.AppendLine($"  enabled: {workflow["enabled"]}");
    sb.AppendLine();

    // Trigger
    var trigger = workflow["trigger"];
    if (trigger != null)
    {
        sb.AppendLine($"  ## Trigger");
        sb.AppendLine($"  type: \"{trigger["type"]}\"");

        if (trigger["schedule"] != null)
        {
            sb.AppendLine($"  schedule:");
            sb.AppendLine($"    cron: \"{trigger["schedule"]["cron"]}\"");
            if (trigger["schedule"]["timezone"] != null)
                sb.AppendLine($"    timezone: \"{trigger["schedule"]["timezone"]}\"");
        }

        if (trigger["filePatterns"] is JArray patterns)
        {
            sb.AppendLine($"  filePatterns:");
            foreach (var p in patterns)
                sb.AppendLine($"    - \"{p}\"");
        }

        sb.AppendLine($"  manual: {trigger["manual"] ?? false}");
    }

    // Steps
    if (workflow["steps"] is JArray steps)
    {
        sb.AppendLine();
        sb.AppendLine($"  ## Steps ({steps.Count})");
        var stepNum = 1;
        foreach (var step in steps)
        {
            sb.AppendLine($"  {stepNum}. [{step["id"]}] {step["name"]}");
            sb.AppendLine($"     Action: {step["action"]}");
            if (step["params"] is JObject stepParams && stepParams.Count > 0)
            {
                sb.AppendLine($"     Params: {stepParams.ToString(Newtonsoft.Json.Formatting.None)}");
            }
            stepNum++;
        }
    }

    // Notifications
    if (workflow["notifications"] is JObject notifications)
    {
        sb.AppendLine();
        sb.AppendLine($"  ## Notifications");
        if (notifications["onSuccess"] != null)
            sb.AppendLine($"  onSuccess: {notifications["onSuccess"]}");
        if (notifications["onFailure"] != null)
            sb.AppendLine($"  onFailure: {notifications["onFailure"]}");
    }

    sb.AppendLine("}");
    return sb.ToString();
});

server.RegisterTool("ExecuteWorkflow", async (args) =>
{
    var workflowId = args["workflowId"]?.Value<string>() ?? throw new ArgumentException("workflowId required");
    var variables = args["variables"] as JObject;

    var payload = new JObject
    {
        ["workflowId"] = workflowId
    };
    if (variables != null)
        payload["variables"] = variables;

    var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
    var response = await server.Http.PostAsJsonAsync($"/api/automation/workflows/{workflowId}/execute", content);

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to execute workflow: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"🚀 Workflow execution started");
    sb.AppendLine($"  Workflow: {workflowId}");
    sb.AppendLine($"  Run ID: {result["runId"]}");
    sb.AppendLine($"  Status: {result["status"]}");

    return sb.ToString();
});

server.RegisterTool("GetWorkflowRuns", async (args) =>
{
    var workflowId = args["workflowId"]?.Value<string>();
    var status = args["status"]?.Value<string>();
    var take = args["take"]?.Value<int>() ?? 20;

    var url = "/api/automation/workflows/runs";
    var queryParams = new List<string> { $"take={take}" };
    if (!string.IsNullOrEmpty(workflowId)) queryParams.Add($"workflowId={Uri.EscapeDataString(workflowId)}");
    if (!string.IsNullOrEmpty(status)) queryParams.Add($"status={Uri.EscapeDataString(status)}");
    url += "?" + string.Join("&", queryParams);

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get workflow runs: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var runs = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine("# Workflow Runs");
    sb.AppendLine($"Showing: {runs.Count} runs");
    sb.AppendLine();

    foreach (var run in runs)
    {
        var runStatus = run["status"]?.ToString() ?? "unknown";
        var statusIcon = runStatus switch
        {
            "Success" or "Completed" => "✅",
            "Running" or "InProgress" => "🔄",
            "Failed" or "Error" => "❌",
            "Cancelled" => "⏹️",
            "Pending" => "⏳",
            _ => "⚪"
        };

        sb.AppendLine($"{statusIcon} @workflowRun[{run["id"]}]");
        sb.AppendLine($"   Workflow: {run["workflowName"] ?? run["workflowId"]}");
        sb.AppendLine($"   Status: {runStatus}");
        sb.AppendLine($"   Started: {run["startedAt"]}");
        if (run["completedAt"] != null)
            sb.AppendLine($"   Completed: {run["completedAt"]}");
        if (run["duration"] != null)
            sb.AppendLine($"   Duration: {run["duration"]}");
        if (runStatus == "Failed" && run["error"] != null)
            sb.AppendLine($"   Error: {run["error"]}");
        sb.AppendLine();
    }

    if (runs.Count == 0)
    {
        sb.AppendLine("No workflow runs found.");
    }

    return sb.ToString();
});

server.RegisterTool("ToggleWorkflow", async (args) =>
{
    var workflowId = args["workflowId"]?.Value<string>() ?? throw new ArgumentException("workflowId required");
    var enabled = args["enabled"]?.Value<bool>() ?? throw new ArgumentException("enabled required");

    var payload = new JObject
    {
        ["enabled"] = enabled
    };

    var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
    var response = await server.Http.PatchAsync($"/api/automation/workflows/{workflowId}", content);

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to toggle workflow: {response.StatusCode}");

    return $"{(enabled ? "✅ Enabled" : "⭕ Disabled")} workflow {workflowId}";
});

// ----------------------------------------------------------------------
// Context Navigation Tools (User, Tenant, Project)
// ----------------------------------------------------------------------

server.RegisterTool("GetUserContext", async (args) =>
{
    var response = await server.Http.GetAsync("/api/context/bootstrap");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get user context: {response.StatusCode}");

    var bootstrap = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine("@userContext {");
    sb.AppendLine($"  userId: {bootstrap["id"]}");
    sb.AppendLine($"  email: \"{bootstrap["email"]}\"");
    sb.AppendLine($"  name: \"{bootstrap["firstName"]} {bootstrap["lastName"]}\"");
    sb.AppendLine();

    var tenants = bootstrap["tenants"] as JArray ?? new JArray();
    sb.AppendLine($"  tenants: [{tenants.Count}] {{");
    foreach (var tenant in tenants)
    {
        sb.AppendLine($"    @tenant[{tenant["id"]}] {{ name: \"{tenant["name"]}\", slug: \"{tenant["slug"]}\" }}");
    }
    sb.AppendLine("  }");
    sb.AppendLine();

    var workspaces = bootstrap["workspaces"] as JArray ?? new JArray();
    sb.AppendLine($"  workspaces: [{workspaces.Count}] {{");
    foreach (var ws in workspaces)
    {
        sb.AppendLine($"    @workspace[{ws["id"]}] {{ name: \"{ws["name"]}\", slug: \"{ws["slug"]}\", type: \"{ws["workspaceType"]}\" }}");
    }
    sb.AppendLine("  }");
    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("GetTenantContext", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");

    var response = await server.Http.GetAsync($"/api/context/tenant/{tenantId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get tenant context: {response.StatusCode}");

    var tenant = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"@tenantContext[{tenantId}] {{");
    sb.AppendLine($"  name: \"{tenant["name"]}\"");
    sb.AppendLine($"  slug: \"{tenant["slug"]}\"");
    sb.AppendLine($"  features: [{string.Join(", ", (tenant["features"] as JArray ?? new JArray()).Select(f => f.ToString()))}]");
    sb.AppendLine();

    var workspaces = tenant["workspaces"] as JArray ?? new JArray();
    sb.AppendLine($"  workspaces: [{workspaces.Count}] {{");
    foreach (var ws in workspaces)
    {
        sb.AppendLine($"    @workspace[{ws["id"]}] {{");
        sb.AppendLine($"      name: \"{ws["name"]}\"");
        sb.AppendLine($"      slug: \"{ws["slug"]}\"");
        sb.AppendLine($"      type: \"{ws["workspaceType"]}\"");
        sb.AppendLine($"    }}");
    }
    sb.AppendLine("  }");
    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("GetProjectContext", async (args) =>
{
    var projectId = args["projectId"]?.Value<int>() ?? throw new ArgumentException("projectId required");

    var response = await server.Http.GetAsync($"/api/projects/{projectId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get project: {response.StatusCode}");

    var project = JObject.Parse(await response.Content.ReadAsStringAsync());

    // Get tasks for this project
    var tasksResponse = await server.Http.GetAsync($"/api/tasks?projectId={projectId}&limit=50");
    var tasks = tasksResponse.IsSuccessStatusCode
        ? JObject.Parse(await tasksResponse.Content.ReadAsStringAsync())["data"] as JArray ?? new JArray()
        : new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"@projectContext[{projectId}] {{");
    sb.AppendLine($"  name: \"{project["name"]}\"");
    sb.AppendLine($"  description: \"{project["description"] ?? ""}\"");
    sb.AppendLine($"  status: \"{project["status"]}\"");
    sb.AppendLine($"  workspaceId: {project["workspaceId"]}");
    sb.AppendLine();

    sb.AppendLine($"  tasks: [{tasks.Count}] {{");
    foreach (var task in tasks.Take(20))
    {
        var status = task["percentComplete"]?.Value<int>() == 100 ? "✓" :
                     task["percentComplete"]?.Value<int>() > 0 ? "●" : "○";
        sb.AppendLine($"    {status} @task[{task["id"]}] {{ name: \"{task["name"]}\", progress: {task["percentComplete"] ?? 0}% }}");
    }
    if (tasks.Count > 20) sb.AppendLine($"    ... and {tasks.Count - 20} more tasks");
    sb.AppendLine("  }");

    sb.AppendLine("}");
    return sb.ToString();
});

// ----------------------------------------------------------------------
// Agent Plan Tools (TodoWrite-style)
// ----------------------------------------------------------------------

server.RegisterTool("AgentPlanGet", async (args) =>
{
    var agentId = args["agentId"]?.Value<int>() ?? throw new ArgumentException("agentId required");

    var response = await server.Http.PostAsJsonAsync($"/api/agents/coordination/GetAgentContext?agentId={agentId}",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get agent plan: {response.StatusCode}");

    var context = JObject.Parse(await response.Content.ReadAsStringAsync());
    var todos = context["todoList"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"@agentPlan[{agentId}] {{");
    sb.AppendLine($"  taskId: \"{context["taskId"] ?? ""}\"");
    sb.AppendLine($"  taskName: \"{context["taskName"] ?? ""}\"");
    sb.AppendLine();

    var completed = todos.Count(t => t["status"]?.ToString() == "Completed");
    var inProgress = todos.Count(t => t["status"]?.ToString() == "InProgress");
    var pending = todos.Count - completed - inProgress;

    sb.AppendLine($"  progress: {completed}/{todos.Count} ({(todos.Count > 0 ? completed * 100 / todos.Count : 0)}%)");
    sb.AppendLine();

    sb.AppendLine($"  todos: [{todos.Count}] {{");
    int idx = 0;
    foreach (var todo in todos)
    {
        var status = todo["status"]?.ToString();
        var marker = status == "Completed" ? "✓" : status == "InProgress" ? "●" : "○";
        sb.AppendLine($"    [{idx}] {marker} {todo["content"]}");
        idx++;
    }
    sb.AppendLine("  }");
    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("AgentPlanAdd", async (args) =>
{
    var agentId = args["agentId"]?.Value<int>() ?? throw new ArgumentException("agentId required");
    var content = args["content"]?.Value<string>() ?? throw new ArgumentException("content required");
    var status = args["status"]?.Value<string>() ?? "Pending";

    var todoItem = new JObject
    {
        ["content"] = content,
        ["status"] = status
    };

    var response = await server.Http.PostAsJsonAsync($"/api/agents/coordination/AddTodo?agentId={agentId}",
        new StringContent(todoItem.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to add plan item: {response.StatusCode}");

    return $"✓ Added to agent {agentId} plan: \"{content}\" [{status}]";
});

server.RegisterTool("AgentPlanUpdate", async (args) =>
{
    var agentId = args["agentId"]?.Value<int>() ?? throw new ArgumentException("agentId required");
    var index = args["index"]?.Value<int>() ?? throw new ArgumentException("index required");
    var status = args["status"]?.Value<string>() ?? throw new ArgumentException("status required (Pending, InProgress, Completed, Skipped)");

    var update = new JObject
    {
        ["index"] = index,
        ["status"] = status
    };

    var response = await server.Http.PostAsJsonAsync($"/api/agents/coordination/UpdateTodoStatus?agentId={agentId}",
        new StringContent(update.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to update plan item: {response.StatusCode}");

    var statusIcon = status switch
    {
        "Completed" => "✓",
        "InProgress" => "●",
        "Skipped" => "⊘",
        _ => "○"
    };

    return $"{statusIcon} Updated item [{index}] to {status}";
});

// ----------------------------------------------------------------------
// Canvas Tools
// ----------------------------------------------------------------------

server.RegisterTool("GetCanvasList", async (args) =>
{
    var workspaceId = args["workspaceId"]?.Value<int>() ?? throw new ArgumentException("workspaceId required");

    var response = await server.Http.GetAsync($"/api/canvas?workspaceId={workspaceId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get canvases: {response.StatusCode}");

    var canvases = JArray.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"Canvases i workspace {workspaceId} ({canvases.Count}):");
    sb.AppendLine();

    var grouped = canvases.GroupBy(c => c["parentFolderName"]?.ToString() ?? "(root)");
    foreach (var group in grouped)
    {
        sb.AppendLine($"📁 {group.Key}");
        foreach (var canvas in group)
        {
            var icon = canvas["isPinned"]?.Value<bool>() == true ? "📌" : "📋";
            var archived = canvas["isArchived"]?.Value<bool>() == true ? " [archived]" : "";
            sb.AppendLine($"  {icon} @canvas[{canvas["id"]}] {{");
            sb.AppendLine($"      name: \"{canvas["name"]}\"");
            sb.AppendLine($"      nodes: {canvas["nodeCount"] ?? 0}");
            sb.AppendLine($"      edges: {canvas["edgeCount"] ?? 0}{archived}");
            sb.AppendLine($"  }}");
        }
        sb.AppendLine();
    }

    return sb.ToString();
});

server.RegisterTool("GetCanvasDetail", async (args) =>
{
    var canvasId = args["canvasId"]?.Value<int>() ?? throw new ArgumentException("canvasId required");

    var response = await server.Http.GetAsync($"/api/canvas/{canvasId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get canvas: {response.StatusCode}");

    var canvas = JObject.Parse(await response.Content.ReadAsStringAsync());
    var nodes = canvas["nodes"] as JArray ?? new JArray();
    var edges = canvas["edges"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"@canvasDetail[{canvasId}] {{");
    sb.AppendLine($"  name: \"{canvas["name"]}\"");
    sb.AppendLine($"  description: \"{canvas["description"] ?? ""}\"");
    sb.AppendLine($"  storageMode: \"{canvas["storageMode"]}\"");
    sb.AppendLine($"  createdBy: \"{canvas["createdByName"]}\"");
    sb.AppendLine();

    sb.AppendLine($"  nodes: [{nodes.Count}] {{");
    foreach (var node in nodes.Take(15))
    {
        var nodeType = node["type"]?.ToString();
        var content = nodeType switch
        {
            "Text" or "0" => node["textContent"]?.ToString()?.Substring(0, Math.Min(50, node["textContent"]?.ToString()?.Length ?? 0)),
            "Entity" or "4" => $"{node["entityType"]} #{node["entityId"]}",
            "Link" or "2" => node["linkUrl"]?.ToString(),
            "File" or "1" => node["filePath"]?.ToString(),
            "Group" or "3" => $"[Group: {node["groupLabel"]}]",
            _ => nodeType
        };
        var nodeId = node["id"]?.ToString() ?? "";
        var shortId = nodeId.Length > 8 ? nodeId.Substring(0, 8) : nodeId;
        sb.AppendLine($"    [{shortId}] {nodeType}: {content}");
    }
    if (nodes.Count > 15) sb.AppendLine($"    ... and {nodes.Count - 15} more");
    sb.AppendLine("  }");
    sb.AppendLine();

    sb.AppendLine($"  edges: [{edges.Count}] {{");
    foreach (var edge in edges.Take(10))
    {
        var fromId = edge["fromNodeId"]?.ToString() ?? "";
        var toId = edge["toNodeId"]?.ToString() ?? "";
        var shortFrom = fromId.Length > 8 ? fromId.Substring(0, 8) : fromId;
        var shortTo = toId.Length > 8 ? toId.Substring(0, 8) : toId;
        var label = edge["label"]?.ToString();
        var labelStr = string.IsNullOrEmpty(label) ? "" : $" ({label})";
        sb.AppendLine($"    {shortFrom} -> {shortTo}{labelStr}");
    }
    if (edges.Count > 10) sb.AppendLine($"    ... and {edges.Count - 10} more");
    sb.AppendLine("  }");

    sb.AppendLine("}");
    return sb.ToString();
});

server.RegisterTool("CreateCanvas", async (args) =>
{
    var workspaceId = args["workspaceId"]?.Value<int>() ?? throw new ArgumentException("workspaceId required");
    var name = args["name"]?.Value<string>() ?? throw new ArgumentException("name required");
    var description = args["description"]?.Value<string>();

    var request = new JObject
    {
        ["workspaceId"] = workspaceId,
        ["name"] = name,
        ["description"] = description,
        ["isPersonal"] = false,
        ["isTemplate"] = false
    };

    var response = await server.Http.PostAsJsonAsync("/api/canvas",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to create canvas: {response.StatusCode} - {error}");
    }

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    return $"✓ Canvas oprettet med ID: {result["id"]}";
});

server.RegisterTool("AddCanvasNode", async (args) =>
{
    var canvasId = args["canvasId"]?.Value<int>() ?? throw new ArgumentException("canvasId required");
    var nodeType = args["type"]?.Value<string>() ?? "Text";
    var content = args["content"]?.Value<string>();
    var entityId = args["entityId"]?.Value<int?>();
    var x = args["x"]?.Value<double>() ?? 100;
    var y = args["y"]?.Value<double>() ?? 100;

    var typeValue = nodeType.ToLower() switch
    {
        "text" => 0,
        "file" => 1,
        "link" => 2,
        "group" => 3,
        "entity" => 4,
        _ => 0
    };

    var request = new JObject
    {
        ["type"] = typeValue,
        ["x"] = x,
        ["y"] = y,
        ["width"] = 300,
        ["height"] = 200
    };

    switch (nodeType.ToLower())
    {
        case "text":
            request["textContent"] = content;
            break;
        case "link":
            request["linkUrl"] = content;
            break;
        case "file":
            request["filePath"] = content;
            break;
        case "group":
            request["groupLabel"] = content;
            break;
        case "entity":
            request["entityType"] = content;
            request["entityId"] = entityId;
            break;
    }

    var response = await server.Http.PostAsJsonAsync($"/api/canvas/{canvasId}/nodes",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to add node: {response.StatusCode} - {error}");
    }

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    return $"✓ Node tilføjet med ID: {result["nodeId"]}";
});

server.RegisterTool("SaveContextToCanvas", async (args) =>
{
    var agentId = args["agentId"]?.Value<int>() ?? throw new ArgumentException("agentId required");
    var canvasId = args["canvasId"]?.Value<int>() ?? throw new ArgumentException("canvasId required");

    // Get agent context
    var contextResponse = await server.Http.PostAsJsonAsync($"/api/agents/coordination/GetAgentContext?agentId={agentId}",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!contextResponse.IsSuccessStatusCode)
        throw new Exception($"Failed to get agent context: {contextResponse.StatusCode}");

    var context = JObject.Parse(await contextResponse.Content.ReadAsStringAsync());
    var todos = context["todoList"] as JArray ?? new JArray();
    var files = context["activeFiles"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    var nodesCreated = 0;

    // Create a group node for the context
    var groupRequest = new JObject
    {
        ["type"] = 3, // Group
        ["x"] = 50,
        ["y"] = 50,
        ["width"] = 600,
        ["height"] = 400,
        ["groupLabel"] = $"Agent #{agentId} - {context["taskName"]}"
    };

    var groupResponse = await server.Http.PostAsJsonAsync($"/api/canvas/{canvasId}/nodes",
        new StringContent(groupRequest.ToString(), Encoding.UTF8, "application/json"));

    if (groupResponse.IsSuccessStatusCode)
    {
        nodesCreated++;
        sb.AppendLine($"✓ Oprettet gruppe: Agent #{agentId} - {context["taskName"]}");
    }

    // Create text nodes for todos
    var yOffset = 100;
    foreach (var todo in todos.Take(10))
    {
        var status = todo["status"]?.ToString();
        var marker = status == "Completed" ? "✓" : status == "InProgress" ? "●" : "○";
        var todoText = $"{marker} {todo["content"]}";

        var todoRequest = new JObject
        {
            ["type"] = 0, // Text
            ["x"] = 80,
            ["y"] = yOffset,
            ["width"] = 300,
            ["height"] = 40,
            ["textContent"] = todoText
        };

        var todoResponse = await server.Http.PostAsJsonAsync($"/api/canvas/{canvasId}/nodes",
            new StringContent(todoRequest.ToString(), Encoding.UTF8, "application/json"));

        if (todoResponse.IsSuccessStatusCode)
        {
            nodesCreated++;
        }
        yOffset += 50;
    }

    // Create text nodes for active files
    foreach (var file in files.Take(5))
    {
        var fileRequest = new JObject
        {
            ["type"] = 0, // Text
            ["x"] = 400,
            ["y"] = yOffset - (todos.Count * 50) + 100,
            ["width"] = 200,
            ["height"] = 30,
            ["textContent"] = $"📄 {file["filePath"]}"
        };

        var fileResponse = await server.Http.PostAsJsonAsync($"/api/canvas/{canvasId}/nodes",
            new StringContent(fileRequest.ToString(), Encoding.UTF8, "application/json"));

        if (fileResponse.IsSuccessStatusCode)
        {
            nodesCreated++;
        }
    }

    sb.AppendLine($"✓ Gemt {nodesCreated} nodes til canvas #{canvasId}");
    sb.AppendLine($"  - {Math.Min(todos.Count, 10)} todos");
    sb.AppendLine($"  - {Math.Min(files.Count, 5)} aktive filer");

    return sb.ToString();
});

// ----------------------------------------------------------------------
// Tenant Feature Management Tools
// ----------------------------------------------------------------------

server.RegisterTool("GetTenantModules", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");

    var response = await server.Http.GetAsync($"/api/tenants/{tenantId}/features/GetModulesWithFeatures");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get tenant modules: {response.StatusCode}");

    var modules = JArray.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"@tenantModules[{tenantId}] {{");
    sb.AppendLine($"  totalModules: {modules.Count}");
    sb.AppendLine();

    foreach (var module in modules)
    {
        var features = module["features"] as JArray ?? new JArray();
        var enabledCount = features.Count(f => f["isEnabled"]?.Value<bool>() == true);
        var isCore = module["isCore"]?.Value<bool>() == true;

        sb.AppendLine($"  @module[{module["id"]}] {{");
        sb.AppendLine($"    name: \"{module["name"]}\"");
        sb.AppendLine($"    icon: \"{module["icon"] ?? ""}\"");
        sb.AppendLine($"    isCore: {isCore.ToString().ToLower()}");
        sb.AppendLine($"    features: {enabledCount}/{features.Count} enabled");

        if (features.Count > 0)
        {
            sb.AppendLine($"    featureList: [");
            foreach (var feature in features)
            {
                var isEnabled = feature["isEnabled"]?.Value<bool>() == true;
                var marker = isEnabled ? "✓" : "○";
                sb.AppendLine($"      {marker} @feature[{feature["id"]}] \"{feature["name"]}\"");
            }
            sb.AppendLine($"    ]");
        }
        sb.AppendLine($"  }}");
    }
    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("GetTenantFeatures", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var moduleId = args["moduleId"]?.Value<int>();

    var response = await server.Http.GetAsync($"/api/tenants/{tenantId}/features/GetFeatures");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get tenant features: {response.StatusCode}");

    var features = JArray.Parse(await response.Content.ReadAsStringAsync());

    // Filter by module if specified
    if (moduleId.HasValue)
    {
        features = new JArray(features.Where(f => f["moduleId"]?.Value<int>() == moduleId.Value));
    }

    var sb = new StringBuilder();
    sb.AppendLine($"@tenantFeatures[{tenantId}] {{");
    sb.AppendLine($"  totalFeatures: {features.Count}");
    sb.AppendLine($"  enabled: {features.Count(f => f["isEnabled"]?.Value<bool>() == true)}");
    sb.AppendLine($"  disabled: {features.Count(f => f["isEnabled"]?.Value<bool>() != true)}");
    sb.AppendLine();

    // Group by module for cleaner output
    var groupedFeatures = features.GroupBy(f => f["moduleName"]?.ToString() ?? "Unknown");
    foreach (var group in groupedFeatures)
    {
        sb.AppendLine($"  [{group.Key}] {{");
        foreach (var feature in group)
        {
            var isEnabled = feature["isEnabled"]?.Value<bool>() == true;
            var marker = isEnabled ? "✓" : "○";
            var canOverride = feature["canUserOverride"]?.Value<bool>() == true;
            var overrideHint = canOverride ? "" : " [locked]";
            sb.AppendLine($"    {marker} @feature[{feature["id"]}] \"{feature["name"]}\"{overrideHint}");

            // Show dependencies if any
            var deps = feature["dependsOnFeatureIds"] as JArray;
            if (deps != null && deps.Count > 0)
            {
                sb.AppendLine($"       → depends on: [{string.Join(", ", deps.Select(d => d.ToString()))}]");
            }
        }
        sb.AppendLine($"  }}");
    }
    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("EnableTenantFeature", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var featureId = args["featureId"]?.Value<int>() ?? throw new ArgumentException("featureId required");
    var notes = args["notes"]?.Value<string>();

    var payload = new JObject();
    if (!string.IsNullOrEmpty(notes))
    {
        payload["notes"] = notes;
    }

    var response = await server.Http.PostAsJsonAsync(
        $"/api/tenants/{tenantId}/features/EnableFeature?featureId={featureId}",
        new StringContent(payload.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to enable feature: {response.StatusCode} - {error}");
    }

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var success = result["success"]?.Value<bool>() == true;

    if (success)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"✅ Feature {featureId} enabled for tenant {tenantId}");

        var enabledPrereqs = result["enabledPrerequisites"] as JArray;
        if (enabledPrereqs != null && enabledPrereqs.Count > 0)
        {
            sb.AppendLine($"   Also enabled prerequisites: {string.Join(", ", enabledPrereqs.Select(p => p.ToString()))}");
        }
        return sb.ToString();
    }
    else
    {
        var message = result["message"]?.ToString() ?? "Unknown error";
        var blockedBy = result["blockedByFeatures"] as JArray;
        var sb = new StringBuilder();
        sb.AppendLine($"❌ Failed to enable feature: {message}");
        if (blockedBy != null && blockedBy.Count > 0)
        {
            sb.AppendLine($"   Blocked by: {string.Join(", ", blockedBy.Select(b => b.ToString()))}");
        }
        return sb.ToString();
    }
});

server.RegisterTool("DisableTenantFeature", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var featureId = args["featureId"]?.Value<int>() ?? throw new ArgumentException("featureId required");
    var force = args["force"]?.Value<bool>() ?? false;
    var notes = args["notes"]?.Value<string>();

    var payload = new JObject
    {
        ["force"] = force
    };
    if (!string.IsNullOrEmpty(notes))
    {
        payload["notes"] = notes;
    }

    var response = await server.Http.PostAsJsonAsync(
        $"/api/tenants/{tenantId}/features/DisableFeature?featureId={featureId}",
        new StringContent(payload.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to disable feature: {response.StatusCode} - {error}");
    }

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var success = result["success"]?.Value<bool>() == true;

    if (success)
    {
        return $"✅ Feature {featureId} disabled for tenant {tenantId}";
    }
    else
    {
        var message = result["message"]?.ToString() ?? "Unknown error";
        var dependents = result["blockingDependents"] as JArray;
        var sb = new StringBuilder();
        sb.AppendLine($"❌ Failed to disable feature: {message}");
        if (dependents != null && dependents.Count > 0)
        {
            sb.AppendLine($"   Blocked by dependents: {string.Join(", ", dependents.Select(d => d.ToString()))}");
            sb.AppendLine($"   Use force=true to disable anyway (will also disable dependents)");
        }
        return sb.ToString();
    }
});

server.RegisterTool("DisableTenantModule", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var moduleId = args["moduleId"]?.Value<int>() ?? throw new ArgumentException("moduleId required");
    var force = args["force"]?.Value<bool>() ?? false;

    var payload = new JObject
    {
        ["force"] = force
    };

    var response = await server.Http.PostAsJsonAsync(
        $"/api/tenants/{tenantId}/features/DisableModule?moduleId={moduleId}",
        new StringContent(payload.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to disable module: {response.StatusCode} - {error}");
    }

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var success = result["success"]?.Value<bool>() == true;

    if (success)
    {
        var disabledCount = result["disabledFeatures"]?.Value<int>() ?? 0;
        return $"✅ Module {moduleId} disabled for tenant {tenantId} ({disabledCount} features disabled)";
    }
    else
    {
        var message = result["message"]?.ToString() ?? "Unknown error";
        return $"❌ Failed to disable module: {message}";
    }
});

server.RegisterTool("GetFeaturePrerequisites", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var featureId = args["featureId"]?.Value<int>() ?? throw new ArgumentException("featureId required");

    var response = await server.Http.GetAsync($"/api/tenants/{tenantId}/features/GetPrerequisites?featureId={featureId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get prerequisites: {response.StatusCode}");

    var prereqs = JArray.Parse(await response.Content.ReadAsStringAsync());

    if (prereqs.Count == 0)
    {
        return $"Feature {featureId} has no prerequisites - it can be enabled immediately.";
    }

    var sb = new StringBuilder();
    sb.AppendLine($"@featurePrerequisites[{featureId}] {{");
    sb.AppendLine($"  count: {prereqs.Count}");
    sb.AppendLine($"  features: [");
    foreach (var prereq in prereqs)
    {
        var isEnabled = prereq["isEnabled"]?.Value<bool>() == true;
        var marker = isEnabled ? "✓" : "○";
        sb.AppendLine($"    {marker} @feature[{prereq["id"]}] \"{prereq["name"]}\" ({prereq["moduleName"]})");
    }
    sb.AppendLine($"  ]");
    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("GetFeatureDependents", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var featureId = args["featureId"]?.Value<int>() ?? throw new ArgumentException("featureId required");

    var response = await server.Http.GetAsync($"/api/tenants/{tenantId}/features/GetDependents?featureId={featureId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get dependents: {response.StatusCode}");

    var dependents = JArray.Parse(await response.Content.ReadAsStringAsync());

    if (dependents.Count == 0)
    {
        return $"No features depend on feature {featureId} - it can be disabled safely.";
    }

    var sb = new StringBuilder();
    sb.AppendLine($"@featureDependents[{featureId}] {{");
    sb.AppendLine($"  count: {dependents.Count}");
    sb.AppendLine($"  ⚠️  These features will be affected if you disable feature {featureId}:");
    sb.AppendLine($"  features: [");
    foreach (var dep in dependents)
    {
        var isEnabled = dep["isEnabled"]?.Value<bool>() == true;
        var marker = isEnabled ? "✓" : "○";
        sb.AppendLine($"    {marker} @feature[{dep["id"]}] \"{dep["name"]}\" ({dep["moduleName"]})");
    }
    sb.AppendLine($"  ]");
    sb.AppendLine("}");

    return sb.ToString();
});

// ----------------------------------------------------------------------
// Admin Feature Tools (System-level management)
// ----------------------------------------------------------------------

server.RegisterTool("GetAllSystemModules", async (args) =>
{
    var response = await server.Http.GetAsync("/api/administration/features/GetAllModules");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get system modules: {response.StatusCode}");

    var modules = JArray.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine("@systemModules {");
    sb.AppendLine($"  totalModules: {modules.Count}");
    sb.AppendLine($"  totalFeatures: {modules.Sum(m => (m["features"] as JArray)?.Count ?? 0)}");
    sb.AppendLine();

    foreach (var module in modules)
    {
        var features = module["features"] as JArray ?? new JArray();
        var isCore = module["isCore"]?.Value<bool>() == true;
        var coreHint = isCore ? " [CORE]" : "";

        sb.AppendLine($"  @module[{module["id"]}] \"{module["name"]}\"{coreHint} {{");
        sb.AppendLine($"    description: \"{module["description"] ?? ""}\"");
        sb.AppendLine($"    icon: \"{module["icon"] ?? ""}\"");
        sb.AppendLine($"    featureCount: {features.Count}");
        sb.AppendLine($"    features: [");
        foreach (var feature in features)
        {
            var isDefault = feature["isDefault"]?.Value<bool>() == true;
            var defaultHint = isDefault ? " [default on]" : "";
            var canOverride = feature["canUserOverride"]?.Value<bool>() == true;
            var overrideHint = canOverride ? "" : " [locked]";
            sb.AppendLine($"      @feature[{feature["id"]}] \"{feature["name"]}\"{defaultHint}{overrideHint}");
        }
        sb.AppendLine($"    ]");
        sb.AppendLine($"  }}");
    }
    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("AdminOverrideFeature", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var featureId = args["featureId"]?.Value<int>() ?? throw new ArgumentException("featureId required");
    var action = args["action"]?.Value<string>()?.ToLower() ?? throw new ArgumentException("action required (grant, revoke, remove)");
    var notes = args["notes"]?.Value<string>() ?? "Admin override via MCP";

    // Map action to enum value
    var actionValue = action switch
    {
        "grant" => 0,
        "revoke" => 1,
        "remove" => 2,
        _ => throw new ArgumentException("action must be 'grant', 'revoke', or 'remove'")
    };

    var payload = new JObject
    {
        ["featureId"] = featureId,
        ["action"] = actionValue,
        ["notes"] = notes
    };

    var response = await server.Http.PostAsJsonAsync(
        $"/api/administration/features/OverrideFeature?tenantId={tenantId}",
        new StringContent(payload.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to override feature: {response.StatusCode} - {error}");
    }

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var success = result["success"]?.Value<bool>() == true;

    if (success)
    {
        return action switch
        {
            "grant" => $"✅ Feature {featureId} granted to tenant {tenantId}",
            "revoke" => $"✅ Feature {featureId} revoked from tenant {tenantId}",
            "remove" => $"✅ Feature override removed for tenant {tenantId} (will use plan default)",
            _ => $"✅ Feature override applied"
        };
    }
    else
    {
        var message = result["message"]?.ToString() ?? "Unknown error";
        return $"❌ Failed to override feature: {message}";
    }
});

server.RegisterTool("AdminBulkEnableFeatures", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var featureIdsToken = args["featureIds"] ?? throw new ArgumentException("featureIds required (array of feature IDs)");

    var featureIds = new List<int>();
    if (featureIdsToken is JArray arr)
    {
        foreach (var id in arr)
        {
            featureIds.Add(id.Value<int>());
        }
    }
    else
    {
        // Support comma-separated string
        var idStr = featureIdsToken.Value<string>() ?? "";
        foreach (var id in idStr.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(id.Trim(), out var parsed))
                featureIds.Add(parsed);
        }
    }

    if (featureIds.Count == 0)
        throw new ArgumentException("No valid feature IDs provided");

    var response = await server.Http.PostAsJsonAsync(
        $"/api/administration/features/BulkEnableFeatures?tenantId={tenantId}",
        new StringContent(new JArray(featureIds).ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to bulk enable features: {response.StatusCode} - {error}");
    }

    var results = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"@bulkEnableResult[tenant:{tenantId}] {{");

    int successCount = 0, failCount = 0;
    foreach (var prop in results.Properties())
    {
        var featureResult = prop.Value as JObject;
        var success = featureResult?["success"]?.Value<bool>() == true;
        var marker = success ? "✓" : "✗";
        if (success) successCount++; else failCount++;

        sb.AppendLine($"  {marker} feature[{prop.Name}]: {(success ? "enabled" : featureResult?["message"]?.ToString() ?? "failed")}");
    }

    sb.AppendLine();
    sb.AppendLine($"  summary: {successCount} succeeded, {failCount} failed");
    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("GetAllFeatureOverrides", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>();

    var response = await server.Http.GetAsync("/api/administration/features/GetAllOverrides");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get feature overrides: {response.StatusCode}");

    var overrides = JArray.Parse(await response.Content.ReadAsStringAsync());

    // Filter by tenant if specified
    if (tenantId.HasValue)
    {
        overrides = new JArray(overrides.Where(o => o["tenantId"]?.Value<int>() == tenantId.Value));
    }

    var sb = new StringBuilder();
    sb.AppendLine("@featureOverrides {");
    sb.AppendLine($"  count: {overrides.Count}");
    sb.AppendLine();

    // Group by tenant
    var byTenant = overrides.GroupBy(o => o["tenantId"]?.Value<int>() ?? 0);
    foreach (var tenantGroup in byTenant)
    {
        sb.AppendLine($"  @tenant[{tenantGroup.Key}] \"{tenantGroup.First()["tenantName"]}\" {{");
        foreach (var ov in tenantGroup)
        {
            var ovType = ov["overrideType"]?.ToString() ?? "Unknown";
            var marker = ovType == "Grant" ? "✓" : "✗";
            sb.AppendLine($"    {marker} @feature[{ov["featureId"]}] \"{ov["featureName"]}\" ({ovType})");
            if (ov["expiresAt"] != null)
            {
                sb.AppendLine($"       expires: {ov["expiresAt"]}");
            }
        }
        sb.AppendLine($"  }}");
    }
    sb.AppendLine("}");

    return sb.ToString();
});

// ----------------------------------------------------------------------
// Tenant Settings Tools
// ----------------------------------------------------------------------

server.RegisterTool("GetTenantSettings", async (args) =>
{
    var category = args["category"]?.Value<string>();

    var response = await server.Http.PostAsJsonAsync("/api/administration/tenant/settings/Get",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get tenant settings: {response.StatusCode}");

    var settings = JArray.Parse(await response.Content.ReadAsStringAsync());

    // Filter by category if specified
    if (!string.IsNullOrEmpty(category))
    {
        settings = new JArray(settings.Where(s =>
            (s["category"]?.ToString() ?? "").Contains(category, StringComparison.OrdinalIgnoreCase)));
    }

    var sb = new StringBuilder();
    sb.AppendLine("@tenantSettings {");
    sb.AppendLine($"  count: {settings.Count}");
    sb.AppendLine();

    // Group by category
    var byCategory = settings.GroupBy(s => s["category"]?.ToString() ?? "General");
    foreach (var catGroup in byCategory)
    {
        sb.AppendLine($"  [{catGroup.Key}] {{");
        foreach (var setting in catGroup)
        {
            var name = setting["name"]?.ToString() ?? "";
            var value = setting["value"]?.ToString() ?? "";
            var displayValue = value.Length > 50 ? value.Substring(0, 47) + "..." : value;
            sb.AppendLine($"    @setting[{setting["id"]}] \"{name}\": \"{displayValue}\"");
        }
        sb.AppendLine($"  }}");
    }
    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("UpdateTenantSetting", async (args) =>
{
    var settingId = args["settingId"]?.Value<int>() ?? throw new ArgumentException("settingId required");
    var value = args["value"]?.Value<string>() ?? throw new ArgumentException("value required");

    var payload = new JObject
    {
        ["id"] = settingId,
        ["value"] = value
    };

    var response = await server.Http.PostAsJsonAsync("/api/administration/tenant/settings/Update",
        new StringContent(payload.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to update setting: {response.StatusCode} - {error}");
    }

    return $"✅ Setting {settingId} updated successfully";
});

// ----------------------------------------------------------------------
// Tenant Configuration Tools (Combined utilities)
// ----------------------------------------------------------------------

server.RegisterTool("ConfigureTenantFeatureSet", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var preset = args["preset"]?.Value<string>()?.ToLower();

    // Define presets
    var presets = new Dictionary<string, List<int>>
    {
        // These are example module IDs - adjust based on actual system modules
        ["minimal"] = new List<int> { 1, 2 },           // Core + Identity only
        ["standard"] = new List<int> { 1, 2, 3, 4 },    // Core + Identity + ProjectManagement + Calendar
        ["professional"] = new List<int> { 1, 2, 3, 4, 5, 6, 7 }, // + Registration + Finance + Reporting
        ["enterprise"] = new List<int> { }              // All features (empty = all)
    };

    if (string.IsNullOrEmpty(preset) || !presets.ContainsKey(preset))
    {
        var sb = new StringBuilder();
        sb.AppendLine("@featurePresets {");
        sb.AppendLine("  Available presets:");
        sb.AppendLine("    - minimal: Core + Identity modules only");
        sb.AppendLine("    - standard: + ProjectManagement + Calendar");
        sb.AppendLine("    - professional: + Registration + Finance + Reporting");
        sb.AppendLine("    - enterprise: All features enabled");
        sb.AppendLine();
        sb.AppendLine("  Usage: ConfigureTenantFeatureSet(tenantId, preset=\"standard\")");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // Get all features for the tenant to understand current state
    var featuresResponse = await server.Http.GetAsync($"/api/tenants/{tenantId}/features/GetModulesWithFeatures");
    if (!featuresResponse.IsSuccessStatusCode)
        throw new Exception($"Failed to get current features: {featuresResponse.StatusCode}");

    var modules = JArray.Parse(await featuresResponse.Content.ReadAsStringAsync());

    var resultSb = new StringBuilder();
    resultSb.AppendLine($"@configureResult[tenant:{tenantId}, preset:{preset}] {{");

    var enabledCount = 0;
    var moduleIds = presets[preset];

    foreach (var module in modules)
    {
        var modId = module["id"]?.Value<int>() ?? 0;
        var isCore = module["isCore"]?.Value<bool>() == true;

        // Enterprise enables all, others check module ID list
        var shouldEnable = preset == "enterprise" || moduleIds.Contains(modId) || isCore;

        var features = module["features"] as JArray ?? new JArray();
        foreach (var feature in features)
        {
            var featureId = feature["id"]?.Value<int>() ?? 0;
            var isEnabled = feature["isEnabled"]?.Value<bool>() == true;
            var canOverride = feature["canUserOverride"]?.Value<bool>() == true;

            if (shouldEnable && !isEnabled && canOverride)
            {
                // Enable this feature
                var enablePayload = new JObject();
                var enableResponse = await server.Http.PostAsJsonAsync(
                    $"/api/tenants/{tenantId}/features/EnableFeature?featureId={featureId}",
                    new StringContent(enablePayload.ToString(), Encoding.UTF8, "application/json"));

                if (enableResponse.IsSuccessStatusCode)
                {
                    resultSb.AppendLine($"  ✓ Enabled: {feature["name"]}");
                    enabledCount++;
                }
            }
            else if (!shouldEnable && isEnabled && canOverride && !isCore)
            {
                // Disable this feature
                var disablePayload = new JObject { ["force"] = true };
                var disableResponse = await server.Http.PostAsJsonAsync(
                    $"/api/tenants/{tenantId}/features/DisableFeature?featureId={featureId}",
                    new StringContent(disablePayload.ToString(), Encoding.UTF8, "application/json"));

                if (disableResponse.IsSuccessStatusCode)
                {
                    resultSb.AppendLine($"  ○ Disabled: {feature["name"]}");
                }
            }
        }
    }

    resultSb.AppendLine();
    resultSb.AppendLine($"  summary: Applied '{preset}' preset, {enabledCount} features adjusted");
    resultSb.AppendLine("}");

    return resultSb.ToString();
});

server.RegisterTool("GetTenantConfiguration", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");

    // Get tenant context
    var contextResponse = await server.Http.GetAsync($"/api/context/tenant/{tenantId}");
    if (!contextResponse.IsSuccessStatusCode)
        throw new Exception($"Failed to get tenant context: {contextResponse.StatusCode}");
    var tenantContext = JObject.Parse(await contextResponse.Content.ReadAsStringAsync());

    // Get features
    var featuresResponse = await server.Http.GetAsync($"/api/tenants/{tenantId}/features/GetModulesWithFeatures");
    var modules = featuresResponse.IsSuccessStatusCode
        ? JArray.Parse(await featuresResponse.Content.ReadAsStringAsync())
        : new JArray();

    // Get settings
    var settingsResponse = await server.Http.PostAsJsonAsync("/api/administration/tenant/settings/Get",
        new StringContent("{}", Encoding.UTF8, "application/json"));
    var settings = settingsResponse.IsSuccessStatusCode
        ? JArray.Parse(await settingsResponse.Content.ReadAsStringAsync())
        : new JArray();

    // Build comprehensive output
    var sb = new StringBuilder();
    sb.AppendLine($"@tenantConfiguration[{tenantId}] {{");
    sb.AppendLine();

    // Basic info
    sb.AppendLine("  @info {");
    sb.AppendLine($"    name: \"{tenantContext["name"]}\"");
    sb.AppendLine($"    slug: \"{tenantContext["slug"]}\"");
    sb.AppendLine($"    workspaces: {(tenantContext["workspaces"] as JArray)?.Count ?? 0}");
    sb.AppendLine("  }");
    sb.AppendLine();

    // Feature summary
    var totalFeatures = modules.Sum(m => (m["features"] as JArray)?.Count ?? 0);
    var enabledFeatures = modules.Sum(m => (m["features"] as JArray)?.Count(f => f["isEnabled"]?.Value<bool>() == true) ?? 0);

    sb.AppendLine("  @features {");
    sb.AppendLine($"    enabled: {enabledFeatures}/{totalFeatures}");
    sb.AppendLine($"    modules: {modules.Count}");

    var coreModules = modules.Where(m => m["isCore"]?.Value<bool>() == true).ToList();
    var optionalModules = modules.Where(m => m["isCore"]?.Value<bool>() != true).ToList();

    sb.AppendLine($"    coreModules: [{string.Join(", ", coreModules.Select(m => $"\"{m["name"]}\""))}]");

    var enabledOptional = optionalModules
        .Where(m => (m["features"] as JArray)?.Any(f => f["isEnabled"]?.Value<bool>() == true) == true)
        .Select(m => m["name"]?.ToString())
        .ToList();
    sb.AppendLine($"    enabledOptionalModules: [{string.Join(", ", enabledOptional.Select(n => $"\"{n}\""))}]");
    sb.AppendLine("  }");
    sb.AppendLine();

    // Settings summary
    var settingCategories = settings.GroupBy(s => s["category"]?.ToString() ?? "General")
        .Select(g => g.Key)
        .ToList();
    sb.AppendLine("  @settings {");
    sb.AppendLine($"    count: {settings.Count}");
    sb.AppendLine($"    categories: [{string.Join(", ", settingCategories.Select(c => $"\"{c}\""))}]");
    sb.AppendLine("  }");

    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("InvalidateTenantCache", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var reason = args["reason"]?.Value<string>() ?? "Cache invalidation via MCP";

    var payload = new JObject
    {
        ["reason"] = reason
    };

    var response = await server.Http.PostAsJsonAsync(
        $"/api/tenants/{tenantId}/features/InvalidateTenantCache",
        new StringContent(payload.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to invalidate cache: {response.StatusCode} - {error}");
    }

    return $"✅ Cache invalidated for tenant {tenantId}. Reason: {reason}";
});

// ----------------------------------------------------------------------
// Documentation Tools (Unified Docs System)
// ----------------------------------------------------------------------

server.RegisterTool("DocsSearch", async (args) =>
{
    var query = args["query"]?.Value<string>() ?? throw new ArgumentException("query required");
    var tags = args["tags"]?.Value<string>();
    var domain = args["domain"]?.Value<string>();
    var docType = args["type"]?.Value<string>();
    var limit = args["limit"]?.Value<int>() ?? 10;

    var cmdArgs = $"search \"{query}\" --limit {limit} --format json";
    if (!string.IsNullOrEmpty(tags)) cmdArgs += $" --tags \"{tags}\"";
    if (!string.IsNullOrEmpty(domain)) cmdArgs += $" --domain {domain}";
    if (!string.IsNullOrEmpty(docType)) cmdArgs += $" --type {docType}";

    return await RunServedDocsCmd(cmdArgs);
});

server.RegisterTool("DocsContext", async (args) =>
{
    var query = args["query"]?.Value<string>() ?? throw new ArgumentException("query required");
    var maxTokens = args["maxTokens"]?.Value<int>() ?? 8000;
    var format = args["format"]?.Value<string>() ?? "md";

    var cmdArgs = $"context \"{query}\" --max-tokens {maxTokens} --format {format}";
    return await RunServedDocsCmd(cmdArgs);
});

server.RegisterTool("DocsSync", async (args) =>
{
    var source = args["source"]?.Value<string>();
    var force = args["force"]?.Value<bool>() ?? false;

    var cmdArgs = source != null ? $"sync source {source}" : "sync all";
    if (force) cmdArgs += " --force";

    return await RunServedDocsCmd(cmdArgs);
});

server.RegisterTool("DocsStats", async (args) =>
{
    return await RunServedDocsCmd("index stats");
});

// Helper function to run served-docs CLI
async Task<string> RunServedDocsCmd(string args)
{
    var cliPath = FindServedDocsCli();
    var psi = new System.Diagnostics.ProcessStartInfo
    {
        FileName = cliPath,
        Arguments = args,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using var process = System.Diagnostics.Process.Start(psi);
    if (process == null)
        throw new Exception("Failed to start served-docs CLI");

    var output = await process.StandardOutput.ReadToEndAsync();
    var error = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
        throw new Exception($"served-docs failed: {error}");

    return output;
}

string FindServedDocsCli()
{
    var locations = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Python", "3.9", "bin", "served-docs"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "served-docs"),
        "/usr/local/bin/served-docs",
        "served-docs"
    };

    foreach (var loc in locations)
        if (File.Exists(loc)) return loc;

    return "served-docs";
}

// ----------------------------------------------------------------------
// Build Detection Tools (F53 - Unified Build Detection)
// ----------------------------------------------------------------------

server.RegisterTool("GetBuildStatus", async (args) =>
{
    var projectsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".served", "projects.unified.yaml");

    if (!File.Exists(projectsFile))
    {
        return "No project index found. Run 'served build index' in the ServedApp repository to create one.";
    }

    var yaml = await File.ReadAllTextAsync(projectsFile);
    var lines = yaml.Split('\n');

    var sb = new StringBuilder();
    sb.AppendLine("# Build Status");
    sb.AppendLine();

    // Extract metadata
    var repo = lines.FirstOrDefault(l => l.StartsWith("@repo:"))?.Replace("@repo:", "").Trim();
    var at = lines.FirstOrDefault(l => l.StartsWith("@at:"))?.Replace("@at:", "").Trim();

    sb.AppendLine($"**Repository:** {repo ?? "Unknown"}");
    sb.AppendLine($"**Last indexed:** {at ?? "Unknown"}");
    sb.AppendLine();

    // Count projects by type
    var inDotnet = false;
    var inNode = false;
    var inSwift = false;
    var dotnetProjects = new List<string>();
    var nodeProjects = new List<string>();
    var swiftProjects = new List<string>();

    foreach (var line in lines)
    {
        if (line.StartsWith("dotnet:")) { inDotnet = true; inNode = false; inSwift = false; continue; }
        if (line.StartsWith("node:")) { inDotnet = false; inNode = true; inSwift = false; continue; }
        if (line.StartsWith("swift:")) { inDotnet = false; inNode = false; inSwift = true; continue; }
        if (line.StartsWith("sum:")) break;

        if (line.Trim().StartsWith("- n:"))
        {
            var name = line.Replace("- n:", "").Trim();
            if (inDotnet) dotnetProjects.Add(name);
            else if (inNode) nodeProjects.Add(name);
            else if (inSwift) swiftProjects.Add(name);
        }
    }

    sb.AppendLine($"## Summary");
    sb.AppendLine($"- **.NET projects:** {dotnetProjects.Count}");
    sb.AppendLine($"- **Node.js projects:** {nodeProjects.Count}");
    sb.AppendLine($"- **Swift projects:** {swiftProjects.Count}");
    sb.AppendLine($"- **Total:** {dotnetProjects.Count + nodeProjects.Count + swiftProjects.Count}");
    sb.AppendLine();

    // List some projects
    if (dotnetProjects.Count > 0)
    {
        sb.AppendLine("## .NET Projects");
        foreach (var p in dotnetProjects.Take(10))
            sb.AppendLine($"- {p}");
        if (dotnetProjects.Count > 10)
            sb.AppendLine($"- ... and {dotnetProjects.Count - 10} more");
        sb.AppendLine();
    }

    if (nodeProjects.Count > 0)
    {
        sb.AppendLine("## Node.js Projects");
        foreach (var p in nodeProjects.Take(10))
            sb.AppendLine($"- {p}");
        if (nodeProjects.Count > 10)
            sb.AppendLine($"- ... and {nodeProjects.Count - 10} more");
        sb.AppendLine();
    }

    if (swiftProjects.Count > 0)
    {
        sb.AppendLine("## Swift Projects");
        foreach (var p in swiftProjects.Take(10))
            sb.AppendLine($"- {p}");
        if (swiftProjects.Count > 10)
            sb.AppendLine($"- ... and {swiftProjects.Count - 10} more");
    }

    return sb.ToString();
});

server.RegisterTool("GetBuildPatterns", async (args) =>
{
    var buildsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".served", "builds.unified.yaml");

    if (!File.Exists(buildsFile))
    {
        return "No build data found. Run 'served build watch <project>' to collect build patterns.";
    }

    var yaml = await File.ReadAllTextAsync(buildsFile);

    var sb = new StringBuilder();
    sb.AppendLine("# Detected Build Patterns");
    sb.AppendLine();

    // Parse patterns section
    var inPatterns = false;
    var currentPattern = new Dictionary<string, string>();
    var patterns = new List<Dictionary<string, string>>();

    foreach (var line in yaml.Split('\n'))
    {
        if (line.StartsWith("patterns:")) { inPatterns = true; continue; }
        if (line.StartsWith("changes:") || line.StartsWith("recent:")) { inPatterns = false; }

        if (inPatterns && line.Trim().StartsWith("- id:"))
        {
            if (currentPattern.Count > 0) patterns.Add(currentPattern);
            currentPattern = new Dictionary<string, string>();
            currentPattern["id"] = line.Replace("- id:", "").Trim();
        }
        else if (inPatterns && currentPattern.Count > 0)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("freq:")) currentPattern["freq"] = trimmed.Replace("freq:", "").Trim();
            else if (trimmed.StartsWith("severity:")) currentPattern["severity"] = trimmed.Replace("severity:", "").Trim();
            else if (trimmed.StartsWith("suggestion:")) currentPattern["suggestion"] = trimmed.Replace("suggestion:", "").Trim();
        }
    }
    if (currentPattern.Count > 0) patterns.Add(currentPattern);

    if (patterns.Count == 0)
    {
        sb.AppendLine("No patterns detected yet. Run builds with 'served build watch' to detect patterns.");
        return sb.ToString();
    }

    // Sort by frequency
    patterns = patterns.OrderByDescending(p => int.TryParse(p.GetValueOrDefault("freq", "0"), out var f) ? f : 0).ToList();

    foreach (var p in patterns)
    {
        var sevIcon = p.GetValueOrDefault("severity", "low") switch
        {
            "high" => "🔴",
            "medium" => "🟡",
            _ => "🟢"
        };

        sb.AppendLine($"## {sevIcon} {p.GetValueOrDefault("id", "unknown")}");
        sb.AppendLine($"- **Frequency:** {p.GetValueOrDefault("freq", "0")} occurrences");
        sb.AppendLine($"- **Severity:** {p.GetValueOrDefault("severity", "low")}");
        sb.AppendLine($"- **Suggestion:** {p.GetValueOrDefault("suggestion", "N/A")}");
        sb.AppendLine();
    }

    return sb.ToString();
});

server.RegisterTool("GetProjectInfo", async (args) =>
{
    var projectName = args["projectName"]?.Value<string>();

    if (string.IsNullOrEmpty(projectName))
    {
        return "Error: projectName argument is required. Example: { \"projectName\": \"ServedApi\" }";
    }

    var projectsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".served", "projects.unified.yaml");

    if (!File.Exists(projectsFile))
    {
        return "No project index found. Run 'served build index' to create one.";
    }

    var yaml = await File.ReadAllTextAsync(projectsFile);
    var lines = yaml.Split('\n');

    var sb = new StringBuilder();
    var foundProject = false;
    var inProject = false;
    var projectType = "";

    foreach (var line in lines)
    {
        if (line.StartsWith("dotnet:")) { projectType = ".NET"; continue; }
        if (line.StartsWith("node:")) { projectType = "Node.js"; continue; }
        if (line.StartsWith("swift:")) { projectType = "Swift"; continue; }
        if (line.StartsWith("sum:")) break;

        if (line.Trim().StartsWith($"- n: {projectName}"))
        {
            foundProject = true;
            inProject = true;
            sb.AppendLine($"# Project: {projectName}");
            sb.AppendLine($"**Type:** {projectType}");
            continue;
        }

        if (inProject)
        {
            if (line.Trim().StartsWith("- n:"))
            {
                inProject = false;
                break;
            }

            var trimmed = line.Trim();
            if (trimmed.StartsWith("p:")) sb.AppendLine($"**Path:** {trimmed.Replace("p:", "").Trim()}");
            else if (trimmed.StartsWith("fw:")) sb.AppendLine($"**Framework:** {trimmed.Replace("fw:", "").Trim()}");
            else if (trimmed.StartsWith("t:")) sb.AppendLine($"**Project Type:** {trimmed.Replace("t:", "").Trim()}");
            else if (trimmed.StartsWith("pm:")) sb.AppendLine($"**Package Manager:** {trimmed.Replace("pm:", "").Trim()}");
            else if (trimmed.StartsWith("deps:")) sb.AppendLine($"**Dependencies:** {trimmed.Replace("deps:", "").Trim()}");
            else if (trimmed.StartsWith("b:")) sb.AppendLine($"**Build Status:** {trimmed.Replace("b:", "").Trim()}");
        }
    }

    if (!foundProject)
    {
        return $"Project '{projectName}' not found in index. Run 'served build status' to see available projects.";
    }

    return sb.ToString();
});

server.RegisterTool("GetBuildHistory", async (args) =>
{
    var limit = args["limit"]?.Value<int>() ?? 10;

    var buildsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".served", "builds.unified.yaml");

    if (!File.Exists(buildsFile))
    {
        return "No build history found. Run 'served build watch <project>' to collect build data.";
    }

    var yaml = await File.ReadAllTextAsync(buildsFile);

    var sb = new StringBuilder();
    sb.AppendLine("# Build History");
    sb.AppendLine();

    // Parse recent section
    var inRecent = false;
    var currentBuild = new Dictionary<string, string>();
    var builds = new List<Dictionary<string, string>>();

    foreach (var line in yaml.Split('\n'))
    {
        if (line.StartsWith("recent:")) { inRecent = true; continue; }
        if (line.StartsWith("patterns:") || line.StartsWith("changes:") || line.StartsWith("active:"))
        {
            if (inRecent && currentBuild.Count > 0) builds.Add(currentBuild);
            inRecent = false;
        }

        if (inRecent && line.Trim().StartsWith("- p:"))
        {
            if (currentBuild.Count > 0) builds.Add(currentBuild);
            currentBuild = new Dictionary<string, string>();
            currentBuild["p"] = line.Replace("- p:", "").Trim();
        }
        else if (inRecent && currentBuild.Count > 0)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("at:")) currentBuild["at"] = trimmed.Replace("at:", "").Trim();
            else if (trimmed.StartsWith("r:")) currentBuild["r"] = trimmed.Replace("r:", "").Trim();
            else if (trimmed.StartsWith("w:")) currentBuild["w"] = trimmed.Replace("w:", "").Trim();
            else if (trimmed.StartsWith("e:")) currentBuild["e"] = trimmed.Replace("e:", "").Trim();
            else if (trimmed.StartsWith("d:")) currentBuild["d"] = trimmed.Replace("d:", "").Trim();
        }
    }
    if (currentBuild.Count > 0) builds.Add(currentBuild);

    if (builds.Count == 0)
    {
        sb.AppendLine("No build history available yet.");
        return sb.ToString();
    }

    sb.AppendLine("| Project | Time | Result | Warnings | Errors | Duration |");
    sb.AppendLine("|---------|------|--------|----------|--------|----------|");

    foreach (var b in builds.Take(limit))
    {
        var resultIcon = b.GetValueOrDefault("r", "?") switch
        {
            "ok" => "✅",
            "warn" => "⚠️",
            "err" => "❌",
            "fail" => "💥",
            _ => "❓"
        };

        var duration = int.TryParse(b.GetValueOrDefault("d", "0"), out var ms)
            ? $"{ms / 1000.0:F1}s"
            : "-";

        sb.AppendLine($"| {b.GetValueOrDefault("p", "-")} | {b.GetValueOrDefault("at", "-")} | {resultIcon} | {b.GetValueOrDefault("w", "0")} | {b.GetValueOrDefault("e", "0")} | {duration} |");
    }

    return sb.ToString();
});

// ----------------------------------------------------------------------
// Tag Detection Tools
// ----------------------------------------------------------------------

server.RegisterTool("ScanForTags", async (args) =>
{
    var content = args["content"]?.Value<string>() ?? throw new ArgumentException("content required");

    var payload = new JObject { ["content"] = content };
    var response = await server.Http.PostAsJsonAsync(
        "/api/audit/tags/scan",
        new StringContent(payload.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return $"Error scanning for tags: {error}";
    }

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var sb = new StringBuilder();
    sb.AppendLine("# Tag Detection Results");
    sb.AppendLine();

    var actionTags = result["actionTags"] as JArray ?? new JArray();
    var statusTags = result["statusTags"] as JArray ?? new JArray();
    var domainTags = result["domainTags"] as JArray ?? new JArray();
    var processingTags = result["processingTags"] as JArray ?? new JArray();
    var mentions = result["mentions"] as JArray ?? new JArray();

    if (actionTags.Count > 0)
        sb.AppendLine($"**Action Tags:** {string.Join(", ", actionTags.Select(t => t.ToString()))}");
    if (statusTags.Count > 0)
        sb.AppendLine($"**Status Tags:** {string.Join(", ", statusTags.Select(t => t.ToString()))}");
    if (domainTags.Count > 0)
        sb.AppendLine($"**Domain Tags:** {string.Join(", ", domainTags.Select(t => t.ToString()))}");
    if (processingTags.Count > 0)
        sb.AppendLine($"**Processing Tags:** {string.Join(", ", processingTags.Select(t => t.ToString()))}");
    if (mentions.Count > 0)
    {
        sb.AppendLine("**Mentions:**");
        foreach (var m in mentions)
        {
            sb.AppendLine($"  - @{m["handle"]} ({m["type"]})");
        }
    }

    if (actionTags.Count == 0 && statusTags.Count == 0 && domainTags.Count == 0 && mentions.Count == 0)
    {
        sb.AppendLine("No tags or mentions detected.");
    }

    return sb.ToString();
});

server.RegisterTool("CreateTagDetection", async (args) =>
{
    var content = args["content"]?.Value<string>() ?? throw new ArgumentException("content required");
    var source = args["source"]?.Value<string>() ?? "mcp";
    var sourceId = args["sourceId"]?.Value<string>();
    var processingTier = args["processingTier"]?.Value<string>() ?? "scheduled";
    var processImmediately = args["processImmediately"]?.Value<bool>() ?? false;

    var payload = new JObject
    {
        ["content"] = content,
        ["source"] = source,
        ["sourceId"] = sourceId,
        ["processingTier"] = processingTier,
        ["processImmediately"] = processImmediately,
        ["actorType"] = "agent"
    };

    var response = await server.Http.PostAsJsonAsync(
        "/api/audit/tags/detect",
        new StringContent(payload.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return $"Error creating detection: {error}";
    }

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    return new
    {
        message = "Detection created successfully",
        detectionId = result["detectionId"]?.Value<long>(),
        tagCount = result["tagCount"]?.Value<int>(),
        mentionCount = result["mentionCount"]?.Value<int>(),
        processingTier = result["processingTier"]?.Value<string>(),
        processed = result["processed"]?.Value<bool>()
    };
});

server.RegisterTool("GetPendingTagDetections", async (args) =>
{
    var response = await server.Http.GetAsync("/api/audit/tags/pending");

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return $"Error getting pending detections: {error}";
    }

    var detections = JArray.Parse(await response.Content.ReadAsStringAsync());

    if (detections.Count == 0)
    {
        return "No pending tag detections.";
    }

    var sb = new StringBuilder();
    sb.AppendLine("# Pending Tag Detections");
    sb.AppendLine();
    sb.AppendLine("| ID | Source | Tags | Mentions | Tier | Created |");
    sb.AppendLine("|----|--------|------|----------|------|---------|");

    foreach (var d in detections)
    {
        var tags = d["tags"]?.Value<string>() ?? "-";
        var mentions = d["mentions"]?.Value<string>() ?? "-";
        var tier = d["processingTier"]?.Value<string>() ?? "-";
        var created = d["createdAt"]?.Value<DateTime>().ToString("MM-dd HH:mm") ?? "-";

        sb.AppendLine($"| {d["id"]} | {d["source"]} | {tags} | {mentions} | {tier} | {created} |");
    }

    return sb.ToString();
});

// ----------------------------------------------------------------------
// Serva AI Marketing Tools
// ----------------------------------------------------------------------

server.RegisterTool("GetServaQueue", async (args) =>
{
    var response = await server.Http.GetAsync("/api/serva/queue");

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return $"Error getting Serva queue: {error}";
    }

    var queue = JArray.Parse(await response.Content.ReadAsStringAsync());

    if (queue.Count == 0)
    {
        return "Serva queue is empty. No items pending review.";
    }

    var sb = new StringBuilder();
    sb.AppendLine("# Serva Review Queue");
    sb.AppendLine();

    foreach (var item in queue)
    {
        sb.AppendLine($"## Item {item["id"]}");
        sb.AppendLine($"**Source:** {item["source"]} | **Priority:** {item["priority"]}");
        sb.AppendLine($"**Status:** {item["status"]} | **Created:** {item["createdAt"]}");
        sb.AppendLine();
        sb.AppendLine("**Original:**");
        sb.AppendLine($"> {item["originalContent"]}");
        sb.AppendLine();
        sb.AppendLine("**Draft Response:**");
        sb.AppendLine($"> {item["draftResponse"]}");
        sb.AppendLine();
        sb.AppendLine("---");
    }

    return sb.ToString();
});

server.RegisterTool("ApproveServaItem", async (args) =>
{
    var itemId = args["itemId"]?.Value<long>() ?? throw new ArgumentException("itemId required");
    var editedResponse = args["editedResponse"]?.Value<string>();

    var payload = new JObject();
    if (!string.IsNullOrEmpty(editedResponse))
    {
        payload["editedResponse"] = editedResponse;
    }

    var response = await server.Http.PostAsJsonAsync(
        $"/api/serva/queue/{itemId}/approve",
        new StringContent(payload.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return $"Error approving item: {error}";
    }

    return $"Item {itemId} approved and response sent.";
});

server.RegisterTool("RejectServaItem", async (args) =>
{
    var itemId = args["itemId"]?.Value<long>() ?? throw new ArgumentException("itemId required");
    var reason = args["reason"]?.Value<string>();

    var payload = new JObject { ["reason"] = reason };

    var response = await server.Http.PostAsJsonAsync(
        $"/api/serva/queue/{itemId}/reject",
        new StringContent(payload.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return $"Error rejecting item: {error}";
    }

    return $"Item {itemId} rejected.";
});

server.RegisterTool("GetServaSettings", async (args) =>
{
    var response = await server.Http.GetAsync("/api/serva/settings");

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return $"Error getting Serva settings: {error}";
    }

    var settings = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine("# Serva Settings");
    sb.AppendLine();
    sb.AppendLine($"**Automation Level:** {settings["automationLevel"]} (1=manual, 4=full auto)");
    sb.AppendLine($"**Brand Voice:** {settings["brandVoice"]}");
    sb.AppendLine($"**Language:** {settings["responseLanguage"]}");
    sb.AppendLine($"**Auto-Response Enabled:** {settings["autoResponseEnabled"]}");

    if (settings["keywords"] is JArray keywords && keywords.Count > 0)
    {
        sb.AppendLine($"**Keywords:** {string.Join(", ", keywords.Select(k => k.ToString()))}");
    }

    return sb.ToString();
});

// ----------------------------------------------------------------------
// Social Media Monitoring Tools
// ----------------------------------------------------------------------

server.RegisterTool("GetSocialMentions", async (args) =>
{
    var status = args["status"]?.Value<string>();
    var platform = args["platform"]?.Value<string>();
    var limit = args["limit"]?.Value<int>() ?? 20;

    var url = $"/api/social/monitoring/mentions?limit={limit}";
    if (!string.IsNullOrEmpty(status)) url += $"&status={status}";
    if (!string.IsNullOrEmpty(platform)) url += $"&platform={platform}";

    var response = await server.Http.GetAsync(url);

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return $"Error getting mentions: {error}";
    }

    var mentions = JArray.Parse(await response.Content.ReadAsStringAsync());

    if (mentions.Count == 0)
    {
        return "No social mentions found.";
    }

    var sb = new StringBuilder();
    sb.AppendLine("# Social Media Mentions");
    sb.AppendLine();

    foreach (var m in mentions)
    {
        var sentiment = m["sentimentScore"]?.Value<decimal>() ?? 0;
        var sentimentIcon = sentiment > 0.3m ? "😊" : sentiment < -0.3m ? "😠" : "😐";

        sb.AppendLine($"## [{m["platform"]}] @{m["authorHandle"]} {sentimentIcon}");
        sb.AppendLine($"**ID:** {m["id"]} | **Intent:** {m["detectedIntent"]} | **Status:** {m["responseStatus"]}");
        sb.AppendLine();
        sb.AppendLine($"> {m["content"]}");
        sb.AppendLine();

        if (m["draftResponse"]?.Value<string>() is { } draft && !string.IsNullOrEmpty(draft))
        {
            sb.AppendLine("**Draft Response:**");
            sb.AppendLine($"> {draft}");
            sb.AppendLine();
        }

        sb.AppendLine($"[View]({m["platformPostUrl"]})");
        sb.AppendLine();
        sb.AppendLine("---");
    }

    return sb.ToString();
});

server.RegisterTool("RespondToSocialMention", async (args) =>
{
    var mentionId = args["mentionId"]?.Value<long>() ?? throw new ArgumentException("mentionId required");
    var response_text = args["response"]?.Value<string>();
    var action = args["action"]?.Value<string>() ?? "accept"; // accept, custom, skip, auto

    HttpResponseMessage response;

    if (action == "skip")
    {
        response = await server.Http.PostAsJsonAsync(
            $"/api/social/monitoring/mentions/{mentionId}/skip",
            new StringContent("{}", Encoding.UTF8, "application/json"));
    }
    else if (action == "auto")
    {
        response = await server.Http.PostAsJsonAsync(
            $"/api/social/monitoring/mentions/{mentionId}/auto",
            new StringContent("{}", Encoding.UTF8, "application/json"));
    }
    else if (action == "custom" && !string.IsNullOrEmpty(response_text))
    {
        var payload = new JObject { ["response"] = response_text };
        response = await server.Http.PostAsJsonAsync(
            $"/api/social/monitoring/mentions/{mentionId}/respond",
            new StringContent(payload.ToString(), Encoding.UTF8, "application/json"));
    }
    else
    {
        // Accept draft
        response = await server.Http.PostAsJsonAsync(
            $"/api/social/monitoring/mentions/{mentionId}/accept",
            new StringContent("{}", Encoding.UTF8, "application/json"));
    }

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return $"Error responding to mention: {error}";
    }

    return $"Mention {mentionId} {action} successfully.";
});

server.RegisterTool("GetSocialKeywords", async (args) =>
{
    var response = await server.Http.GetAsync("/api/social/monitoring/keywords");

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return $"Error getting keywords: {error}";
    }

    var config = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine("# Social Monitoring Keywords");
    sb.AppendLine();

    if (config["keywords"] is JArray keywords && keywords.Count > 0)
    {
        sb.AppendLine("**Keywords:**");
        foreach (var k in keywords)
        {
            sb.AppendLine($"  - {k}");
        }
    }
    else
    {
        sb.AppendLine("No keywords configured.");
    }

    sb.AppendLine();

    if (config["enabledPlatforms"] is JArray platforms && platforms.Count > 0)
    {
        sb.AppendLine($"**Enabled Platforms:** {string.Join(", ", platforms.Select(p => p.ToString()))}");
    }

    if (config["excludeHandles"] is JArray excludes && excludes.Count > 0)
    {
        sb.AppendLine($"**Excluded Handles:** {string.Join(", ", excludes.Select(e => e.ToString()))}");
    }

    return sb.ToString();
});

server.RegisterTool("GetSocialStats", async (args) =>
{
    var days = args["days"]?.Value<int>() ?? 30;

    var response = await server.Http.GetAsync($"/api/social/monitoring/stats?days={days}");

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return $"Error getting social stats: {error}";
    }

    var stats = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"# Social Media Stats (Last {days} Days)");
    sb.AppendLine();
    sb.AppendLine($"**Total Mentions:** {stats["totalMentions"]}");
    sb.AppendLine($"**Responses Sent:** {stats["responsesSent"]}");
    sb.AppendLine($"**Average Response Time:** {stats["averageResponseTime"]}");
    sb.AppendLine($"**Average Sentiment:** {stats["averageSentiment"]:F2}");
    sb.AppendLine();

    if (stats["platformBreakdown"] is JObject platforms)
    {
        sb.AppendLine("**By Platform:**");
        foreach (var prop in platforms.Properties())
        {
            sb.AppendLine($"  - {prop.Name}: {prop.Value}");
        }
    }

    return sb.ToString();
});

// ----------------------------------------------------------------------
// File Tools (Local Filesystem with Tooling Auth)
// ----------------------------------------------------------------------

server.RegisterTool("served_file_find", async (args) =>
{
    var path = args["path"]?.Value<string>() ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var name = args["name"]?.Value<string>();
    var regex = args["regex"]?.Value<string>();
    var ext = args["ext"]?.Value<string>();
    var type = args["type"]?.Value<string>() ?? "f";
    var depth = args["depth"]?.Value<int>() ?? 0;
    var sizeFilter = args["size"]?.Value<string>();
    var newer = args["newer"]?.Value<string>();
    var older = args["older"]?.Value<string>();
    var contains = args["contains"]?.Value<string>();
    var allowPattern = args["allow"]?.Value<string>();
    var maxResults = args["max"]?.Value<int>() ?? 500;
    var showHidden = args["hidden"]?.Value<bool>() ?? false;
    var sort = args["sort"]?.Value<string>() ?? "date";
    var reverse = args["reverse"]?.Value<bool>() ?? false;
    var preset = args["preset"]?.Value<string>();

    // Expand ~
    if (path.StartsWith("~"))
    {
        path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..].TrimStart('/'));
    }

    if (!Directory.Exists(path))
    {
        return new { success = false, error = $"Path does not exist: {path}" };
    }

    // File type presets
    var presets = new Dictionary<string, string[]>
    {
        ["code"] = new[] { "cs", "ts", "js", "py", "go", "rs", "java", "kt", "swift", "rb", "php", "c", "cpp", "h", "hpp", "m", "mm" },
        ["web"] = new[] { "html", "htm", "css", "scss", "sass", "less", "vue", "jsx", "tsx", "svelte" },
        ["config"] = new[] { "json", "yaml", "yml", "toml", "ini", "conf", "env", "xml", "plist" },
        ["docs"] = new[] { "md", "txt", "rst", "doc", "docx", "pdf", "rtf", "odt" },
        ["media"] = new[] { "jpg", "jpeg", "png", "gif", "bmp", "svg", "mp3", "wav", "mp4", "mov", "avi", "mkv" },
        ["data"] = new[] { "csv", "tsv", "json", "xml", "sqlite", "db", "parquet", "xlsx" },
        ["archive"] = new[] { "zip", "tar", "gz", "bz2", "7z", "rar", "xz" }
    };

    if (preset != null && presets.TryGetValue(preset.ToLower(), out var presetExts))
    {
        ext = string.Join(",", presetExts);
    }

    // Build extension set
    var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (!string.IsNullOrEmpty(ext))
    {
        foreach (var e in ext.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            extensions.Add(e.TrimStart('.'));
        }
    }

    // Excluded paths for performance
    var excludedPaths = new[] { "node_modules", ".git/objects", "bin/Debug", "bin/Release", "obj/Debug", "obj/Release",
        ".nuget", ".npm", ".cache", "__pycache__", ".venv", "venv", "dist", "build", ".next", ".turbo",
        "coverage", ".pytest_cache", "DerivedData", "xcuserdata", ".Spotlight-V100", ".fseventsd" };

    // Sensitive path patterns
    var sensitivePatterns = new[] { @"\.ssh", @"\.aws", @"\.gnupg", @"Keychains", @"\.env$", @"credentials", @"secrets",
        @"Google/Chrome", @"Mozilla/Firefox", @"\.pem$", @"\.key$", @"id_rsa", @"id_ed25519" };

    var results = new List<object>();
    var scanned = 0;
    var sensitiveSkipped = 0;
    var excludedSkipped = 0;
    var allowedPatternRegex = allowPattern != null ? new System.Text.RegularExpressions.Regex(allowPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase) : null;

    // Size parser
    long? ParseSize(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        var multiplier = 1L;
        var upper = s.ToUpper();
        if (upper.EndsWith("K")) { multiplier = 1024L; s = s[..^1]; }
        else if (upper.EndsWith("M")) { multiplier = 1024L * 1024L; s = s[..^1]; }
        else if (upper.EndsWith("G")) { multiplier = 1024L * 1024L * 1024L; s = s[..^1]; }
        if (long.TryParse(s.TrimStart('+', '-'), out var val))
            return val * multiplier;
        return null;
    }

    DateTime? ParseDate(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        if (s.EndsWith("d", StringComparison.OrdinalIgnoreCase) && int.TryParse(s[..^1], out var days))
            return DateTime.Now.AddDays(-days);
        if (s.EndsWith("h", StringComparison.OrdinalIgnoreCase) && int.TryParse(s[..^1], out var hours))
            return DateTime.Now.AddHours(-hours);
        if (DateTime.TryParse(s, out var dt))
            return dt;
        return null;
    }

    var minSize = sizeFilter?.StartsWith("+") == true ? ParseSize(sizeFilter) : null;
    var maxSize = sizeFilter?.StartsWith("-") == true ? ParseSize(sizeFilter) : null;
    var newerThan = ParseDate(newer);
    var olderThan = ParseDate(older);
    var nameRegex = regex != null ? new System.Text.RegularExpressions.Regex(regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase) : null;

    void Search(string dir, int currentDepth)
    {
        if (results.Count >= maxResults) return;
        if (depth > 0 && currentDepth > depth) return;

        try
        {
            // Check exclusions
            foreach (var excl in excludedPaths)
            {
                if (dir.Contains(excl, StringComparison.OrdinalIgnoreCase))
                {
                    excludedSkipped++;
                    return;
                }
            }

            // Check sensitive
            var isSensitive = false;
            foreach (var pattern in sensitivePatterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(dir, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    isSensitive = true;
                    break;
                }
            }

            if (isSensitive && (allowedPatternRegex == null || !allowedPatternRegex.IsMatch(dir)))
            {
                sensitiveSkipped++;
                return;
            }

            // Search files
            if (type.Contains('f') || type == "all")
            {
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    if (results.Count >= maxResults) return;
                    scanned++;

                    var fileName = Path.GetFileName(file);
                    if (!showHidden && fileName.StartsWith(".")) continue;

                    // Extension filter
                    if (extensions.Count > 0)
                    {
                        var fileExt = Path.GetExtension(file).TrimStart('.');
                        if (!extensions.Contains(fileExt)) continue;
                    }

                    // Name pattern
                    if (!string.IsNullOrEmpty(name))
                    {
                        var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(name)
                            .Replace("\\*", ".*").Replace("\\?", ".") + "$";
                        if (!System.Text.RegularExpressions.Regex.IsMatch(fileName, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                            continue;
                    }

                    // Regex pattern
                    if (nameRegex != null && !nameRegex.IsMatch(fileName)) continue;

                    try
                    {
                        var info = new FileInfo(file);

                        // Size filter
                        if (minSize.HasValue && info.Length < minSize.Value) continue;
                        if (maxSize.HasValue && info.Length > maxSize.Value) continue;

                        // Date filter
                        if (newerThan.HasValue && info.LastWriteTime < newerThan.Value) continue;
                        if (olderThan.HasValue && info.LastWriteTime > olderThan.Value) continue;

                        // Content filter
                        if (!string.IsNullOrEmpty(contains))
                        {
                            try
                            {
                                var content = File.ReadAllText(file);
                                if (!content.Contains(contains, StringComparison.OrdinalIgnoreCase)) continue;
                            }
                            catch { continue; }
                        }

                        results.Add(new
                        {
                            path = file,
                            name = fileName,
                            size = info.Length,
                            sizeHuman = FormatSize(info.Length),
                            modified = info.LastWriteTime,
                            created = info.CreationTime,
                            extension = info.Extension.TrimStart('.'),
                            type = "file"
                        });
                    }
                    catch { /* skip inaccessible files */ }
                }
            }

            // Search directories
            if (type.Contains('d') || type == "all")
            {
                foreach (var subdir in Directory.EnumerateDirectories(dir))
                {
                    if (results.Count >= maxResults) return;

                    var dirName = Path.GetFileName(subdir);
                    if (!showHidden && dirName.StartsWith(".")) continue;

                    if (!string.IsNullOrEmpty(name))
                    {
                        var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(name)
                            .Replace("\\*", ".*").Replace("\\?", ".") + "$";
                        if (System.Text.RegularExpressions.Regex.IsMatch(dirName, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        {
                            results.Add(new
                            {
                                path = subdir,
                                name = dirName,
                                type = "directory"
                            });
                        }
                    }

                    Search(subdir, currentDepth + 1);
                }
            }
            else
            {
                // Recurse into subdirs even if not listing dirs
                foreach (var subdir in Directory.EnumerateDirectories(dir))
                {
                    var dirName = Path.GetFileName(subdir);
                    if (!showHidden && dirName.StartsWith(".")) continue;
                    Search(subdir, currentDepth + 1);
                }
            }
        }
        catch { /* skip inaccessible directories */ }
    }

    string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        var order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    Search(path, 0);

    // Sort results
    var sortedResults = sort.ToLower() switch
    {
        "size" => reverse ? results.OrderByDescending(r => ((dynamic)r).size) : results.OrderBy(r => ((dynamic)r).size),
        "name" => reverse ? results.OrderByDescending(r => ((dynamic)r).name) : results.OrderBy(r => ((dynamic)r).name),
        "ext" => reverse ? results.OrderByDescending(r => ((dynamic)r).extension ?? "") : results.OrderBy(r => ((dynamic)r).extension ?? ""),
        _ => reverse ? results.OrderBy(r => ((dynamic)r).modified) : results.OrderByDescending(r => ((dynamic)r).modified)
    };

    return new
    {
        success = true,
        searchPath = path,
        count = results.Count,
        scanned,
        sensitiveSkipped,
        excludedSkipped,
        results = sortedResults.ToList(),
        hint = sensitiveSkipped > 0 ? $"Use 'allow' parameter to access {sensitiveSkipped} sensitive paths" : null
    };
});

server.RegisterTool("served_file_stats", async (args) =>
{
    var path = args["path"]?.Value<string>() ?? Directory.GetCurrentDirectory();

    if (path.StartsWith("~"))
    {
        path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..].TrimStart('/'));
    }

    if (!Directory.Exists(path))
    {
        return new { success = false, error = $"Path does not exist: {path}" };
    }

    var fileCount = 0L;
    var dirCount = 0L;
    var totalSize = 0L;
    var extensionStats = new Dictionary<string, (int count, long size)>();

    void CountFiles(string dir)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                try
                {
                    var info = new FileInfo(file);
                    fileCount++;
                    totalSize += info.Length;

                    var ext = info.Extension.TrimStart('.').ToLowerInvariant();
                    if (string.IsNullOrEmpty(ext)) ext = "(no extension)";

                    if (extensionStats.TryGetValue(ext, out var stat))
                    {
                        extensionStats[ext] = (stat.count + 1, stat.size + info.Length);
                    }
                    else
                    {
                        extensionStats[ext] = (1, info.Length);
                    }
                }
                catch { }
            }

            foreach (var subdir in Directory.EnumerateDirectories(dir))
            {
                dirCount++;
                CountFiles(subdir);
            }
        }
        catch { }
    }

    CountFiles(path);

    string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        var order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    var topExtensions = extensionStats
        .OrderByDescending(x => x.Value.size)
        .Take(15)
        .Select(x => new
        {
            extension = x.Key,
            count = x.Value.count,
            totalSize = x.Value.size,
            totalSizeHuman = FormatSize(x.Value.size)
        })
        .ToList();

    return new
    {
        success = true,
        path,
        fileCount,
        directoryCount = dirCount,
        totalSize,
        totalSizeHuman = FormatSize(totalSize),
        topExtensionsBySize = topExtensions
    };
});

server.RegisterTool("served_file_duplicates", async (args) =>
{
    var path = args["path"]?.Value<string>() ?? Directory.GetCurrentDirectory();
    var minSize = args["minSize"]?.Value<long>() ?? 1024; // Default 1KB
    var maxResults = args["max"]?.Value<int>() ?? 100;

    if (path.StartsWith("~"))
    {
        path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..].TrimStart('/'));
    }

    if (!Directory.Exists(path))
    {
        return new { success = false, error = $"Path does not exist: {path}" };
    }

    var filesBySize = new Dictionary<long, List<string>>();

    void GatherFiles(string dir)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                try
                {
                    var info = new FileInfo(file);
                    if (info.Length < minSize) continue;

                    if (!filesBySize.TryGetValue(info.Length, out var list))
                    {
                        list = new List<string>();
                        filesBySize[info.Length] = list;
                    }
                    list.Add(file);
                }
                catch { }
            }

            foreach (var subdir in Directory.EnumerateDirectories(dir))
            {
                var name = Path.GetFileName(subdir);
                if (name.StartsWith(".") || name == "node_modules" || name == ".git") continue;
                GatherFiles(subdir);
            }
        }
        catch { }
    }

    GatherFiles(path);

    // Find duplicates by hash
    var duplicateGroups = new List<object>();
    var potentialDups = filesBySize.Where(x => x.Value.Count > 1);

    foreach (var group in potentialDups)
    {
        var hashGroups = new Dictionary<string, List<string>>();

        foreach (var file in group.Value)
        {
            try
            {
                using var stream = File.OpenRead(file);
                using var sha = System.Security.Cryptography.SHA256.Create();
                var hash = Convert.ToHexString(sha.ComputeHash(stream))[..16];

                if (!hashGroups.TryGetValue(hash, out var files))
                {
                    files = new List<string>();
                    hashGroups[hash] = files;
                }
                files.Add(file);
            }
            catch { }
        }

        foreach (var hashGroup in hashGroups.Where(x => x.Value.Count > 1))
        {
            if (duplicateGroups.Count >= maxResults) break;

            string FormatSize(long bytes)
            {
                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                var order = 0;
                double size = bytes;
                while (size >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    size /= 1024;
                }
                return $"{size:0.##} {sizes[order]}";
            }

            duplicateGroups.Add(new
            {
                hash = hashGroup.Key,
                size = group.Key,
                sizeHuman = FormatSize(group.Key),
                count = hashGroup.Value.Count,
                wastedSpace = group.Key * (hashGroup.Value.Count - 1),
                wastedSpaceHuman = FormatSize(group.Key * (hashGroup.Value.Count - 1)),
                files = hashGroup.Value
            });
        }
    }

    var totalWasted = duplicateGroups.Sum(g => (long)((dynamic)g).wastedSpace);

    string FormatTotalSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        var order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    return new
    {
        success = true,
        searchPath = path,
        duplicateGroupsFound = duplicateGroups.Count,
        totalWastedSpace = totalWasted,
        totalWastedSpaceHuman = FormatTotalSize(totalWasted),
        duplicates = duplicateGroups.OrderByDescending(g => ((dynamic)g).wastedSpace).ToList()
    };
});

server.RegisterTool("served_file_auth_status", async (args) =>
{
    var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".served");
    var configPath = Path.Combine(configDir, "tooling-auth.json");

    if (!File.Exists(configPath))
    {
        return new
        {
            success = true,
            enabled = true,
            message = "Tooling auth is enabled with default settings",
            configPath,
            allowedPaths = new List<object>(),
            sensitivePatterns = new[] { ".ssh", ".aws", ".gnupg", "Keychains", ".env", "credentials", "secrets" }
        };
    }

    var config = JObject.Parse(await File.ReadAllTextAsync(configPath));

    return new
    {
        success = true,
        enabled = config["Enabled"]?.Value<bool>() ?? true,
        requireConfirmation = config["RequireConfirmation"]?.Value<bool>() ?? true,
        auditLogging = config["AuditLogging"]?.Value<bool>() ?? true,
        configPath,
        allowedPaths = config["AllowedPaths"] ?? new JArray(),
        customSensitivePatterns = config["CustomSensitivePatterns"] ?? new JArray()
    };
});

server.RegisterTool("served_file_auth_allow", async (args) =>
{
    var pattern = args["pattern"]?.Value<string>() ?? throw new ArgumentException("pattern required");
    var reason = args["reason"]?.Value<string>() ?? "MCP tool access";
    var days = args["days"]?.Value<int>() ?? 1;

    var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".served");
    var configPath = Path.Combine(configDir, "tooling-auth.json");

    if (!Directory.Exists(configDir))
        Directory.CreateDirectory(configDir);

    JObject config;
    if (File.Exists(configPath))
    {
        config = JObject.Parse(await File.ReadAllTextAsync(configPath));
    }
    else
    {
        config = new JObject
        {
            ["Enabled"] = true,
            ["RequireConfirmation"] = true,
            ["AuditLogging"] = true,
            ["DefaultExpirationDays"] = 0,
            ["AllowedPaths"] = new JArray(),
            ["CustomSensitivePatterns"] = new JArray(),
            ["ExcludedPaths"] = new JArray()
        };
    }

    var allowedPaths = config["AllowedPaths"] as JArray ?? new JArray();

    // Remove existing entry for same pattern
    var existing = allowedPaths.FirstOrDefault(x => x["Pattern"]?.Value<string>() == pattern);
    if (existing != null)
        allowedPaths.Remove(existing);

    var entry = new JObject
    {
        ["Pattern"] = pattern,
        ["Reason"] = reason,
        ["AllowedAt"] = DateTime.UtcNow,
        ["ExpiresAt"] = days > 0 ? DateTime.UtcNow.AddDays(days) : null,
        ["AllowedBy"] = Environment.UserName
    };

    allowedPaths.Add(entry);
    config["AllowedPaths"] = allowedPaths;

    await File.WriteAllTextAsync(configPath, config.ToString());

    // Log to audit
    var auditPath = Path.Combine(configDir, "tooling-audit.log");
    var auditEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ALLOW | {pattern} | {reason} | User: {Environment.UserName}\n";
    await File.AppendAllTextAsync(auditPath, auditEntry);

    return new
    {
        success = true,
        message = $"Pattern '{pattern}' allowed for {days} day(s)",
        pattern,
        reason,
        expiresAt = days > 0 ? DateTime.UtcNow.AddDays(days).ToString("yyyy-MM-dd HH:mm:ss") : "Never"
    };
});

server.RegisterTool("served_file_tree", async (args) =>
{
    var path = args["path"]?.Value<string>() ?? Directory.GetCurrentDirectory();
    var depth = args["depth"]?.Value<int>() ?? 3;
    var showHidden = args["hidden"]?.Value<bool>() ?? false;
    var showFiles = args["files"]?.Value<bool>() ?? true;

    if (path.StartsWith("~"))
    {
        path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..].TrimStart('/'));
    }

    if (!Directory.Exists(path))
    {
        return new { success = false, error = $"Path does not exist: {path}" };
    }

    var tree = new StringBuilder();
    tree.AppendLine(Path.GetFileName(path) + "/");

    void BuildTree(string dir, string prefix, int currentDepth)
    {
        if (currentDepth > depth) return;

        try
        {
            var entries = new List<(string path, bool isDir)>();

            foreach (var subdir in Directory.EnumerateDirectories(dir))
            {
                var name = Path.GetFileName(subdir);
                if (!showHidden && name.StartsWith(".")) continue;
                if (name == "node_modules" || name == ".git") continue;
                entries.Add((subdir, true));
            }

            if (showFiles)
            {
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    var name = Path.GetFileName(file);
                    if (!showHidden && name.StartsWith(".")) continue;
                    entries.Add((file, false));
                }
            }

            entries = entries.OrderBy(e => !e.isDir).ThenBy(e => Path.GetFileName(e.path)).ToList();

            for (var i = 0; i < entries.Count; i++)
            {
                var (entryPath, isDir) = entries[i];
                var isLast = i == entries.Count - 1;
                var name = Path.GetFileName(entryPath);
                var connector = isLast ? "└── " : "├── ";
                var extension = isDir ? "/" : "";

                tree.AppendLine($"{prefix}{connector}{name}{extension}");

                if (isDir)
                {
                    var newPrefix = prefix + (isLast ? "    " : "│   ");
                    BuildTree(entryPath, newPrefix, currentDepth + 1);
                }
            }
        }
        catch { }
    }

    BuildTree(path, "", 1);

    return new
    {
        success = true,
        path,
        depth,
        tree = tree.ToString()
    };
});

// ----------------------------------------------------------------------
// Social Monitoring Tools
// ----------------------------------------------------------------------

server.RegisterTool("GetSocialMentions", async (args) =>
{
    var status = args["status"]?.Value<string>();
    var platform = args["platform"]?.Value<string>();
    var limit = args["limit"]?.Value<int>() ?? 20;

    var queryParams = new List<string> { $"limit={limit}" };
    if (!string.IsNullOrEmpty(status))
        queryParams.Add($"status={status}");
    if (!string.IsNullOrEmpty(platform))
        queryParams.Add($"platform={platform}");

    var response = await server.Http.GetAsync($"/api/social/monitoring/mentions?{string.Join("&", queryParams)}");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var mentions = JArray.Parse(content);

    var sb = new StringBuilder();
    sb.AppendLine($"📱 Social Mentions ({mentions.Count} found)");
    sb.AppendLine(new string('─', 50));

    foreach (var mention in mentions)
    {
        var mentionPlatform = mention["platform"]?.Value<string>() ?? "";
        var icon = mentionPlatform switch
        {
            "twitter" => "🐦",
            "linkedin" => "💼",
            "youtube" => "▶️",
            "reddit" => "🔴",
            _ => "📢"
        };

        sb.AppendLine($"{icon} [{mentionPlatform.ToUpper()}] @{mention["authorHandle"]}");
        sb.AppendLine($"   Status: {mention["responseStatus"]} | Intent: {mention["detectedIntent"]}");
        sb.AppendLine($"   Content: {TruncateText(mention["content"]?.Value<string>(), 100)}");
        if (mention["draftResponse"] != null && !string.IsNullOrEmpty(mention["draftResponse"]?.Value<string>()))
        {
            sb.AppendLine($"   Draft: {TruncateText(mention["draftResponse"]?.Value<string>(), 80)}");
        }
        sb.AppendLine($"   ID: {mention["id"]} | Posted: {mention["postedAt"]}");
        sb.AppendLine();
    }

    return sb.ToString();

    static string TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";
    }
});

server.RegisterTool("GetSocialMentionDetails", async (args) =>
{
    var mentionId = args["mentionId"]?.Value<long>() ?? throw new ArgumentException("mentionId required");

    var response = await server.Http.GetAsync($"/api/social/monitoring/mentions/{mentionId}");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    return JObject.Parse(content);
});

server.RegisterTool("RespondToMention", async (args) =>
{
    var mentionId = args["mentionId"]?.Value<long>() ?? throw new ArgumentException("mentionId required");
    var responseText = args["response"]?.Value<string>() ?? throw new ArgumentException("response required");

    var payload = new JObject { ["response"] = responseText };
    var httpContent = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

    var response = await server.Http.PostAsJsonAsync($"/api/social/monitoring/mentions/{mentionId}/respond", httpContent);
    response.EnsureSuccessStatusCode();

    return new { success = true, message = $"Response sent to mention {mentionId}" };
});

server.RegisterTool("AcceptMentionDraft", async (args) =>
{
    var mentionId = args["mentionId"]?.Value<long>() ?? throw new ArgumentException("mentionId required");

    var response = await server.Http.PostAsync($"/api/social/monitoring/mentions/{mentionId}/accept", null);
    response.EnsureSuccessStatusCode();

    return new { success = true, message = $"Draft response accepted and sent for mention {mentionId}" };
});

server.RegisterTool("SkipMention", async (args) =>
{
    var mentionId = args["mentionId"]?.Value<long>() ?? throw new ArgumentException("mentionId required");

    var response = await server.Http.PostAsync($"/api/social/monitoring/mentions/{mentionId}/skip", null);
    response.EnsureSuccessStatusCode();

    return new { success = true, message = $"Mention {mentionId} marked as skipped" };
});

server.RegisterTool("GetSocialKeywords", async (args) =>
{
    var response = await server.Http.GetAsync("/api/social/monitoring/keywords");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    return JObject.Parse(content);
});

server.RegisterTool("GetSocialStats", async (args) =>
{
    var days = args["days"]?.Value<int>() ?? 30;

    var response = await server.Http.GetAsync($"/api/social/monitoring/stats?days={days}");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var stats = JObject.Parse(content);

    var sb = new StringBuilder();
    sb.AppendLine("📊 Social Monitoring Stats");
    sb.AppendLine(new string('─', 40));
    sb.AppendLine($"Period: Last {days} days");
    sb.AppendLine();
    sb.AppendLine($"Total Mentions: {stats["totalMentions"]}");
    sb.AppendLine($"Responded: {stats["respondedCount"]}");
    sb.AppendLine($"Pending: {stats["pendingCount"]}");
    sb.AppendLine($"Skipped: {stats["skippedCount"]}");
    sb.AppendLine();
    sb.AppendLine("By Platform:");

    if (stats["byPlatform"] is JObject platforms)
    {
        foreach (var prop in platforms.Properties())
        {
            sb.AppendLine($"  {prop.Name}: {prop.Value}");
        }
    }

    return sb.ToString();
});

// ----------------------------------------------------------------------
// Serva Queue Tools
// ----------------------------------------------------------------------

server.RegisterTool("GetServaQueue", async (args) =>
{
    var response = await server.Http.GetAsync("/api/serva/queue");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var queue = JArray.Parse(content);

    var sb = new StringBuilder();
    sb.AppendLine($"🤖 Serva Queue ({queue.Count} pending items)");
    sb.AppendLine(new string('─', 50));

    foreach (var item in queue)
    {
        sb.AppendLine($"[{item["source"]}] {item["type"]}");
        sb.AppendLine($"  Content: {item["content"]?.Value<string>()?[..Math.Min(100, item["content"]?.Value<string>()?.Length ?? 0)]}...");
        sb.AppendLine($"  ID: {item["id"]} | Created: {item["createdAt"]}");
        sb.AppendLine();
    }

    return sb.ToString();
});

server.RegisterTool("GetServaSettings", async (args) =>
{
    var response = await server.Http.GetAsync("/api/serva/settings");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    return JObject.Parse(content);
});

server.RegisterTool("SetServaAutomationLevel", async (args) =>
{
    var level = args["level"]?.Value<int>() ?? throw new ArgumentException("level required (1-4)");
    if (level < 1 || level > 4)
        throw new ArgumentException("Level must be between 1 and 4");

    var httpContent = new StringContent(level.ToString(), Encoding.UTF8, "application/json");
    var response = await server.Http.PutAsync("/api/serva/automation-level", httpContent);
    response.EnsureSuccessStatusCode();

    var descriptions = new Dictionary<int, string>
    {
        { 1, "Manual - All responses require approval" },
        { 2, "Assisted - Draft responses generated, approval required" },
        { 3, "Semi-Auto - Low-risk responses sent automatically" },
        { 4, "Full Auto - All responses handled automatically" }
    };

    return new { success = true, level, description = descriptions[level] };
});

// ----------------------------------------------------------------------
// Tool Usage Analytics
// ----------------------------------------------------------------------

server.RegisterTool("GetToolUsageStats", async (args) =>
{
    var days = args["days"]?.Value<int>() ?? 7;

    var response = await server.Http.GetAsync($"/api/analytics/tools/usage?days={days}");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var stats = JObject.Parse(content);

    var sb = new StringBuilder();
    sb.AppendLine("📈 Tool Usage Statistics");
    sb.AppendLine(new string('─', 40));
    sb.AppendLine($"Period: Last {days} days");
    sb.AppendLine();
    sb.AppendLine($"Total Events: {stats["totalEvents"]}");
    sb.AppendLine($"CLI Commands: {stats["cliCommands"]}");
    sb.AppendLine($"MCP Tool Calls: {stats["mcpToolCalls"]}");
    sb.AppendLine($"Agent Sessions: {stats["agentSessions"]}");
    sb.AppendLine($"Total Cost: {stats["totalCost"]:C}");
    sb.AppendLine();

    if (stats["successRate"] != null)
    {
        sb.AppendLine($"Success Rate: {stats["successRate"]:P1}");
    }
    if (stats["avgDurationMs"] != null)
    {
        sb.AppendLine($"Avg Duration: {stats["avgDurationMs"]}ms");
    }

    return sb.ToString();
});

server.RegisterTool("GetTopCliCommands", async (args) =>
{
    var days = args["days"]?.Value<int>() ?? 7;
    var limit = args["limit"]?.Value<int>() ?? 10;

    var response = await server.Http.GetAsync($"/api/analytics/tools/cli/commands?days={days}&limit={limit}");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var commands = JArray.Parse(content);

    var sb = new StringBuilder();
    sb.AppendLine("🔧 Top CLI Commands");
    sb.AppendLine(new string('─', 40));

    var rank = 1;
    foreach (var cmd in commands)
    {
        sb.AppendLine($"{rank}. {cmd["command"]} ({cmd["count"]} calls)");
        if (cmd["avgDurationMs"] != null)
        {
            sb.AppendLine($"   Avg: {cmd["avgDurationMs"]}ms | Success: {cmd["successRate"]:P0}");
        }
        rank++;
    }

    return sb.ToString();
});

server.RegisterTool("GetTopMcpTools", async (args) =>
{
    var days = args["days"]?.Value<int>() ?? 7;
    var limit = args["limit"]?.Value<int>() ?? 10;

    var response = await server.Http.GetAsync($"/api/analytics/tools/mcp/tools?days={days}&limit={limit}");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var tools = JArray.Parse(content);

    var sb = new StringBuilder();
    sb.AppendLine("🛠️ Top MCP Tools");
    sb.AppendLine(new string('─', 40));

    var rank = 1;
    foreach (var tool in tools)
    {
        sb.AppendLine($"{rank}. {tool["toolName"]} ({tool["count"]} calls)");
        if (tool["avgDurationMs"] != null)
        {
            sb.AppendLine($"   Avg: {tool["avgDurationMs"]}ms | Success: {tool["successRate"]:P0}");
        }
        rank++;
    }

    return sb.ToString();
});

server.RegisterTool("GetAgentSessions", async (args) =>
{
    var days = args["days"]?.Value<int>() ?? 7;
    var limit = args["limit"]?.Value<int>() ?? 20;

    var response = await server.Http.GetAsync($"/api/analytics/tools/agents/sessions?days={days}&limit={limit}");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var sessions = JArray.Parse(content);

    var sb = new StringBuilder();
    sb.AppendLine("🤖 Agent Sessions");
    sb.AppendLine(new string('─', 40));

    foreach (var session in sessions)
    {
        sb.AppendLine($"Session: {session["sessionId"]}");
        sb.AppendLine($"  Agent: {session["agentId"]} | Turns: {session["conversationTurns"]}");
        sb.AppendLine($"  Duration: {session["totalDurationMs"]}ms | Tools: {session["toolCallCount"]}");
        sb.AppendLine($"  Started: {session["startedAt"]}");
        sb.AppendLine();
    }

    return sb.ToString();
});

// ----------------------------------------------------------------------
// Tag Detection Tools
// ----------------------------------------------------------------------

server.RegisterTool("ScanForTags", async (args) =>
{
    var content = args["content"]?.Value<string>() ?? throw new ArgumentException("content required");

    var payload = new JObject { ["content"] = content };
    var httpContent = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

    var response = await server.Http.PostAsJsonAsync("/api/audit/tags/scan", httpContent);
    response.EnsureSuccessStatusCode();

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine("🏷️ Tag Detection Results");
    sb.AppendLine(new string('─', 40));

    if (result["actionTags"] is JArray actionTags && actionTags.Count > 0)
    {
        sb.AppendLine($"Action Tags: {string.Join(", ", actionTags)}");
    }
    if (result["statusTags"] is JArray statusTags && statusTags.Count > 0)
    {
        sb.AppendLine($"Status Tags: {string.Join(", ", statusTags)}");
    }
    if (result["domainTags"] is JArray domainTags && domainTags.Count > 0)
    {
        sb.AppendLine($"Domain Tags: {string.Join(", ", domainTags)}");
    }
    if (result["processingTags"] is JArray processingTags && processingTags.Count > 0)
    {
        sb.AppendLine($"Processing Tags: {string.Join(", ", processingTags)}");
    }
    if (result["mentions"] is JArray mentions && mentions.Count > 0)
    {
        sb.AppendLine($"Mentions: {string.Join(", ", mentions.Select(m => m["handle"]))}");
    }

    return sb.ToString();
});

server.RegisterTool("GetPendingTagDetections", async (args) =>
{
    var response = await server.Http.GetAsync("/api/audit/tags/pending");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var detections = JArray.Parse(content);

    var sb = new StringBuilder();
    sb.AppendLine($"🏷️ Pending Tag Detections ({detections.Count})");
    sb.AppendLine(new string('─', 40));

    foreach (var det in detections)
    {
        sb.AppendLine($"[{det["source"]}] {det["primaryTag"]}");
        sb.AppendLine($"  Tags: {det["tags"]}");
        sb.AppendLine($"  ID: {det["id"]} | Created: {det["createdDate"]}");
        sb.AppendLine();
    }

    return sb.ToString();
});

// ----------------------------------------------------------------------
// Databricks Export Tools
// ----------------------------------------------------------------------

server.RegisterTool("GetDatabricksExportStatus", async (args) =>
{
    var response = await server.Http.GetAsync("/api/analytics/databricks/status");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var status = JObject.Parse(content);

    var sb = new StringBuilder();
    sb.AppendLine("📊 Databricks Export Status");
    sb.AppendLine(new string('─', 40));
    sb.AppendLine($"Pending Tool Usage: {status["pendingToolUsageEvents"]}");
    sb.AppendLine($"Pending Tag Detections: {status["pendingTagDetections"]}");
    sb.AppendLine($"Pending Social Mentions: {status["pendingSocialMentions"]}");
    sb.AppendLine();
    sb.AppendLine($"Last Tool Usage Export: {status["lastToolUsageExport"] ?? "Never"}");
    sb.AppendLine($"Last Tag Detection Export: {status["lastTagDetectionExport"] ?? "Never"}");
    sb.AppendLine($"Last Social Mention Export: {status["lastSocialMentionExport"] ?? "Never"}");
    sb.AppendLine();
    sb.AppendLine($"Total Exported Records: {status["totalExportedRecords"]}");

    return sb.ToString();
});

server.RegisterTool("TriggerDatabricksExport", async (args) =>
{
    var type = args["type"]?.Value<string>() ?? "all";

    var endpoint = type.ToLower() switch
    {
        "tool-usage" => "/api/analytics/databricks/export/tool-usage",
        "tag-detections" => "/api/analytics/databricks/export/tag-detections",
        "social-mentions" => "/api/analytics/databricks/export/social-mentions",
        _ => "/api/analytics/databricks/export/all"
    };

    var response = await server.Http.PostAsync(endpoint, null);
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var result = JObject.Parse(content);

    var sb = new StringBuilder();
    sb.AppendLine("✅ Databricks Export Triggered");
    sb.AppendLine(new string('─', 40));

    if (type == "all")
    {
        sb.AppendLine($"Total Records: {result["totalRecordsExported"]}");
        sb.AppendLine($"Total Files: {result["totalFilesCreated"]}");
        sb.AppendLine($"All Successful: {result["allSuccessful"]}");
    }
    else
    {
        sb.AppendLine($"Records Exported: {result["recordsExported"]}");
        sb.AppendLine($"Files Created: {result["filesCreated"]}");
        sb.AppendLine($"Success: {result["success"]}");
    }

    return sb.ToString();
});

server.RegisterTool("GetDatabricksSchema", async (args) =>
{
    var catalog = args["catalog"]?.Value<string>() ?? "served_analytics";

    var response = await server.Http.GetAsync($"/api/analytics/databricks/schema?catalog={catalog}");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var result = JObject.Parse(content);

    return result["schema"]?.Value<string>() ?? "No schema available";
});

// ----------------------------------------------------------------------
// Processing Tier & Billing Tools
// ----------------------------------------------------------------------

server.RegisterTool("GetProcessingTiers", async (args) =>
{
    var response = await server.Http.GetAsync("/api/finance/processing/tiers");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var tiers = JArray.Parse(content);

    var sb = new StringBuilder();
    sb.AppendLine("💰 Processing Tiers");
    sb.AppendLine(new string('─', 40));

    foreach (var tier in tiers)
    {
        sb.AppendLine($"📦 {tier["name"]} ({tier["id"]})");
        sb.AppendLine($"   {tier["description"]}");
        sb.AppendLine($"   Processing: {tier["processingSpeed"]}");
        sb.AppendLine();
    }

    return sb.ToString();
});

server.RegisterTool("GetSubscriptionPlans", async (args) =>
{
    var response = await server.Http.GetAsync("/api/finance/processing/plans");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var plans = JArray.Parse(content);

    var sb = new StringBuilder();
    sb.AppendLine("📋 Subscription Plans");
    sb.AppendLine(new string('─', 40));

    foreach (var plan in plans)
    {
        sb.AppendLine($"🎯 {plan["name"]} ({plan["id"]})");
        sb.AppendLine($"   Monthly: {plan["priceMonthly"]} DKK");
        sb.AppendLine($"   Yearly: {plan["priceYearly"]} DKK (save {plan["yearlyDiscount"]}%)");
        sb.AppendLine($"   Scheduled Quota: {plan["scheduledQuota"]}");
        sb.AppendLine($"   Instant Quota: {plan["instantQuota"]}");
        sb.AppendLine($"   Social Quota: {plan["socialResponseQuota"]}");
        sb.AppendLine();
    }

    return sb.ToString();
});

server.RegisterTool("GetCurrentSubscription", async (args) =>
{
    var response = await server.Http.GetAsync("/api/finance/processing/subscription");

    if (!response.IsSuccessStatusCode)
    {
        return "No active subscription found";
    }

    var content = await response.Content.ReadAsStringAsync();
    var sub = JObject.Parse(content);

    var sb = new StringBuilder();
    sb.AppendLine("🔐 Current Subscription");
    sb.AppendLine(new string('─', 40));
    sb.AppendLine($"Plan: {sub["planId"]}");
    sb.AppendLine($"Status: {sub["status"]}");
    sb.AppendLine($"Period: {sub["currentPeriodStart"]} - {sub["currentPeriodEnd"]}");
    sb.AppendLine();
    sb.AppendLine("Usage:");
    sb.AppendLine($"  Scheduled: {sub["scheduledUsed"]}/{sub["scheduledQuota"]}");
    sb.AppendLine($"  Instant: {sub["instantUsed"]}/{sub["instantQuota"]}");
    sb.AppendLine($"  Social: {sub["socialUsed"]}/{sub["socialQuota"]}");

    return sb.ToString();
});

server.RegisterTool("CheckQuota", async (args) =>
{
    var tier = args["tier"]?.Value<string>() ?? "scheduled";

    var response = await server.Http.GetAsync($"/api/finance/processing/quota/{tier}");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var result = JObject.Parse(content);

    var sb = new StringBuilder();
    sb.AppendLine($"📊 Quota Check: {tier}");
    sb.AppendLine(new string('─', 40));
    sb.AppendLine($"Allowed: {result["allowed"]}");
    sb.AppendLine($"Used: {result["used"]} / {result["quota"]}");
    sb.AppendLine($"Remaining: {result["remaining"]}");

    if (result["allowed"]?.Value<bool>() == false)
    {
        sb.AppendLine();
        sb.AppendLine($"⚠️ Reason: {result["reason"]}");
    }

    return sb.ToString();
});

server.RegisterTool("GetBillingSummary", async (args) =>
{
    var response = await server.Http.GetAsync("/api/finance/processing/billing/summary");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var summary = JObject.Parse(content);

    var sb = new StringBuilder();
    sb.AppendLine("💳 Billing Summary");
    sb.AppendLine(new string('─', 40));
    sb.AppendLine($"Period: {summary["periodStart"]} - {summary["periodEnd"]}");
    sb.AppendLine();
    sb.AppendLine($"Subscription: {summary["subscriptionCost"]} DKK");
    sb.AppendLine($"Usage Overage: {summary["overageCost"]} DKK");
    sb.AppendLine($"Total: {summary["totalCost"]} DKK");

    if (summary["lineItems"] is JArray items && items.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine("Line Items:");
        foreach (var item in items)
        {
            sb.AppendLine($"  {item["description"]}: {item["amount"]} DKK");
        }
    }

    return sb.ToString();
});

// ═══════════════════════════════════════════════════════════════════════════════════
// CLOUDFLARE INFRASTRUCTURE MANAGEMENT
// DNS, Load Balancing, Tunnels, and AI-Powered Routing
// ═══════════════════════════════════════════════════════════════════════════════════

// Cloudflare API client setup (uses ~/.served/config.json)
HttpClient CreateCloudflareClient()
{
    var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".served", "config.json");
    if (!File.Exists(configPath))
        throw new Exception("Served config not found. Run 'served cloudflare setup' first.");

    var configJson = File.ReadAllText(configPath);
    var config = JObject.Parse(configJson);
    var cfConfig = config["Cloudflare"] as JObject;

    if (cfConfig == null || string.IsNullOrEmpty(cfConfig["ApiToken"]?.ToString()))
        throw new Exception("Cloudflare not configured. Run 'served cloudflare setup' first.");

    var client = new HttpClient { BaseAddress = new Uri("https://api.cloudflare.com/client/v4/") };
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cfConfig["ApiToken"]}");
    return client;
}

server.RegisterTool("CloudflareGetZones", async (args) =>
{
    using var cfClient = CreateCloudflareClient();
    var response = await cfClient.GetAsync("zones?per_page=50");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var result = JObject.Parse(content);
    var zones = result["result"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine("☁️ Cloudflare DNS Zones");
    sb.AppendLine(new string('═', 50));

    if (zones.Count == 0)
    {
        sb.AppendLine("No zones found.");
        return sb.ToString();
    }

    foreach (var zone in zones)
    {
        var status = zone["status"]?.ToString() ?? "unknown";
        var icon = status == "active" ? "✅" : "⏳";
        var paused = zone["paused"]?.Value<bool>() == true ? " (paused)" : "";

        sb.AppendLine($"{icon} {zone["name"]}{paused}");
        sb.AppendLine($"   ID: {zone["id"]}");
        sb.AppendLine($"   Status: {status}");
        sb.AppendLine($"   Plan: {zone["plan"]?["name"]}");
        sb.AppendLine($"   Name Servers: {string.Join(", ", (zone["name_servers"] as JArray ?? new JArray()).Select(n => n.ToString()))}");
        sb.AppendLine();
    }

    return sb.ToString();
});

server.RegisterTool("CloudflareGetDnsRecords", async (args) =>
{
    var zoneId = args["zoneId"]?.Value<string>();
    var zoneName = args["zoneName"]?.Value<string>();
    var recordType = args["type"]?.Value<string>(); // Optional filter

    if (string.IsNullOrEmpty(zoneId) && string.IsNullOrEmpty(zoneName))
        throw new Exception("Either zoneId or zoneName is required");

    using var cfClient = CreateCloudflareClient();

    // If zoneName provided, look up zone ID
    if (string.IsNullOrEmpty(zoneId))
    {
        var zonesResponse = await cfClient.GetAsync($"zones?name={zoneName}");
        zonesResponse.EnsureSuccessStatusCode();
        var zonesResult = JObject.Parse(await zonesResponse.Content.ReadAsStringAsync());
        var zones = zonesResult["result"] as JArray;
        if (zones == null || zones.Count == 0)
            throw new Exception($"Zone '{zoneName}' not found");
        zoneId = zones[0]["id"]?.ToString();
    }

    var url = $"zones/{zoneId}/dns_records?per_page=100";
    if (!string.IsNullOrEmpty(recordType))
        url += $"&type={recordType}";

    var response = await cfClient.GetAsync(url);
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var result = JObject.Parse(content);
    var records = result["result"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine("📋 DNS Records");
    sb.AppendLine(new string('═', 60));

    var grouped = records.GroupBy(r => r["type"]?.ToString() ?? "UNKNOWN");
    foreach (var group in grouped.OrderBy(g => g.Key))
    {
        sb.AppendLine();
        sb.AppendLine($"## {group.Key} Records ({group.Count()})");
        sb.AppendLine(new string('─', 40));

        foreach (var record in group)
        {
            var proxied = record["proxied"]?.Value<bool>() == true ? " 🔶" : " ⚪";
            var ttl = record["ttl"]?.Value<int>() == 1 ? "Auto" : $"{record["ttl"]}s";

            sb.AppendLine($"  {record["name"]}{proxied}");
            sb.AppendLine($"    → {record["content"]}");
            sb.AppendLine($"    TTL: {ttl} | ID: {record["id"]}");
        }
    }

    return sb.ToString();
});

server.RegisterTool("CloudflareCreateDnsRecord", async (args) =>
{
    var zoneId = args["zoneId"]?.Value<string>();
    var zoneName = args["zoneName"]?.Value<string>();
    var name = args["name"]?.Value<string>() ?? throw new Exception("name is required");
    var type = args["type"]?.Value<string>() ?? "A";
    var content = args["content"]?.Value<string>() ?? throw new Exception("content is required");
    var ttl = args["ttl"]?.Value<int>() ?? 1; // 1 = Auto
    var proxied = args["proxied"]?.Value<bool>() ?? true;

    if (string.IsNullOrEmpty(zoneId) && string.IsNullOrEmpty(zoneName))
        throw new Exception("Either zoneId or zoneName is required");

    using var cfClient = CreateCloudflareClient();

    // If zoneName provided, look up zone ID
    if (string.IsNullOrEmpty(zoneId))
    {
        var zonesResponse = await cfClient.GetAsync($"zones?name={zoneName}");
        zonesResponse.EnsureSuccessStatusCode();
        var zonesResult = JObject.Parse(await zonesResponse.Content.ReadAsStringAsync());
        var zones = zonesResult["result"] as JArray;
        if (zones == null || zones.Count == 0)
            throw new Exception($"Zone '{zoneName}' not found");
        zoneId = zones[0]["id"]?.ToString();
    }

    var record = new JObject
    {
        ["type"] = type.ToUpper(),
        ["name"] = name,
        ["content"] = content,
        ["ttl"] = ttl,
        ["proxied"] = proxied
    };

    var requestContent = new StringContent(record.ToString(), Encoding.UTF8, "application/json");
    var response = await cfClient.PostAsync($"zones/{zoneId}/dns_records", requestContent);
    var responseContent = await response.Content.ReadAsStringAsync();
    var result = JObject.Parse(responseContent);

    if (result["success"]?.Value<bool>() != true)
    {
        var errors = result["errors"] as JArray;
        var errorMsg = errors?.FirstOrDefault()?["message"]?.ToString() ?? "Unknown error";
        throw new Exception($"Failed to create DNS record: {errorMsg}");
    }

    var created = result["result"];
    return $"✅ DNS record created:\n   {created?["name"]} ({created?["type"]}) → {created?["content"]}\n   ID: {created?["id"]}";
});

server.RegisterTool("CloudflareDeleteDnsRecord", async (args) =>
{
    var zoneId = args["zoneId"]?.Value<string>() ?? throw new Exception("zoneId is required");
    var recordId = args["recordId"]?.Value<string>() ?? throw new Exception("recordId is required");

    using var cfClient = CreateCloudflareClient();
    var response = await cfClient.DeleteAsync($"zones/{zoneId}/dns_records/{recordId}");
    var responseContent = await response.Content.ReadAsStringAsync();
    var result = JObject.Parse(responseContent);

    if (result["success"]?.Value<bool>() != true)
    {
        var errors = result["errors"] as JArray;
        var errorMsg = errors?.FirstOrDefault()?["message"]?.ToString() ?? "Unknown error";
        throw new Exception($"Failed to delete DNS record: {errorMsg}");
    }

    return $"✅ DNS record deleted: {recordId}";
});

server.RegisterTool("CloudflareGetLoadBalancers", async (args) =>
{
    var zoneId = args["zoneId"]?.Value<string>();
    var zoneName = args["zoneName"]?.Value<string>();

    if (string.IsNullOrEmpty(zoneId) && string.IsNullOrEmpty(zoneName))
        throw new Exception("Either zoneId or zoneName is required");

    using var cfClient = CreateCloudflareClient();

    // If zoneName provided, look up zone ID
    if (string.IsNullOrEmpty(zoneId))
    {
        var zonesResponse = await cfClient.GetAsync($"zones?name={zoneName}");
        zonesResponse.EnsureSuccessStatusCode();
        var zonesResult = JObject.Parse(await zonesResponse.Content.ReadAsStringAsync());
        var zones = zonesResult["result"] as JArray;
        if (zones == null || zones.Count == 0)
            throw new Exception($"Zone '{zoneName}' not found");
        zoneId = zones[0]["id"]?.ToString();
    }

    var response = await cfClient.GetAsync($"zones/{zoneId}/load_balancers");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var result = JObject.Parse(content);
    var lbs = result["result"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine("⚖️ Cloudflare Load Balancers");
    sb.AppendLine(new string('═', 50));

    if (lbs.Count == 0)
    {
        sb.AppendLine("No load balancers configured.");
        return sb.ToString();
    }

    foreach (var lb in lbs)
    {
        var enabled = lb["enabled"]?.Value<bool>() == true;
        var icon = enabled ? "✅" : "⏸️";

        sb.AppendLine($"{icon} {lb["name"]}");
        sb.AppendLine($"   ID: {lb["id"]}");
        sb.AppendLine($"   Steering: {lb["steering_policy"]}");
        sb.AppendLine($"   TTL: {lb["ttl"]}s");
        sb.AppendLine($"   Fallback Pool: {lb["fallback_pool"]}");

        var pools = lb["default_pools"] as JArray;
        if (pools != null && pools.Count > 0)
        {
            sb.AppendLine($"   Pools: {string.Join(", ", pools.Select(p => p.ToString()))}");
        }
        sb.AppendLine();
    }

    return sb.ToString();
});

server.RegisterTool("CloudflareGetTunnels", async (args) =>
{
    using var cfClient = CreateCloudflareClient();

    // Get account ID first
    var accountsResponse = await cfClient.GetAsync("accounts?per_page=1");
    accountsResponse.EnsureSuccessStatusCode();
    var accountsResult = JObject.Parse(await accountsResponse.Content.ReadAsStringAsync());
    var accountId = (accountsResult["result"] as JArray)?.FirstOrDefault()?["id"]?.ToString();

    if (string.IsNullOrEmpty(accountId))
        throw new Exception("No Cloudflare account found");

    var response = await cfClient.GetAsync($"accounts/{accountId}/cfd_tunnel");
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var result = JObject.Parse(content);
    var tunnels = result["result"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine("🚇 Cloudflare Tunnels");
    sb.AppendLine(new string('═', 50));

    if (tunnels.Count == 0)
    {
        sb.AppendLine("No tunnels configured.");
        return sb.ToString();
    }

    foreach (var tunnel in tunnels)
    {
        var status = tunnel["status"]?.ToString() ?? "unknown";
        var icon = status == "healthy" ? "✅" : status == "inactive" ? "⏸️" : "⚠️";

        sb.AppendLine($"{icon} {tunnel["name"]}");
        sb.AppendLine($"   ID: {tunnel["id"]}");
        sb.AppendLine($"   Status: {status}");
        sb.AppendLine($"   Created: {tunnel["created_at"]}");

        var connections = tunnel["connections"] as JArray;
        if (connections != null && connections.Count > 0)
        {
            sb.AppendLine($"   Active Connections: {connections.Count}");
            foreach (var conn in connections.Take(3))
            {
                sb.AppendLine($"      - {conn["colo_name"]} ({conn["client_id"]?.ToString()?[..8]}...)");
            }
        }
        sb.AppendLine();
    }

    return sb.ToString();
});

server.RegisterTool("CloudflareAnalyzeRouting", async (args) =>
{
    var zoneName = args["zoneName"]?.Value<string>();

    using var cfClient = CreateCloudflareClient();

    var sb = new StringBuilder();
    sb.AppendLine("🧠 AI Routing Analysis");
    sb.AppendLine(new string('═', 60));
    sb.AppendLine();

    // Get zones
    var zonesUrl = string.IsNullOrEmpty(zoneName) ? "zones?per_page=50" : $"zones?name={zoneName}";
    var zonesResponse = await cfClient.GetAsync(zonesUrl);
    zonesResponse.EnsureSuccessStatusCode();
    var zonesResult = JObject.Parse(await zonesResponse.Content.ReadAsStringAsync());
    var zones = zonesResult["result"] as JArray ?? new JArray();

    if (zones.Count == 0)
    {
        sb.AppendLine("No zones found to analyze.");
        return sb.ToString();
    }

    // Analyze each zone
    foreach (var zone in zones.Take(5))
    {
        var zoneId = zone["id"]?.ToString();
        var zName = zone["name"]?.ToString();

        sb.AppendLine($"## Zone: {zName}");
        sb.AppendLine();

        // Get DNS records
        var dnsResponse = await cfClient.GetAsync($"zones/{zoneId}/dns_records?per_page=100");
        var dnsResult = JObject.Parse(await dnsResponse.Content.ReadAsStringAsync());
        var records = dnsResult["result"] as JArray ?? new JArray();

        var aRecords = records.Where(r => r["type"]?.ToString() == "A").ToList();
        var cnameRecords = records.Where(r => r["type"]?.ToString() == "CNAME").ToList();
        var proxiedRecords = records.Where(r => r["proxied"]?.Value<bool>() == true).ToList();
        var unproxiedRecords = records.Where(r => r["proxied"]?.Value<bool>() == false).ToList();

        // Analyze and suggest
        var suggestions = new List<string>();

        // Check for unproxied A records (security concern)
        var unproxiedA = aRecords.Where(r => r["proxied"]?.Value<bool>() == false).ToList();
        if (unproxiedA.Count > 0)
        {
            suggestions.Add($"⚠️ Found {unproxiedA.Count} unproxied A records exposing origin IPs:");
            foreach (var record in unproxiedA.Take(5))
            {
                suggestions.Add($"   - {record["name"]} → {record["content"]}");
            }
            suggestions.Add($"   💡 Recommendation: Enable Cloudflare proxy to hide origin IPs");
        }

        // Check for missing www redirect
        var hasApex = aRecords.Any(r => r["name"]?.ToString() == zName);
        var hasWww = records.Any(r => r["name"]?.ToString() == $"www.{zName}");
        if (hasApex && !hasWww)
        {
            suggestions.Add($"⚠️ Missing www subdomain");
            suggestions.Add($"   💡 Recommendation: Add CNAME www → {zName} for better coverage");
        }

        // Check for load balancer opportunity
        var sameIpRecords = aRecords
            .GroupBy(r => r["content"]?.ToString())
            .Where(g => g.Count() >= 2)
            .ToList();
        if (sameIpRecords.Any())
        {
            suggestions.Add($"⚠️ Multiple A records pointing to same IP - no redundancy");
            suggestions.Add($"   💡 Recommendation: Consider Cloudflare Load Balancer for high availability");
        }

        // Check for missing security headers (via page rules)
        suggestions.Add($"ℹ️ Consider enabling:");
        suggestions.Add($"   - HSTS (Strict-Transport-Security)");
        suggestions.Add($"   - Always Use HTTPS");
        suggestions.Add($"   - Automatic HTTPS Rewrites");
        suggestions.Add($"   - Browser Integrity Check");

        // Get load balancers
        try
        {
            var lbResponse = await cfClient.GetAsync($"zones/{zoneId}/load_balancers");
            if (lbResponse.IsSuccessStatusCode)
            {
                var lbResult = JObject.Parse(await lbResponse.Content.ReadAsStringAsync());
                var lbs = lbResult["result"] as JArray ?? new JArray();

                if (lbs.Count > 0)
                {
                    sb.AppendLine($"**Load Balancers**: {lbs.Count} configured");
                    foreach (var lb in lbs)
                    {
                        var steering = lb["steering_policy"]?.ToString() ?? "off";
                        if (steering == "off" || steering == "")
                        {
                            suggestions.Add($"⚠️ Load Balancer '{lb["name"]}' has no steering policy");
                            suggestions.Add($"   💡 Recommendation: Enable geo/latency steering for better performance");
                        }
                    }
                }
                else
                {
                    if (aRecords.Count > 0)
                    {
                        suggestions.Add($"💡 No load balancers configured");
                        suggestions.Add($"   Consider adding load balancing for:");
                        suggestions.Add($"   - Automatic failover");
                        suggestions.Add($"   - Geographic routing (latency optimization)");
                        suggestions.Add($"   - Health checks for origins");
                    }
                }
            }
        }
        catch { /* Ignore LB fetch errors */ }

        // Output suggestions
        sb.AppendLine($"**DNS Records**: {records.Count} total ({proxiedRecords.Count} proxied, {unproxiedRecords.Count} direct)");
        sb.AppendLine();
        sb.AppendLine("### Recommendations:");
        foreach (var suggestion in suggestions)
        {
            sb.AppendLine(suggestion);
        }
        sb.AppendLine();
    }

    // Overall infrastructure suggestions
    sb.AppendLine("## Global Recommendations");
    sb.AppendLine();
    sb.AppendLine("🔐 **Security**");
    sb.AppendLine("   - Use Cloudflare Tunnels instead of exposing origin IPs");
    sb.AppendLine("   - Enable WAF rules for common attack patterns");
    sb.AppendLine("   - Set up Rate Limiting for API endpoints");
    sb.AppendLine();
    sb.AppendLine("⚡ **Performance**");
    sb.AppendLine("   - Enable Argo Smart Routing for 30%+ latency improvement");
    sb.AppendLine("   - Use Workers for edge computing / caching");
    sb.AppendLine("   - Configure Cache Rules for static assets");
    sb.AppendLine();
    sb.AppendLine("🔄 **Reliability**");
    sb.AppendLine("   - Set up Load Balancers with health checks");
    sb.AppendLine("   - Configure multiple origin pools in different regions");
    sb.AppendLine("   - Use steering policy: dynamic_latency or geo");

    return sb.ToString();
});

// ============================================================================
// DevOps Tools - Repository, Pull Request, and Pipeline Management
// ============================================================================

server.RegisterTool("GetDevOpsRepositories", async (args) =>
{
    var activeOnly = args["activeOnly"]?.Value<bool>() ?? true;
    var response = await server.Http.GetAsync($"/api/devops/repositories?activeOnly={activeOnly}");
    response.EnsureSuccessStatusCode();
    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var repos = result["data"] as JArray ?? new JArray();

    return new
    {
        success = true,
        count = repos.Count,
        repositories = repos.Select(r => new
        {
            id = r["id"],
            provider = r["provider"],
            repositoryName = r["repositoryName"],
            repositoryUrl = r["repositoryUrl"],
            defaultBranch = r["defaultBranch"],
            isActive = r["isActive"],
            webhookActive = r["webhookActive"],
            description = r["description"],
            isPrivate = r["isPrivate"],
            lastSyncedAt = r["lastSyncedAt"]
        }).ToList(),
        hint = repos.Count == 0
            ? "No repositories connected. Use ConnectRepository to add one."
            : $"Found {repos.Count} connected repositories."
    };
});

server.RegisterTool("GetDevOpsRepository", async (args) =>
{
    var repositoryId = args["repositoryId"]?.Value<int>() ?? throw new ArgumentException("repositoryId required");
    var response = await server.Http.GetAsync($"/api/devops/repositories/{repositoryId}");
    response.EnsureSuccessStatusCode();
    var repo = JObject.Parse(await response.Content.ReadAsStringAsync());

    return new
    {
        success = true,
        repository = new
        {
            id = repo["id"],
            provider = repo["provider"],
            repositoryName = repo["repositoryName"],
            repositoryUrl = repo["repositoryUrl"],
            defaultBranch = repo["defaultBranch"],
            isActive = repo["isActive"],
            webhookActive = repo["webhookActive"],
            description = repo["description"],
            isPrivate = repo["isPrivate"],
            lastSyncedAt = repo["lastSyncedAt"],
            accessTokenSet = !string.IsNullOrEmpty(repo["accessToken"]?.ToString())
        }
    };
});

server.RegisterTool("ConnectRepository", async (args) =>
{
    var request = new JObject
    {
        ["provider"] = args["provider"]?.ToString() ?? throw new ArgumentException("provider required (GitHub, GitLab, AzureDevOps)"),
        ["repositoryName"] = args["repositoryName"]?.ToString() ?? throw new ArgumentException("repositoryName required"),
        ["repositoryUrl"] = args["repositoryUrl"]?.ToString() ?? throw new ArgumentException("repositoryUrl required"),
        ["accessToken"] = args["accessToken"]?.ToString() ?? throw new ArgumentException("accessToken required"),
        ["defaultBranch"] = args["defaultBranch"]?.ToString() ?? "main",
        ["description"] = args["description"]?.ToString(),
        ["isPrivate"] = args["isPrivate"]?.Value<bool>() ?? false,
        ["setupWebhook"] = args["setupWebhook"]?.Value<bool>() ?? true
    };

    // Azure DevOps specific
    if (request["provider"]?.ToString() == "AzureDevOps")
    {
        request["azureOrganization"] = args["azureOrganization"]?.ToString() ?? throw new ArgumentException("azureOrganization required for AzureDevOps");
        request["azureProject"] = args["azureProject"]?.ToString() ?? throw new ArgumentException("azureProject required for AzureDevOps");
    }

    var content = new StringContent(request.ToString(), Encoding.UTF8, "application/json");
    var response = await server.Http.PostAsJsonAsync("/api/devops/repositories", content);
    response.EnsureSuccessStatusCode();
    var repo = JObject.Parse(await response.Content.ReadAsStringAsync());

    return new
    {
        success = true,
        message = "Repository connected successfully",
        repository = new
        {
            id = repo["id"],
            provider = repo["provider"],
            repositoryName = repo["repositoryName"],
            webhookActive = repo["webhookActive"]
        },
        hint = repo["webhookActive"]?.Value<bool>() == true
            ? "Webhook configured - PRs and pipeline events will sync automatically."
            : "Webhook not configured. Consider enabling for automatic sync."
    };
});

server.RegisterTool("UpdateRepository", async (args) =>
{
    var request = new JObject
    {
        ["id"] = args["repositoryId"]?.Value<int>() ?? throw new ArgumentException("repositoryId required")
    };
    if (args["isActive"] != null) request["isActive"] = args["isActive"];
    if (args["defaultBranch"] != null) request["defaultBranch"] = args["defaultBranch"];
    if (args["description"] != null) request["description"] = args["description"];
    if (args["accessToken"] != null) request["accessToken"] = args["accessToken"];

    var content = new StringContent(request.ToString(), Encoding.UTF8, "application/json");
    var response = await server.Http.PutAsync("/api/devops/repositories", content);
    response.EnsureSuccessStatusCode();

    return new
    {
        success = true,
        message = "Repository updated successfully"
    };
});

server.RegisterTool("DisconnectRepository", async (args) =>
{
    var repositoryId = args["repositoryId"]?.Value<int>() ?? throw new ArgumentException("repositoryId required");
    var response = await server.Http.DeleteAsync($"/api/devops/repositories/{repositoryId}");
    response.EnsureSuccessStatusCode();

    return new
    {
        success = true,
        message = "Repository disconnected successfully"
    };
});

server.RegisterTool("GetPullRequests", async (args) =>
{
    var repositoryId = args["repositoryId"]?.Value<int>();
    var state = args["state"]?.ToString();
    var limit = args["limit"]?.Value<int>() ?? 50;

    string url;
    if (repositoryId.HasValue)
    {
        url = $"/api/devops/repositories/{repositoryId}/pullrequests?limit={limit}";
    }
    else
    {
        url = $"/api/devops/pullrequests?limit={limit}";
    }
    if (!string.IsNullOrEmpty(state)) url += $"&state={state}";

    var response = await server.Http.GetAsync(url);
    response.EnsureSuccessStatusCode();
    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var prs = result["data"] as JArray ?? new JArray();

    return new
    {
        success = true,
        count = prs.Count,
        pullRequests = prs.Select(pr => new
        {
            id = pr["id"],
            externalPrNumber = pr["externalPrNumber"],
            title = pr["title"],
            state = pr["state"],
            url = pr["url"],
            sourceBranch = pr["sourceBranch"],
            targetBranch = pr["targetBranch"],
            authorUsername = pr["authorUsername"],
            ciStatus = pr["ciStatus"],
            taskId = pr["taskId"],
            createdAt = pr["createdAt"],
            updatedAt = pr["updatedAt"]
        }).ToList(),
        hint = prs.Count == 0 ? "No pull requests found." : null
    };
});

server.RegisterTool("GetTaskPullRequests", async (args) =>
{
    var taskId = args["taskId"]?.Value<int>() ?? throw new ArgumentException("taskId required");
    var response = await server.Http.GetAsync($"/api/devops/tasks/{taskId}/pullrequests");
    response.EnsureSuccessStatusCode();
    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var prs = result["data"] as JArray ?? new JArray();

    return new
    {
        success = true,
        taskId = taskId,
        count = prs.Count,
        pullRequests = prs.Select(pr => new
        {
            id = pr["id"],
            externalPrNumber = pr["externalPrNumber"],
            title = pr["title"],
            state = pr["state"],
            url = pr["url"],
            ciStatus = pr["ciStatus"]
        }).ToList(),
        hint = prs.Count == 0
            ? "No PRs linked to this task. PRs can be linked via 'Fixes SERVED-123' in PR description."
            : $"Found {prs.Count} PR(s) linked to task."
    };
});

server.RegisterTool("GetAgentSessionPullRequests", async (args) =>
{
    var sessionId = args["sessionId"]?.Value<int>() ?? throw new ArgumentException("sessionId required");
    var response = await server.Http.GetAsync($"/api/devops/sessions/{sessionId}/pullrequests");
    response.EnsureSuccessStatusCode();
    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var prs = result["data"] as JArray ?? new JArray();

    return new
    {
        success = true,
        sessionId = sessionId,
        count = prs.Count,
        pullRequests = prs.Select(pr => new
        {
            id = pr["id"],
            externalPrNumber = pr["externalPrNumber"],
            title = pr["title"],
            state = pr["state"],
            url = pr["url"],
            ciStatus = pr["ciStatus"]
        }).ToList()
    };
});

server.RegisterTool("LinkPullRequestToTask", async (args) =>
{
    var pullRequestId = args["pullRequestId"]?.Value<int>() ?? throw new ArgumentException("pullRequestId required");
    var taskId = args["taskId"]?.Value<int>() ?? throw new ArgumentException("taskId required");

    var request = new JObject { ["taskId"] = taskId };
    var content = new StringContent(request.ToString(), Encoding.UTF8, "application/json");
    var response = await server.Http.PostAsJsonAsync($"/api/devops/pullrequests/{pullRequestId}/link", content);
    response.EnsureSuccessStatusCode();

    return new
    {
        success = true,
        message = $"PR #{pullRequestId} linked to task #{taskId}",
        hint = "The task will now show this PR in its DevOps section."
    };
});

server.RegisterTool("GetPipelineRuns", async (args) =>
{
    var pullRequestId = args["pullRequestId"]?.Value<int>();
    var repositoryId = args["repositoryId"]?.Value<int>();
    var limit = args["limit"]?.Value<int>() ?? 50;

    if (!pullRequestId.HasValue && !repositoryId.HasValue)
        throw new ArgumentException("Either pullRequestId or repositoryId is required");

    string url;
    if (pullRequestId.HasValue)
    {
        url = $"/api/devops/pullrequests/{pullRequestId}/runs";
    }
    else
    {
        url = $"/api/devops/repositories/{repositoryId}/runs?limit={limit}";
    }

    var response = await server.Http.GetAsync(url);
    response.EnsureSuccessStatusCode();
    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var runs = result["data"] as JArray ?? new JArray();

    return new
    {
        success = true,
        count = runs.Count,
        pipelineRuns = runs.Select(run => new
        {
            id = run["id"],
            pipelineName = run["pipelineName"],
            status = run["status"],
            conclusion = run["conclusion"],
            url = run["url"],
            durationSeconds = run["durationSeconds"],
            startedAt = run["startedAt"],
            finishedAt = run["finishedAt"]
        }).ToList()
    };
});

server.RegisterTool("GetLatestPipelineRun", async (args) =>
{
    var pullRequestId = args["pullRequestId"]?.Value<int>() ?? throw new ArgumentException("pullRequestId required");
    var response = await server.Http.GetAsync($"/api/devops/pullrequests/{pullRequestId}/runs/latest");

    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        return new
        {
            success = true,
            pullRequestId = pullRequestId,
            latestRun = (object?)null,
            summary = "No pipeline runs found",
            hint = "This PR has no CI/CD runs yet."
        };
    }

    response.EnsureSuccessStatusCode();
    var run = JObject.Parse(await response.Content.ReadAsStringAsync());

    var conclusion = run["conclusion"]?.ToString() ?? "Unknown";
    var status = run["status"]?.ToString() ?? "Unknown";
    var duration = run["durationSeconds"]?.Value<int>() ?? 0;

    var summary = status == "Completed"
        ? $"CI Status: {(conclusion == "Success" ? "✅ OK" : "❌ Failed")} ({conclusion})"
        : $"CI Status: ⏳ {status}";

    var hint = conclusion switch
    {
        "Success" => "Pipeline completed successfully! Ready to merge.",
        "Failure" => "Pipeline failed. Check logs for details.",
        "Cancelled" => "Pipeline was cancelled.",
        _ => status == "InProgress" ? "Pipeline is still running..." : null
    };

    return new
    {
        success = true,
        pullRequestId = pullRequestId,
        latestRun = new
        {
            id = run["id"],
            pipelineName = run["pipelineName"],
            status = status,
            conclusion = conclusion,
            url = run["url"],
            durationSeconds = duration
        },
        summary = summary,
        hint = hint
    };
});

server.RegisterTool("GetPipelineJobs", async (args) =>
{
    var repositoryId = args["repositoryId"]?.Value<int>() ?? throw new ArgumentException("repositoryId required");
    var pipelineId = args["pipelineId"]?.Value<long>() ?? throw new ArgumentException("pipelineId required");

    var response = await server.Http.GetAsync($"/api/devops/repositories/{repositoryId}/pipelines/{pipelineId}/jobs");
    response.EnsureSuccessStatusCode();
    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var jobs = result["data"] as JArray ?? new JArray();

    var failedJobs = jobs.Where(j => j["status"]?.ToString() == "failed").ToList();

    return new
    {
        success = true,
        pipelineId = pipelineId,
        count = jobs.Count,
        jobs = jobs.Select(j => new
        {
            id = j["id"],
            name = j["name"],
            stage = j["stage"],
            status = j["status"],
            duration = j["duration"],
            webUrl = j["web_url"]
        }).ToList(),
        summary = failedJobs.Count > 0
            ? $"❌ {failedJobs.Count} job(s) failed: {string.Join(", ", failedJobs.Select(j => j["name"]))}"
            : "✅ All jobs passed"
    };
});

server.RegisterTool("GetJobLog", async (args) =>
{
    var repositoryId = args["repositoryId"]?.Value<int>() ?? throw new ArgumentException("repositoryId required");
    var jobId = args["jobId"]?.Value<long>() ?? throw new ArgumentException("jobId required");
    var tail = args["tail"]?.Value<int>() ?? 100;

    var response = await server.Http.GetAsync($"/api/devops/repositories/{repositoryId}/jobs/{jobId}/log");
    response.EnsureSuccessStatusCode();
    var log = await response.Content.ReadAsStringAsync();

    // Get last N lines
    var lines = log.Split('\n');
    var lastLines = lines.TakeLast(tail).ToArray();

    return new
    {
        success = true,
        jobId = jobId,
        totalLines = lines.Length,
        returnedLines = lastLines.Length,
        log = string.Join("\n", lastLines),
        hint = lines.Length > tail ? $"Showing last {tail} lines. Use 'tail' parameter to get more." : null
    };
});

server.RegisterTool("RetryJob", async (args) =>
{
    var repositoryId = args["repositoryId"]?.Value<int>() ?? throw new ArgumentException("repositoryId required");
    var jobId = args["jobId"]?.Value<long>() ?? throw new ArgumentException("jobId required");

    var response = await server.Http.PostAsync($"/api/devops/repositories/{repositoryId}/jobs/{jobId}/retry", null);
    response.EnsureSuccessStatusCode();

    return new
    {
        success = true,
        message = $"Job #{jobId} retried successfully",
        hint = "Check pipeline status for updated results."
    };
});

server.RegisterTool("CancelJob", async (args) =>
{
    var repositoryId = args["repositoryId"]?.Value<int>() ?? throw new ArgumentException("repositoryId required");
    var jobId = args["jobId"]?.Value<long>() ?? throw new ArgumentException("jobId required");

    var response = await server.Http.PostAsync($"/api/devops/repositories/{repositoryId}/jobs/{jobId}/cancel", null);
    response.EnsureSuccessStatusCode();

    return new
    {
        success = true,
        message = $"Job #{jobId} cancelled",
        hint = "The job has been stopped."
    };
});

// ----------------------------------------------------------------------
// Supervisor Pattern Tools
// Multi-agent orchestration for complex tasks
// ----------------------------------------------------------------------

server.RegisterTool("StartSupervisor", async (args) =>
{
    var agents = args["agents"]?.Value<string>() ?? "build,test,deploy,analysis";
    var maxParallel = args["maxParallel"]?.Value<int>() ?? 4;

    var response = await server.Http.PostAsJsonAsync("/api/agents/supervisor/start", new
    {
        agents = agents.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        maxParallelAgents = maxParallel,
        hostname = Environment.MachineName,
        workingDirectory = Environment.CurrentDirectory
    });

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return new { success = false, error };
    }

    var result = await response.Content.ReadFromJsonAsync<dynamic>();
    return new
    {
        success = true,
        sessionId = result?.sessionId,
        agents = result?.agents,
        message = "Supervisor session started. Ready to receive tasks."
    };
});

server.RegisterTool("StopSupervisor", async (args) =>
{
    var graceful = args["graceful"]?.Value<bool>() ?? true;

    var response = await server.Http.PostAsJsonAsync("/api/agents/supervisor/stop", new { graceful });

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return new { success = false, error };
    }

    return new
    {
        success = true,
        message = graceful ? "Supervisor stopped gracefully" : "Supervisor stopped immediately"
    };
});

server.RegisterTool("GetSupervisorStatus", async (args) =>
{
    var response = await server.Http.GetAsync("/api/agents/supervisor/status");

    if (!response.IsSuccessStatusCode)
    {
        return new { active = false, message = "No active supervisor session" };
    }

    var result = await response.Content.ReadFromJsonAsync<dynamic>();
    return result ?? new { active = false };
});

server.RegisterTool("AssignSupervisorTask", async (args) =>
{
    var taskDescription = args["taskDescription"]?.Value<string>()
        ?? throw new ArgumentException("taskDescription required");
    var dryRun = args["dryRun"]?.Value<bool>() ?? false;

    var response = await server.Http.PostAsJsonAsync("/api/agents/supervisor/assign", new
    {
        taskDescription,
        dryRun
    });

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return new { success = false, error };
    }

    var result = await response.Content.ReadFromJsonAsync<dynamic>();
    return new
    {
        success = true,
        planId = result?.planId,
        steps = result?.steps,
        summary = result?.summary,
        message = dryRun ? "Plan generated (dry run)" : "Task assigned and execution started"
    };
});

server.RegisterTool("GetExecutionPlan", async (args) =>
{
    var planId = args["planId"]?.Value<string>();

    var url = string.IsNullOrEmpty(planId)
        ? "/api/agents/supervisor/plan/current"
        : $"/api/agents/supervisor/plan/{planId}";

    var response = await server.Http.GetAsync(url);

    if (!response.IsSuccessStatusCode)
    {
        return new { success = false, message = "No active plan found" };
    }

    var result = await response.Content.ReadFromJsonAsync<dynamic>();
    return result ?? new { success = false };
});

server.RegisterTool("ControlPlan", async (args) =>
{
    var action = args["action"]?.Value<string>()
        ?? throw new ArgumentException("action required (pause/resume/cancel)");
    var planId = args["planId"]?.Value<string>();

    var response = await server.Http.PostAsJsonAsync($"/api/agents/supervisor/plan/{action}", new { planId });

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return new { success = false, error };
    }

    return new
    {
        success = true,
        action,
        message = $"Plan {action} executed"
    };
});

server.RegisterTool("SpawnSpecializedAgent", async (args) =>
{
    var role = args["role"]?.Value<string>()
        ?? throw new ArgumentException("role required (build/test/deploy/analysis)");
    var model = args["model"]?.Value<string>();
    var customPromptPath = args["customPromptPath"]?.Value<string>();

    var response = await server.Http.PostAsJsonAsync("/api/agents/spawn", new
    {
        role,
        model,
        customPromptPath,
        runInBackground = true
    });

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return new { success = false, error };
    }

    var result = await response.Content.ReadFromJsonAsync<dynamic>();
    return new
    {
        success = true,
        agentId = result?.agentId,
        role,
        status = result?.status,
        communicationChannel = result?.communicationChannel
    };
});

server.RegisterTool("GetAgentStatus", async (args) =>
{
    var agentId = args["agentId"]?.Value<string>();

    var url = string.IsNullOrEmpty(agentId)
        ? "/api/agents/coordination"
        : $"/api/agents/coordination/{agentId}";

    var response = await server.Http.GetAsync(url);

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return new { success = false, error };
    }

    var result = await response.Content.ReadFromJsonAsync<dynamic>();
    return result ?? new { };
});

server.RegisterTool("ControlAgent", async (args) =>
{
    var agentId = args["agentId"]?.Value<string>()
        ?? throw new ArgumentException("agentId required");
    var action = args["action"]?.Value<string>()
        ?? throw new ArgumentException("action required (pause/resume/terminate)");
    var force = args["force"]?.Value<bool>() ?? false;

    var response = await server.Http.PostAsJsonAsync($"/api/agents/{agentId}/{action}", new { force });

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return new { success = false, error };
    }

    return new
    {
        success = true,
        agentId,
        action,
        message = $"Agent {action} executed"
    };
});

server.RegisterTool("SendAgentMessage", async (args) =>
{
    var toAgentId = args["toAgentId"]?.Value<string>()
        ?? throw new ArgumentException("toAgentId required");
    var messageType = args["messageType"]?.Value<string>() ?? "Command";
    var payload = args["payload"]?.ToString() ?? "{}";

    var response = await server.Http.PostAsJsonAsync("/api/agents/message", new
    {
        toAgentId,
        messageType,
        payload
    });

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return new { success = false, error };
    }

    return new
    {
        success = true,
        message = $"Message sent to agent {toAgentId}"
    };
});

server.RegisterTool("ValidateAgentOutput", async (args) =>
{
    var agentId = args["agentId"]?.Value<string>()
        ?? throw new ArgumentException("agentId required");
    var stepId = args["stepId"]?.Value<string>()
        ?? throw new ArgumentException("stepId required");
    var output = args["output"]?.Value<string>() ?? "";

    var response = await server.Http.PostAsJsonAsync("/api/agents/validate", new
    {
        agentId,
        stepId,
        output
    });

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return new { success = false, error };
    }

    var result = await response.Content.ReadFromJsonAsync<dynamic>();
    return new
    {
        success = true,
        validationStatus = result?.status,
        checks = result?.checks,
        warnings = result?.warnings,
        errors = result?.errors
    };
});

// Start Server
await server.RunAsync();
