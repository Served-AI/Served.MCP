using System.Net.Http.Json;
using Newtonsoft.Json.Linq;
using Served.SDK.Client;

namespace Served.MCP.Tools;

/// <summary>
/// Supervisor Pattern Tools - Multi-agent orchestration for complex tasks.
/// Enables Atlas to spawn and coordinate specialized agents.
/// </summary>
public static class SupervisorTools
{
    /// <summary>
    /// Register all Supervisor orchestration tools.
    /// </summary>
    public static void Register(McpServer server, ServedClient client)
    {
        server.RegisterTool("StartSupervisor",
            "Start a supervisor session with specified agent types (build, test, deploy, analysis).",
            async (args) =>
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
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["agents"] = new JObject { ["type"] = "string", ["description"] = "Comma-separated agent types: build,test,deploy,analysis" },
                    ["maxParallel"] = new JObject { ["type"] = "integer", ["description"] = "Maximum parallel agents (default: 4)" }
                }
            });

        server.RegisterTool("StopSupervisor",
            "Stop the active supervisor session.",
            async (args) =>
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
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["graceful"] = new JObject { ["type"] = "boolean", ["description"] = "Wait for running tasks to complete (default: true)" }
                }
            });

        server.RegisterTool("GetSupervisorStatus",
            "Get the status of the active supervisor session.",
            async (args) =>
            {
                var response = await server.Http.GetAsync("/api/agents/supervisor/status");

                if (!response.IsSuccessStatusCode)
                {
                    return new { active = false, message = "No active supervisor session" };
                }

                var result = await response.Content.ReadFromJsonAsync<dynamic>();
                return result ?? new { active = false };
            });

        server.RegisterTool("AssignSupervisorTask",
            "Assign a task to the supervisor for automatic decomposition and execution.",
            async (args) =>
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
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["taskDescription"] = new JObject { ["type"] = "string", ["description"] = "Description of the task to execute" },
                    ["dryRun"] = new JObject { ["type"] = "boolean", ["description"] = "Generate plan without executing (default: false)" }
                },
                ["required"] = new JArray { "taskDescription" }
            });

        server.RegisterTool("GetExecutionPlan",
            "Get the current or specified execution plan.",
            async (args) =>
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
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["planId"] = new JObject { ["type"] = "string", ["description"] = "Plan ID (optional, defaults to current)" }
                }
            });

        server.RegisterTool("ControlPlan",
            "Control an execution plan: pause, resume, or cancel.",
            async (args) =>
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
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["action"] = new JObject { ["type"] = "string", ["description"] = "Action: pause, resume, cancel" },
                    ["planId"] = new JObject { ["type"] = "string", ["description"] = "Plan ID (optional)" }
                },
                ["required"] = new JArray { "action" }
            });

        server.RegisterTool("SpawnSpecializedAgent",
            "Spawn a specialized agent with a specific role.",
            async (args) =>
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
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["role"] = new JObject { ["type"] = "string", ["description"] = "Agent role: build, test, deploy, analysis" },
                    ["model"] = new JObject { ["type"] = "string", ["description"] = "Model to use (optional)" },
                    ["customPromptPath"] = new JObject { ["type"] = "string", ["description"] = "Path to custom prompt file (optional)" }
                },
                ["required"] = new JArray { "role" }
            });

        server.RegisterTool("GetAgentCoordinationStatus",
            "Get coordination status for all agents or a specific agent.",
            async (args) =>
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
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["agentId"] = new JObject { ["type"] = "string", ["description"] = "Agent ID (optional, returns all if not specified)" }
                }
            });

        server.RegisterTool("ControlAgent",
            "Control a specific agent: pause, resume, or terminate.",
            async (args) =>
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
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["agentId"] = new JObject { ["type"] = "string", ["description"] = "Agent ID to control" },
                    ["action"] = new JObject { ["type"] = "string", ["description"] = "Action: pause, resume, terminate" },
                    ["force"] = new JObject { ["type"] = "boolean", ["description"] = "Force the action (default: false)" }
                },
                ["required"] = new JArray { "agentId", "action" }
            });
    }
}
