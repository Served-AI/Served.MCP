using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Served.MCP.Tools;

/// <summary>
/// served_mcp_info — the server describes itself: what it is built from and which tools it really serves.
/// `served mcp check` and `served mcp status` read this over stdio.
/// </summary>
public static class McpInfoTools
{
    public static void Register(McpServer server, ToolGroupRegistry registry)
    {
        server.RegisterTool("served_mcp_info",
            "Describe this Served MCP server: version, the Served.MCP and Served.SDK commits it was built from, " +
            "build time, tool counts, tool groups, and tools that belong to no group. " +
            "Use it to check whether the server is current.",
            async (args) =>
            {
                await Task.CompletedTask;
                return JsonConvert.SerializeObject(Describe(server, registry, args["includeTools"]?.Value<bool>() == true), Formatting.Indented);
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["includeTools"] = new JObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Also list every registered tool with its groups"
                    }
                },
                ["required"] = new JArray()
            });
    }

    public static JObject Describe(McpServer server, ToolGroupRegistry registry, bool includeTools)
    {
        var registered = server.GetRegisteredToolNames().OrderBy(n => n, StringComparer.Ordinal).ToList();
        var registeredSet = new HashSet<string>(registered, StringComparer.OrdinalIgnoreCase);

        var info = new JObject
        {
            ["server"] = "served-mcp",
            ["version"] = BuildStamp.ServerVersion,
            ["mcpCommit"] = BuildStamp.McpCommit,
            ["mcpDirty"] = BuildStamp.McpDirty,
            ["sdkCommit"] = BuildStamp.SdkCommit,
            ["sdkDirty"] = BuildStamp.SdkDirty,
            ["builtAtUtc"] = BuildStamp.BuiltAtUtc,
            ["generatedTools"] = GeneratedToolRegistrations.ToolCount,
            ["registeredTools"] = registered.Count,
            ["groups"] = new JArray(registry.ListGroups().Select(g => new JObject
            {
                ["name"] = g.Name,
                ["active"] = g.Active,
                ["tools"] = g.Tools.Count(registeredSet.Contains)
            })),
            ["ungroupedTools"] = new JArray(registry.UngroupedTools(registered)),
            ["unknownGroupTools"] = new JArray(registry.UnknownTools(registered))
        };

        if (includeTools)
        {
            info["tools"] = new JArray(registered.Select(t => new JObject
            {
                ["name"] = t,
                ["groups"] = new JArray(registry.GroupsOf(t))
            }));
        }

        return info;
    }
}
