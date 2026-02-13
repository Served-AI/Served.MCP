using Newtonsoft.Json.Linq;
using Served.SDK.Client;
using Served.SDK.Models.Grouping;
using Served.SDK.Utilities;

namespace Served.MCP.Tools;

/// <summary>
/// Grouping tools for hierarchical project and task views.
/// Enables groupBy queries for Kanban boards, tree views, and dataset breakdowns.
/// </summary>
public static class GroupingTools
{
    /// <summary>
    /// Register all grouping tools.
    /// </summary>
    public static void Register(McpServer server, ServedClient client)
    {
        server.RegisterTool("GetTaskGrouping",
            "Get tasks grouped by a specified field (e.g., taskStateId, assignedTo, projectId). " +
            "Returns a hierarchical flat list with parent references for building tree views, Kanban boards, or grouped datasets. " +
            "Use groupBy to specify the field to group on. Provide projectIds to scope to specific projects.",
            async (args) =>
            {
                var request = new TaskGroupingRequest();

                var projectIds = args.GetIntList("projectIds");
                if (projectIds != null && projectIds.Count > 0)
                    request.ProjectIds = projectIds;

                var groupBy = args.GetOptionalString("groupBy");
                if (!string.IsNullOrEmpty(groupBy))
                    request.GroupBy = groupBy;

                var search = args.GetOptionalString("search");
                if (!string.IsNullOrEmpty(search))
                    request.Search = search;

                var filter = args.GetOptionalString("filter");
                if (!string.IsNullOrEmpty(filter))
                    request.Filter = filter;

                var take = args.GetOptionalIntOrNull("take");
                if (take.HasValue)
                    request.Take = take.Value;

                var result = await client.Tasks.GetGroupingAsync(request);
                return result;
            },
            JObject.Parse(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""projectIds"": {
                        ""type"": ""array"",
                        ""items"": { ""type"": ""integer"" },
                        ""description"": ""Filter by specific project IDs""
                    },
                    ""groupBy"": {
                        ""type"": ""string"",
                        ""description"": ""Field to group by (e.g., taskStateId, assignedTo, projectId, taskTypeId)""
                    },
                    ""search"": {
                        ""type"": ""string"",
                        ""description"": ""Search term to filter tasks""
                    },
                    ""filter"": {
                        ""type"": ""string"",
                        ""description"": ""Filter expression""
                    },
                    ""take"": {
                        ""type"": ""integer"",
                        ""description"": ""Maximum items to return""
                    }
                },
                ""required"": []
            }"));

        server.RegisterTool("GetProjectGrouping",
            "Get projects grouped by a specified field (e.g., projectStatusId, customerId, projectTypeId). " +
            "Returns a hierarchical flat list with parent references for building tree views or grouped datasets. " +
            "Use groupBy to specify the field to group on.",
            async (args) =>
            {
                var request = new ProjectGroupingRequest();

                var projectIds = args.GetIntList("projectIds");
                if (projectIds != null && projectIds.Count > 0)
                    request.ProjectIds = projectIds;

                var groupBy = args.GetOptionalString("groupBy");
                if (!string.IsNullOrEmpty(groupBy))
                    request.GroupBy = groupBy;

                var search = args.GetOptionalString("search");
                if (!string.IsNullOrEmpty(search))
                    request.Search = search;

                var filter = args.GetOptionalString("filter");
                if (!string.IsNullOrEmpty(filter))
                    request.Filter = filter;

                var take = args.GetOptionalIntOrNull("take");
                if (take.HasValue)
                    request.Take = take.Value;

                var result = await client.Projects.GetGroupingAsync(request);
                return result;
            },
            JObject.Parse(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""projectIds"": {
                        ""type"": ""array"",
                        ""items"": { ""type"": ""integer"" },
                        ""description"": ""Filter by specific project IDs""
                    },
                    ""groupBy"": {
                        ""type"": ""string"",
                        ""description"": ""Field to group by (e.g., projectStatusId, customerId, projectTypeId, projectStageId)""
                    },
                    ""search"": {
                        ""type"": ""string"",
                        ""description"": ""Search term to filter projects""
                    },
                    ""filter"": {
                        ""type"": ""string"",
                        ""description"": ""Filter expression""
                    },
                    ""take"": {
                        ""type"": ""integer"",
                        ""description"": ""Maximum items to return""
                    }
                },
                ""required"": []
            }"));
    }
}
