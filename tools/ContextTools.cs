using System.Text;
using Newtonsoft.Json.Linq;
using Served.SDK.Client;

namespace Served.MCP.Tools;

/// <summary>
/// Context navigation tools - essential entry points for AI sessions.
/// These tools provide formatted context data that helps AI understand
/// available workspaces, tenants, and project structures.
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
                var response = await server.Http.GetAsync("/api/core/bootstrap/user");
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Failed to get user context: {response.StatusCode}");

                var bootstrap = JObject.Parse(await response.Content.ReadAsStringAsync());

                var sb = new StringBuilder();
                sb.AppendLine("@userContext {");
                sb.AppendLine($"  userId: {bootstrap["userId"]}");
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

        server.RegisterTool("GetTenantContext",
            "Get detailed tenant context including workspaces, features, and team members.",
            async (args) =>
            {
                var tenantSlug = args["tenantSlug"]?.Value<string>() ?? throw new ArgumentException("tenantSlug required");

                var response = await server.Http.GetAsync($"/api/core/bootstrap/tenant/{tenantSlug}");
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Failed to get tenant context: {response.StatusCode}");

                var data = JObject.Parse(await response.Content.ReadAsStringAsync());
                var tenant = data["tenant"] ?? data;

                var sb = new StringBuilder();
                sb.AppendLine($"@tenantContext {{");
                sb.AppendLine($"  name: \"{tenant["name"]}\"");
                sb.AppendLine($"  slug: \"{tenant["slug"]}\"");
                sb.AppendLine($"  id: {tenant["id"]}");
                sb.AppendLine();

                var features = data["features"] as JArray ?? new JArray();
                sb.AppendLine($"  features: [{features.Count}] {{");
                foreach (var f in features)
                {
                    sb.AppendLine($"    {f["key"]}: {f["isEnabled"]}");
                }
                sb.AppendLine("  }");
                sb.AppendLine();

                var settings = data["settings"] as JArray ?? new JArray();
                sb.AppendLine($"  settings: [{settings.Count}]");

                var boards = data["boards"] as JArray ?? new JArray();
                if (boards.Count > 0)
                {
                    sb.AppendLine($"  boards: [{boards.Count}] {{");
                    foreach (var b in boards)
                    {
                        sb.AppendLine($"    @board[{b["id"]}] {{ name: \"{b["name"]}\" }}");
                    }
                    sb.AppendLine("  }");
                }

                var categoryKeys = data["categoryKeys"];
                if (categoryKeys != null)
                {
                    sb.AppendLine($"  categoryKeys: {{");
                    foreach (var prop in (categoryKeys as JObject)?.Properties() ?? [])
                    {
                        sb.AppendLine($"    {prop.Name}: [{string.Join(", ", prop.Value)}]");
                    }
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

                var response = await server.Http.GetAsync($"/api/project-management/projects/{projectId}");
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Failed to get project: {response.StatusCode}");

                var project = JObject.Parse(await response.Content.ReadAsStringAsync());

                var sb = new StringBuilder();
                sb.AppendLine($"@projectContext[{projectId}] {{");
                sb.AppendLine($"  name: \"{project["name"]}\"");
                sb.AppendLine($"  description: \"{project["description"]}\"");
                sb.AppendLine($"  status: \"{project["projectStatusId"]}\"");
                sb.AppendLine($"  customerId: {project["customerId"]}");
                sb.AppendLine($"  progress: {project["progress"]}%");
                sb.AppendLine($"  startDate: \"{project["startDate"]}\"");
                sb.AppendLine($"  endDate: \"{project["endDate"]}\"");
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
