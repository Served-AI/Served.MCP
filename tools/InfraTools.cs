using System.Diagnostics;
using System.Text;
using Newtonsoft.Json.Linq;
using Served.MCP.Tools.Services;
using Served.SDK.Client;

namespace Served.MCP.Tools;

/// <summary>
/// MCP tools for UnifiedInfra — AI-driven infrastructure management
/// 5 tools: plan, apply, recommend, status, cost
/// </summary>
public static class InfraTools
{
    public static void Register(McpServer server, ServedClient client)
    {
        var service = new InfraMcpService();

        // ─── infra_status ────────────────────────────────────────

        server.RegisterTool(
            "infra_status",
            "Get current infrastructure status — list stacks, resources, and state",
            async (args) =>
            {
                try
                {
                    var stack = args["stack"]?.Value<string>();

                    var sb = new StringBuilder();
                    sb.AppendLine("@infraStatus {");

                    if (string.IsNullOrEmpty(stack))
                    {
                        // List all stacks
                        var stacks = service.ListStacks();
                        sb.AppendLine($"  stacks: {stacks.Count}");

                        if (stacks.Count == 0)
                        {
                            sb.AppendLine("  message: \"No infrastructure state found. Create with: served iac apply <file.infra.yaml>\"");
                        }
                        else
                        {
                            foreach (var s in stacks)
                            {
                                sb.AppendLine($"  - {s}");
                            }
                        }
                    }
                    else
                    {
                        // Show specific stack
                        var state = service.GetStackState(stack);
                        if (state == null)
                        {
                            sb.AppendLine($"  stack: \"{stack}\"");
                            sb.AppendLine("  status: \"not found\"");
                        }
                        else
                        {
                            sb.AppendLine($"  stack: \"{stack}\"");
                            sb.AppendLine($"  output: {{");
                            foreach (var line in state.Output.Split('\n'))
                                sb.AppendLine($"    {line}");
                            sb.AppendLine("  }");
                        }
                    }

                    // Also show recent history
                    var history = service.GetHistory(stack, 5);
                    if (history.Count > 0)
                    {
                        sb.AppendLine("  recentHistory: [");
                        foreach (var h in history)
                        {
                            sb.AppendLine($"    {{ action: \"{h.Action}\", stack: \"{h.StackName}\", time: \"{h.Timestamp:yyyy-MM-dd HH:mm}\", created: {h.Created}, destroyed: {h.Destroyed} }}");
                        }
                        sb.AppendLine("  ]");
                    }

                    sb.AppendLine("}");
                    return sb.ToString();
                }
                catch (Exception ex)
                {
                    return $"@infraStatus {{ success: false, error: \"{ex.Message.Replace("\"", "'")}\" }}";
                }
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["stack"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Stack name to show details for (omit to list all stacks)"
                    }
                }
            });

        // ─── infra_plan ──────────────────────────────────────────

        server.RegisterTool(
            "infra_plan",
            "Preview infrastructure changes — shows what would be created, updated, or destroyed",
            async (args) =>
            {
                try
                {
                    var configPath = args["config_path"]?.Value<string>()
                        ?? throw new ArgumentException("config_path required");

                    var result = await service.PlanAsync(configPath);

                    var sb = new StringBuilder();
                    sb.AppendLine("@infraPlan {");
                    sb.AppendLine($"  success: {result.Success.ToString().ToLower()}");
                    sb.AppendLine($"  configPath: \"{configPath}\"");
                    sb.AppendLine("  output: {");
                    foreach (var line in result.StdOut.Split('\n'))
                        sb.AppendLine($"    {line}");
                    sb.AppendLine("  }");

                    if (!result.Success && !string.IsNullOrEmpty(result.StdErr))
                    {
                        sb.AppendLine($"  error: \"{result.StdErr.Trim().Replace("\"", "'")}\"");
                    }

                    sb.AppendLine("}");
                    return sb.ToString();
                }
                catch (Exception ex)
                {
                    return $"@infraPlan {{ success: false, error: \"{ex.Message.Replace("\"", "'")}\" }}";
                }
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["config_path"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Path to .infra.yaml config file or directory containing configs"
                    }
                },
                ["required"] = new JArray { "config_path" }
            });

        // ─── infra_apply ─────────────────────────────────────────

        server.RegisterTool(
            "infra_apply",
            "Apply infrastructure changes — creates, updates, or destroys resources. CAUTION: This modifies real infrastructure. Always run infra_plan first.",
            async (args) =>
            {
                try
                {
                    var configPath = args["config_path"]?.Value<string>()
                        ?? throw new ArgumentException("config_path required");
                    var autoApprove = args["auto_approve"]?.Value<bool>() ?? false;

                    if (!autoApprove)
                    {
                        return "@infraApply { success: false, error: \"auto_approve must be true. Set auto_approve=true to confirm you want to apply changes. Run infra_plan first to preview.\" }";
                    }

                    var result = await service.ApplyAsync(configPath, true);

                    var sb = new StringBuilder();
                    sb.AppendLine("@infraApply {");
                    sb.AppendLine($"  success: {result.Success.ToString().ToLower()}");
                    sb.AppendLine("  output: {");
                    foreach (var line in result.StdOut.Split('\n'))
                        sb.AppendLine($"    {line}");
                    sb.AppendLine("  }");

                    if (!result.Success && !string.IsNullOrEmpty(result.StdErr))
                    {
                        sb.AppendLine($"  error: \"{result.StdErr.Trim().Replace("\"", "'")}\"");
                    }

                    sb.AppendLine("}");
                    return sb.ToString();
                }
                catch (Exception ex)
                {
                    return $"@infraApply {{ success: false, error: \"{ex.Message.Replace("\"", "'")}\" }}";
                }
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["config_path"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Path to .infra.yaml config file or directory"
                    },
                    ["auto_approve"] = new JObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Must be true to confirm apply. Safety flag to prevent accidental changes.",
                        ["default"] = false
                    }
                },
                ["required"] = new JArray { "config_path", "auto_approve" }
            });

        // ─── infra_recommend ─────────────────────────────────────

        server.RegisterTool(
            "infra_recommend",
            "Get smart server recommendations based on workload type. Returns ranked provider/spec/price suggestions.",
            async (args) =>
            {
                try
                {
                    var workload = args["workload"]?.Value<string>();

                    var sb = new StringBuilder();
                    sb.AppendLine("@infraRecommend {");

                    if (string.IsNullOrEmpty(workload))
                    {
                        // List available workloads
                        sb.AppendLine("  availableWorkloads: [");
                        foreach (var w in service.ListWorkloads())
                            sb.AppendLine($"    \"{w}\"");
                        sb.AppendLine("  ]");
                        sb.AppendLine("  hint: \"Pass a workload name to get recommendations\"");
                    }
                    else
                    {
                        var rec = service.Recommend(workload);
                        if (rec == null)
                        {
                            sb.AppendLine($"  error: \"Unknown workload: {workload}\"");
                            sb.AppendLine("  availableWorkloads: [");
                            foreach (var w in service.ListWorkloads())
                                sb.AppendLine($"    \"{w}\"");
                            sb.AppendLine("  ]");
                        }
                        else
                        {
                            sb.AppendLine($"  workload: \"{rec.Workload}\"");
                            sb.AppendLine($"  description: \"{rec.Description}\"");
                            sb.AppendLine("  recommendations: [");
                            foreach (var entry in rec.Entries)
                            {
                                var price = entry.PriceMonthly > 0 ? $"~{entry.PriceMonthly:F2} EUR/mo" : "FREE";
                                sb.AppendLine("    {");
                                sb.AppendLine($"      rank: {entry.Rank}");
                                sb.AppendLine($"      label: \"{entry.Label}\"");
                                sb.AppendLine($"      provider: \"{entry.Provider}\"");
                                sb.AppendLine($"      plan: \"{entry.Plan}\"");
                                sb.AppendLine($"      spec: \"{entry.Spec}\"");
                                sb.AppendLine($"      price: \"{price}\"");
                                sb.AppendLine($"      location: \"{entry.Location}\"");
                                sb.AppendLine($"      reason: \"{entry.Reason}\"");
                                sb.AppendLine("    }");
                            }
                            sb.AppendLine("  ]");
                            sb.AppendLine($"  quickStart: \"served iac init {workload} --name my-{workload} && served iac plan && served iac apply\"");
                        }
                    }

                    sb.AppendLine("}");
                    return sb.ToString();
                }
                catch (Exception ex)
                {
                    return $"@infraRecommend {{ success: false, error: \"{ex.Message.Replace("\"", "'")}\" }}";
                }
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["workload"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Workload type: ai-inference, web-api, database, build-server, k8s-worker, game-server, monitoring. Omit to list all."
                    }
                }
            });

        // ─── infra_cost ──────────────────────────────────────────

        server.RegisterTool(
            "infra_cost",
            "Estimate monthly infrastructure cost for a provider and server type, or for an entire config file",
            async (args) =>
            {
                try
                {
                    var provider = args["provider"]?.Value<string>() ?? "";
                    var serverType = args["server_type"]?.Value<string>() ?? "";
                    var cpu = args["cpu"]?.Value<int>() ?? 0;
                    var configPath = args["config_path"]?.Value<string>();

                    var sb = new StringBuilder();
                    sb.AppendLine("@infraCost {");

                    if (!string.IsNullOrEmpty(configPath))
                    {
                        // Run cost via CLI for full config
                        var result = await Task.Run(() =>
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = "served",
                                Arguments = $"iac cost \"{configPath}\"",
                                RedirectStandardOutput = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            using var p = Process.Start(psi);
                            var output = p?.StandardOutput.ReadToEnd() ?? "";
                            p?.WaitForExit();
                            return output;
                        });

                        sb.AppendLine($"  configPath: \"{configPath}\"");
                        sb.AppendLine("  output: {");
                        foreach (var line in result.Split('\n'))
                            sb.AppendLine($"    {line}");
                        sb.AppendLine("  }");
                    }
                    else
                    {
                        // Single resource estimate
                        var cost = service.EstimateCost(provider, serverType, cpu);
                        var priceStr = cost > 0 ? $"~{cost:F2} EUR/mo" : "FREE (local)";

                        sb.AppendLine($"  provider: \"{provider}\"");
                        if (!string.IsNullOrEmpty(serverType))
                            sb.AppendLine($"  serverType: \"{serverType}\"");
                        if (cpu > 0)
                            sb.AppendLine($"  cpu: {cpu}");
                        sb.AppendLine($"  estimatedCost: \"{priceStr}\"");
                        sb.AppendLine($"  priceMonthly: {cost:F2}");
                        sb.AppendLine($"  currency: \"EUR\"");

                        // Add context
                        sb.AppendLine("  pricingNote: \"Prices are approximate EU estimates. Use served iac catalog for live pricing.\"");
                    }

                    sb.AppendLine("}");
                    return sb.ToString();
                }
                catch (Exception ex)
                {
                    return $"@infraCost {{ success: false, error: \"{ex.Message.Replace("\"", "'")}\" }}";
                }
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["provider"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Provider: hetzner, ovh, proxmox"
                    },
                    ["server_type"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Server type (e.g., cx32, cax41, ccx33, b3-8)"
                    },
                    ["cpu"] = new JObject
                    {
                        ["type"] = "integer",
                        ["description"] = "CPU count (for estimation when server_type is unknown)"
                    },
                    ["config_path"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Path to .infra.yaml file to estimate total cost for all resources"
                    }
                }
            });
    }
}
