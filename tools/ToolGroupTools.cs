using Newtonsoft.Json.Linq;

namespace Served.MCP.Tools;

/// <summary>
/// Meta-tools that let the AI model manage its own tool groups.
/// These three tools are always visible (never filtered by the group system).
/// </summary>
public static class ToolGroupTools
{
    public static void Register(McpServer server, ToolGroupRegistry registry)
    {
        // ─── list_tool_groups ─────────────────────────────────────
        server.RegisterTool("list_tool_groups",
            "List all available tool groups with their activation status and tool counts. " +
            "Use this to see what tool domains are available before activating them.",
            async (args) =>
            {
                var groups = registry.ListGroups();
                var result = groups.Select(g => new
                {
                    name = g.Name,
                    description = g.Description,
                    active = g.Active,
                    always_active = g.AlwaysActive,
                    tool_count = g.Tools.Count,
                    notes = g.Notes
                });
                return new { groups = result, total = groups.Count, active = groups.Count(g => g.Active) };
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject(),
                ["required"] = new JArray()
            });

        // ─── activate_tool_group ──────────────────────────────────
        server.RegisterTool("activate_tool_group",
            "Activate a tool group to make its tools available for use. " +
            "Call list_tool_groups first to see available groups. " +
            "Available groups: core, project-management, task-management, customer-management, " +
            "time-tracking, agreements, devops, infrastructure, dashboards, datasource, boards, " +
            "finance, sales, atlas-control, supervisor, serva-marketing, media, vault, tenant-management",
            async (args) =>
            {
                var name = args["name"]?.Value<string>();
                if (string.IsNullOrEmpty(name))
                    return new { success = false, error = "Missing required parameter: name" };

                var (success, tools, error) = registry.ActivateGroup(name);
                if (!success)
                    return new { success = false, error };

                // Notify client to re-fetch tool list
                server.NotifyToolsChanged();

                return new { success = true, group = name, tools_added = tools.Count, tool_names = tools };
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["name"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Name of the tool group to activate"
                    }
                },
                ["required"] = new JArray { "name" }
            });

        // ─── deactivate_tool_group ────────────────────────────────
        server.RegisterTool("deactivate_tool_group",
            "Deactivate a tool group you no longer need. Reduces context window usage. " +
            "Cannot deactivate the 'core' group.",
            async (args) =>
            {
                var name = args["name"]?.Value<string>();
                if (string.IsNullOrEmpty(name))
                    return new { success = false, error = "Missing required parameter: name" };

                var (success, tools, error) = registry.DeactivateGroup(name);
                if (!success)
                    return new { success = false, error };

                // Notify client to re-fetch tool list
                server.NotifyToolsChanged();

                return new { success = true, group = name, tools_removed = tools.Count };
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["name"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Name of the tool group to deactivate"
                    }
                },
                ["required"] = new JArray { "name" }
            });

        Console.Error.WriteLine($"[MCP] Registered tool group meta-tools (list, activate, deactivate)");
    }
}
