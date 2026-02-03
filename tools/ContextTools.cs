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

        server.RegisterTool("GetTenantContext",
            "Get detailed tenant context including workspaces, features, and team members.",
            async (args) =>
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
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["tenantId"] = new JObject { ["type"] = "integer", ["description"] = "Tenant ID to get context for" }
                },
                ["required"] = new JArray { "tenantId" }
            });

        server.RegisterTool("GetProjectContext",
            "Get comprehensive project context including tasks, team, and recent activity.",
            async (args) =>
            {
                var projectId = args["projectId"]?.Value<int>() ?? throw new ArgumentException("projectId required");

                var response = await server.Http.GetAsync($"/api/projects/{projectId}");
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Failed to get project: {response.StatusCode}");

                var project = JObject.Parse(await response.Content.ReadAsStringAsync());

                var sb = new StringBuilder();
                sb.AppendLine($"@projectContext[{projectId}] {{");
                sb.AppendLine($"  name: \"{project["name"]}\"");
                sb.AppendLine($"  status: \"{project["status"]}\"");
                sb.AppendLine($"  customer: \"{project["customerName"]}\"");
                sb.AppendLine($"  progress: {project["percentComplete"]}%");
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
