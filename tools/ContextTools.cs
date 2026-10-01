using System.Text;
using Newtonsoft.Json.Linq;
using Served.SDK.Client;

namespace Served.MCP.Tools;

/// <summary>
/// Context navigation tools - essential entry points for AI sessions.
/// These tools provide formatted context data that helps AI understand
/// available workspaces, tenants, and project structures.
/// Uses SDK client instead of raw HTTP calls.
/// </summary>
public static class ContextTools
{
    /// <summary>
    /// Register all context navigation tools.
    /// </summary>
    public static void Register(McpServer server, ServedClient client)
    {
        server.RegisterTool("GetUserContext",
            "FIRST TOOL TO CALL! Returns current user info, available tenants and workspaces. Essential for understanding what data you can access.",
            async (args) =>
            {
                var bootstrap = await client.Bootstrap.GetUserAsync();

                var sb = new StringBuilder();
                sb.AppendLine("@userContext {");
                sb.AppendLine($"  userId: {bootstrap.UserId}");
                sb.AppendLine($"  email: \"{bootstrap.Email}\"");
                sb.AppendLine($"  name: \"{bootstrap.FirstName} {bootstrap.LastName}\"");
                sb.AppendLine();

                // Workspaces nested under their tenant — slugs are only unique within a tenant,
                // and every workspace-scoped call needs both slugs
                var tenants = bootstrap.Tenants ?? [];
                var workspaces = bootstrap.Workspaces ?? [];
                sb.AppendLine($"  tenants: [{tenants.Count}] {{");
                foreach (var tenant in tenants)
                {
                    var tenantWorkspaces = workspaces.Where(w => w.TenantId == tenant.Id).ToList();
                    sb.AppendLine($"    @tenant[{tenant.Id}] {{ name: \"{tenant.Name}\", slug: \"{tenant.Slug}\", workspaces: [{tenantWorkspaces.Count}] }}");
                    foreach (var ws in tenantWorkspaces)
                        sb.AppendLine($"      @workspace[{ws.Id}] {{ name: \"{ws.Name}\", slug: \"{ws.Slug}\" }}");
                }
                var tenantIds = tenants.Select(t => t.Id).ToHashSet();
                foreach (var ws in workspaces.Where(w => !tenantIds.Contains(w.TenantId)))
                    sb.AppendLine($"    @workspace[{ws.Id}] {{ name: \"{ws.Name}\", slug: \"{ws.Slug}\", tenantId: {ws.TenantId} }}");
                sb.AppendLine("  }");
                sb.AppendLine("}");

                return sb.ToString();
            });

        server.RegisterTool("GetTenantContext",
            "Get detailed tenant context including workspaces, features, and team members.",
            async (args) =>
            {
                var tenantSlug = args["tenantSlug"]?.Value<string>() ?? throw new ArgumentException("tenantSlug required");

                var data = await client.Bootstrap.GetTenantAsync(tenantSlug);
                var tenant = data.Tenant;

                var sb = new StringBuilder();
                sb.AppendLine($"@tenantContext {{");
                sb.AppendLine($"  name: \"{tenant?.Name}\"");
                sb.AppendLine($"  slug: \"{tenant?.Slug}\"");
                sb.AppendLine($"  id: {tenant?.Id}");
                sb.AppendLine();

                var features = data.Features ?? [];
                var enabled = features.Where(f => f.IsEnabled).Select(f => f.Key).ToList();
                sb.AppendLine($"  features enabled ({enabled.Count} of {features.Count}): [{string.Join(", ", enabled)}]");
                sb.AppendLine();

                var user = await client.Bootstrap.GetUserAsync();
                var workspaces = (user.Workspaces ?? []).Where(w => w.TenantId == tenant?.Id).ToList();
                sb.AppendLine($"  workspaces: [{workspaces.Count}] {{");
                foreach (var ws in workspaces)
                    sb.AppendLine($"    @workspace[{ws.Id}] {{ name: \"{ws.Name}\", slug: \"{ws.Slug}\" }}");
                sb.AppendLine("  }");

                var employees = data.Employees ?? [];
                sb.AppendLine($"  team: [{employees.Count}] {{");
                foreach (var e in employees)
                    sb.AppendLine($"    @user[{e.UserId}] {{ name: \"{e.Name}\", initials: \"{e.Initials}\"{(string.IsNullOrEmpty(e.JobTitle) ? "" : $", title: \"{e.JobTitle}\"")} }}");
                sb.AppendLine("  }");
                sb.AppendLine();

                var settings = data.Settings ?? [];
                sb.AppendLine($"  settings: [{settings.Count}]");

                var boards = data.Boards ?? [];
                if (boards.Count > 0)
                {
                    sb.AppendLine($"  boards: [{boards.Count}] {{");
                    foreach (var b in boards)
                    {
                        sb.AppendLine($"    @board[{b.Id}] {{ name: \"{b.Name}\" }}");
                    }
                    sb.AppendLine("  }");
                }

                var categoryKeys = data.CategoryKeys;
                if (categoryKeys != null)
                {
                    sb.AppendLine($"  categoryKeys: {{");
                    if (categoryKeys.ProjectTypes?.Count > 0)
                        sb.AppendLine($"    ProjectTypes: [{string.Join(", ", categoryKeys.ProjectTypes)}]");
                    if (categoryKeys.ProjectStatuses?.Count > 0)
                        sb.AppendLine($"    ProjectStatuses: [{string.Join(", ", categoryKeys.ProjectStatuses)}]");
                    if (categoryKeys.ProjectStages?.Count > 0)
                        sb.AppendLine($"    ProjectStages: [{string.Join(", ", categoryKeys.ProjectStages)}]");
                    if (categoryKeys.TaskTypes?.Count > 0)
                        sb.AppendLine($"    TaskTypes: [{string.Join(", ", categoryKeys.TaskTypes)}]");
                    if (categoryKeys.TaskStates?.Count > 0)
                        sb.AppendLine($"    TaskStates: [{string.Join(", ", categoryKeys.TaskStates)}]");
                    if (categoryKeys.TaskPriorities?.Count > 0)
                        sb.AppendLine($"    TaskPriorities: [{string.Join(", ", categoryKeys.TaskPriorities)}]");
                    if (categoryKeys.ProjectTags?.Count > 0)
                        sb.AppendLine($"    ProjectTags: [{string.Join(", ", categoryKeys.ProjectTags)}]");
                    if (categoryKeys.TaskTags?.Count > 0)
                        sb.AppendLine($"    TaskTags: [{string.Join(", ", categoryKeys.TaskTags)}]");
                    sb.AppendLine("  }");
                }

                sb.AppendLine("}");
                return sb.ToString();
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["tenantSlug"] = new JObject { ["type"] = "string", ["description"] = "Tenant slug (e.g. 'served')" }
                },
                ["required"] = new JArray { "tenantSlug" }
            });

        server.RegisterTool("GetProjectContext",
            "Get project context: dates, progress, open tasks (due date, priority, assignee), task counts and the team working on it.",
            async (args) =>
            {
                var projectId = args["projectId"]?.Value<int>() ?? throw new ArgumentException("projectId required");

                var project = await client.Projects.GetAsync(projectId);
                var tasks = await client.Tasks.GetByProjectAsync(projectId, includeCompleted: true);
                var today = DateTime.UtcNow.Date;
                var open = tasks.Where(t => !t.IsCompleted).ToList();
                var overdue = open.Count(t => t.DueDate < today);

                var sb = new StringBuilder();
                sb.AppendLine($"@projectContext[{projectId}] {{");
                sb.AppendLine($"  name: \"{project.Name}\"");
                if (!string.IsNullOrWhiteSpace(project.Description))
                    sb.AppendLine($"  description: \"{project.Description}\"");
                sb.AppendLine($"  statusId: {project.ProjectStatusId}");
                if (project.CustomerId.HasValue)
                    sb.AppendLine($"  customerId: {project.CustomerId}");
                sb.AppendLine($"  progress: {project.Progress}%");
                sb.AppendLine($"  dates: {project.StartDate:yyyy-MM-dd} → {project.EndDate:yyyy-MM-dd}");
                sb.AppendLine($"  tasks: {{ total: {tasks.Count}, open: {open.Count}, done: {tasks.Count - open.Count}, overdue: {overdue} }}");

                // The task list carries assignee ids only — names come from the employee list
                var names = (await client.Employees.ListAsync(limit: 500))
                    .GroupBy(e => e.EmployeeId).ToDictionary(g => g.Key, g => g.First().FullName);
                string NameOf(int? userId) => userId is { } id && names.TryGetValue(id, out var n) ? n ?? $"user {id}" : $"user {userId}";

                var team = tasks.Where(t => t.AssignedTo.HasValue)
                    .GroupBy(t => t.AssignedTo!.Value)
                    .Select(g => (Id: g.Key, Name: NameOf(g.Key), Open: g.Count(t => !t.IsCompleted)))
                    .OrderByDescending(m => m.Open)
                    .ToList();
                sb.AppendLine($"  team: [{team.Count}] {{");
                foreach (var m in team)
                    sb.AppendLine($"    @user[{m.Id}] {{ name: \"{m.Name}\", openTasks: {m.Open} }}");
                sb.AppendLine("  }");

                sb.AppendLine($"  openTasks: [{open.Count}] {{");
                foreach (var t in open.OrderBy(t => t.DueDate ?? DateTime.MaxValue).Take(50))
                {
                    var due = t.DueDate.HasValue ? $", due: {t.DueDate:yyyy-MM-dd}{(t.DueDate < today ? " (overdue)" : "")}" : "";
                    var assignee = t.AssignedTo.HasValue ? $", assignee: \"{NameOf(t.AssignedTo)}\"" : "";
                    var priority = t.Priority.HasValue ? $", priority: {(int)t.Priority.Value}" : "";
                    sb.AppendLine($"    @task[{t.Id}] {{ name: \"{t.Name}\", status: {t.Status}{priority}{due}{assignee} }}");
                }
                if (open.Count > 50) sb.AppendLine($"    ...and {open.Count - 50} more");
                sb.AppendLine("  }");
                sb.AppendLine("}");

                return sb.ToString();
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["projectId"] = new JObject { ["type"] = "integer", ["description"] = "Project ID to get context for" }
                },
                ["required"] = new JArray { "projectId" }
            });
    }
}
