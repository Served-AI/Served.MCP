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

                var tenants = bootstrap.Tenants ?? [];
                sb.AppendLine($"  tenants: [{tenants.Count}] {{");
                foreach (var tenant in tenants)
                {
                    sb.AppendLine($"    @tenant[{tenant.Id}] {{ name: \"{tenant.Name}\", slug: \"{tenant.Slug}\" }}");
                }
                sb.AppendLine("  }");
                sb.AppendLine();

                var workspaces = bootstrap.Workspaces ?? [];
                sb.AppendLine($"  workspaces: [{workspaces.Count}] {{");
                foreach (var ws in workspaces)
                {
                    sb.AppendLine($"    @workspace[{ws.Id}] {{ name: \"{ws.Name}\", slug: \"{ws.Slug}\" }}");
                }
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
                sb.AppendLine($"  features: [{features.Count}] {{");
                foreach (var f in features)
                {
                    sb.AppendLine($"    {f.Key}: {f.IsEnabled}");
                }
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
            "Get comprehensive project context including tasks, team, and recent activity.",
            async (args) =>
            {
                var projectId = args["projectId"]?.Value<int>() ?? throw new ArgumentException("projectId required");

                var project = await client.Projects.GetAsync(projectId);

                var sb = new StringBuilder();
                sb.AppendLine($"@projectContext[{projectId}] {{");
                sb.AppendLine($"  name: \"{project.Name}\"");
                sb.AppendLine($"  description: \"{project.Description}\"");
                sb.AppendLine($"  status: \"{project.ProjectStatusId}\"");
                sb.AppendLine($"  customerId: {project.CustomerId}");
                sb.AppendLine($"  progress: {project.Progress}%");
                sb.AppendLine($"  startDate: \"{project.StartDate}\"");
                sb.AppendLine($"  endDate: \"{project.EndDate}\"");
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
