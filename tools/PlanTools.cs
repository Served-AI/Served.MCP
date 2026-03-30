using Newtonsoft.Json.Linq;
using Served.MCP.Tools.State;

namespace Served.MCP.Tools;

/// <summary>
/// Plan Notebook MCP tools — structured plan management for agents.
/// Inspired by AgentScope's PlanNotebook pattern.
///
/// create_plan and get_current_plan are in the "core" group (always visible).
/// The remaining 4 mutation tools are in the "plan" group, auto-activated
/// when an agent creates a plan.
/// </summary>
public static class PlanTools
{
    public static void Register(McpServer server, ToolGroupRegistry registry, AgentStateManager stateManager)
    {
        // ─── create_plan (core group — always visible) ────────────
        server.RegisterTool("create_plan",
            "Create a structured execution plan with subtasks. Use this for complex tasks (3+ steps). " +
            "Creates the plan and auto-activates plan management tools (add_subtask, finish_subtask, etc.).",
            async (args) =>
            {
                var state = stateManager.GetOrCreate();

                var title = args["title"]?.Value<string>();
                if (string.IsNullOrEmpty(title))
                    return new { success = false, error = "Missing required parameter: title" };

                var subtaskNames = args["subtasks"]?.ToObject<List<string>>() ?? new();
                if (subtaskNames.Count == 0)
                    return new { success = false, error = "At least one subtask is required" };

                var plan = new AgentPlan
                {
                    Title = title,
                    Goal = args["goal"]?.Value<string>(),
                    SubTasks = subtaskNames.Select((name, i) => new SubTask
                    {
                        Title = name,
                        Order = i
                    }).ToList()
                };

                state.CurrentPlan = plan;
                stateManager.MarkDirty();
                stateManager.Save();

                // Auto-activate plan tools
                registry.ActivateGroup("plan");
                server.NotifyToolsChanged();

                return new { success = true, plan_id = plan.Id, plan = plan.ToMarkdown() };
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["title"] = new JObject { ["type"] = "string", ["description"] = "Plan title (concise, max 10 words)" },
                    ["goal"] = new JObject { ["type"] = "string", ["description"] = "What the plan should achieve (measurable outcome)" },
                    ["subtasks"] = new JObject
                    {
                        ["type"] = "array",
                        ["items"] = new JObject { ["type"] = "string" },
                        ["description"] = "List of subtask titles in execution order"
                    }
                },
                ["required"] = new JArray { "title", "subtasks" }
            });

        // ─── get_current_plan (core group — always visible) ───────
        server.RegisterTool("get_current_plan",
            "View the current plan with subtask progress. Returns markdown with checkboxes.",
            async (args) =>
            {
                var state = stateManager.GetOrCreate();
                if (state.CurrentPlan == null)
                    return new { success = false, message = "No active plan. Use create_plan to start one." };

                return new { success = true, plan = state.CurrentPlan.ToMarkdown() };
            });

        // ─── add_subtask (plan group) ─────────────────────────────
        server.RegisterTool("add_subtask",
            "Add a subtask to the current plan.",
            async (args) =>
            {
                var state = stateManager.GetOrCreate();
                if (state.CurrentPlan == null)
                    return new { success = false, error = "No active plan" };

                var title = args["title"]?.Value<string>();
                if (string.IsNullOrEmpty(title))
                    return new { success = false, error = "Missing required parameter: title" };

                var subtask = new SubTask
                {
                    Title = title,
                    Description = args["description"]?.Value<string>(),
                    Order = state.CurrentPlan.SubTasks.Count
                };

                var afterId = args["after"]?.Value<string>();
                if (!string.IsNullOrEmpty(afterId))
                {
                    var idx = state.CurrentPlan.SubTasks.FindIndex(s => s.Id == afterId);
                    if (idx >= 0)
                    {
                        subtask.Order = idx + 1;
                        state.CurrentPlan.SubTasks.Insert(idx + 1, subtask);
                        // Re-order
                        for (var i = 0; i < state.CurrentPlan.SubTasks.Count; i++)
                            state.CurrentPlan.SubTasks[i].Order = i;
                    }
                    else
                        state.CurrentPlan.SubTasks.Add(subtask);
                }
                else
                    state.CurrentPlan.SubTasks.Add(subtask);

                stateManager.MarkDirty();
                stateManager.Save();

                return new { success = true, subtask_id = subtask.Id, plan = state.CurrentPlan.ToMarkdown() };
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["title"] = new JObject { ["type"] = "string", ["description"] = "Subtask title" },
                    ["description"] = new JObject { ["type"] = "string", ["description"] = "Optional details" },
                    ["after"] = new JObject { ["type"] = "string", ["description"] = "Insert after this subtask ID" }
                },
                ["required"] = new JArray { "title" }
            });

        // ─── update_subtask_status (plan group) ───────────────────
        server.RegisterTool("update_subtask_status",
            "Change a subtask's status: todo, in_progress, or abandoned.",
            async (args) =>
            {
                var state = stateManager.GetOrCreate();
                if (state.CurrentPlan == null)
                    return new { success = false, error = "No active plan" };

                var id = args["subtask_id"]?.Value<string>();
                var statusStr = args["status"]?.Value<string>()?.ToLowerInvariant();

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(statusStr))
                    return new { success = false, error = "Missing subtask_id or status" };

                var subtask = state.CurrentPlan.SubTasks.FirstOrDefault(s => s.Id == id);
                if (subtask == null)
                {
                    // Try by index (1-based)
                    if (int.TryParse(id, out var idx) && idx >= 1 && idx <= state.CurrentPlan.SubTasks.Count)
                        subtask = state.CurrentPlan.SubTasks[idx - 1];
                }
                if (subtask == null)
                    return new { success = false, error = $"Subtask '{id}' not found" };

                subtask.State = statusStr switch
                {
                    "todo" => SubTaskState.Todo,
                    "in_progress" => SubTaskState.InProgress,
                    "abandoned" => SubTaskState.Abandoned,
                    "done" => SubTaskState.Done,
                    _ => subtask.State
                };

                if (subtask.State is SubTaskState.Done or SubTaskState.Abandoned)
                    subtask.CompletedAt = DateTime.UtcNow;

                state.CurrentPlan.RefreshState();
                stateManager.MarkDirty();
                stateManager.Save();

                return new { success = true, plan = state.CurrentPlan.ToMarkdown() };
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["subtask_id"] = new JObject { ["type"] = "string", ["description"] = "Subtask ID or 1-based index" },
                    ["status"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "New status",
                        ["enum"] = new JArray { "todo", "in_progress", "done", "abandoned" }
                    }
                },
                ["required"] = new JArray { "subtask_id", "status" }
            });

        // ─── finish_subtask (plan group) ──────────────────────────
        server.RegisterTool("finish_subtask",
            "Mark a subtask as done and record what was accomplished. The result is preserved across sessions.",
            async (args) =>
            {
                var state = stateManager.GetOrCreate();
                if (state.CurrentPlan == null)
                    return new { success = false, error = "No active plan" };

                var id = args["subtask_id"]?.Value<string>();
                var result = args["result"]?.Value<string>();

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(result))
                    return new { success = false, error = "Missing subtask_id or result" };

                var subtask = state.CurrentPlan.SubTasks.FirstOrDefault(s => s.Id == id);
                if (subtask == null)
                {
                    if (int.TryParse(id, out var idx) && idx >= 1 && idx <= state.CurrentPlan.SubTasks.Count)
                        subtask = state.CurrentPlan.SubTasks[idx - 1];
                }
                if (subtask == null)
                    return new { success = false, error = $"Subtask '{id}' not found" };

                subtask.State = SubTaskState.Done;
                subtask.Result = result;
                subtask.CompletedAt = DateTime.UtcNow;

                state.CurrentPlan.RefreshState();
                stateManager.MarkDirty();
                stateManager.Save();

                return new { success = true, plan = state.CurrentPlan.ToMarkdown() };
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["subtask_id"] = new JObject { ["type"] = "string", ["description"] = "Subtask ID or 1-based index" },
                    ["result"] = new JObject { ["type"] = "string", ["description"] = "What was accomplished (preserved across sessions)" }
                },
                ["required"] = new JArray { "subtask_id", "result" }
            });

        // ─── revise_plan (plan group) ─────────────────────────────
        server.RegisterTool("revise_plan",
            "Revise the current plan: abandon subtasks, add new ones, record why.",
            async (args) =>
            {
                var state = stateManager.GetOrCreate();
                if (state.CurrentPlan == null)
                    return new { success = false, error = "No active plan" };

                var note = args["note"]?.Value<string>();
                if (string.IsNullOrEmpty(note))
                    return new { success = false, error = "Missing revision note" };

                state.CurrentPlan.RevisionNotes.Add($"[{DateTime.UtcNow:HH:mm}] {note}");

                // Abandon specified subtasks
                var abandonIds = args["abandon"]?.ToObject<List<string>>() ?? new();
                foreach (var id in abandonIds)
                {
                    var st = state.CurrentPlan.SubTasks.FirstOrDefault(s => s.Id == id);
                    if (st == null && int.TryParse(id, out var idx) && idx >= 1 && idx <= state.CurrentPlan.SubTasks.Count)
                        st = state.CurrentPlan.SubTasks[idx - 1];
                    if (st != null)
                    {
                        st.State = SubTaskState.Abandoned;
                        st.CompletedAt = DateTime.UtcNow;
                    }
                }

                // Add new subtasks
                var newTasks = args["add"]?.ToObject<List<string>>() ?? new();
                foreach (var title in newTasks)
                {
                    state.CurrentPlan.SubTasks.Add(new SubTask
                    {
                        Title = title,
                        Order = state.CurrentPlan.SubTasks.Count
                    });
                }

                state.CurrentPlan.RefreshState();
                stateManager.MarkDirty();
                stateManager.Save();

                return new { success = true, plan = state.CurrentPlan.ToMarkdown() };
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["note"] = new JObject { ["type"] = "string", ["description"] = "Why the plan is being revised" },
                    ["abandon"] = new JObject
                    {
                        ["type"] = "array",
                        ["items"] = new JObject { ["type"] = "string" },
                        ["description"] = "Subtask IDs or indices to abandon"
                    },
                    ["add"] = new JObject
                    {
                        ["type"] = "array",
                        ["items"] = new JObject { ["type"] = "string" },
                        ["description"] = "New subtask titles to add"
                    }
                },
                ["required"] = new JArray { "note" }
            });

        Console.Error.WriteLine($"[MCP] Registered plan notebook tools (create, get, add, update, finish, revise)");
    }
}
