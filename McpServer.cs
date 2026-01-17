using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Served.SDK.Client;
using Served.SDK.Tracing;

namespace Served.MCP;

public class McpServer(ServedClient servedClient, string baseUrl, string token, string tenant)
{
    private readonly ServedClient _servedClient = servedClient;
    private readonly Dictionary<string, Func<JObject, Task<object>>> _tools = new();
    private readonly HttpClient _httpClient = CreateHttpClient(baseUrl, token, tenant);

    // Session tracking
    public string? SessionId { get; set; }
    public string? AgentId { get; set; }
    private int _conversationTurn = 0;

    // Analytics tracking (now uses SDK tracing)
    private bool _trackingEnabled = true;
    public bool TrackingEnabled
    {
        get => _trackingEnabled;
        set => _trackingEnabled = value;
    }

    /// <summary>
    /// Gets the SDK tracer for observability.
    /// </summary>
    public IServedTracer? Tracer => _servedClient.Tracer;

    private static HttpClient CreateHttpClient(string baseUrl, string token, string tenant)
    {
        var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        if (!string.IsNullOrEmpty(tenant))
        {
            client.DefaultRequestHeaders.Add("X-Tenant-Id", tenant);
        }
        return client;
    }

    public HttpClient Http => _httpClient;

    public void RegisterTool(string name, Func<JObject, Task<object>> handler)
    {
        _tools[name] = handler;
    }

    /// <summary>
    /// Get all registered tool names.
    /// </summary>
    public IReadOnlyCollection<string> GetRegisteredToolNames() => _tools.Keys.ToList().AsReadOnly();

    /// <summary>
    /// Check if a tool is registered.
    /// </summary>
    public bool HasTool(string name) => _tools.ContainsKey(name);

    /// <summary>
    /// Get count of registered tools.
    /// </summary>
    public int ToolCount => _tools.Count;

    /// <summary>
    /// Track a tool call for analytics. Uses SDK tracing when available, falls back to HTTP tracking.
    /// </summary>
    private async Task TrackToolCallAsync(string toolName, JObject? arguments, bool success, long durationMs, string? errorType = null, int? resultSize = null)
    {
        if (!_trackingEnabled) return;

        _conversationTurn++;

        // Use SDK tracing if available
        if (Tracer?.IsEnabled == true)
        {
            Tracer.RecordEvent(new TelemetryEvent
            {
                Type = success ? TelemetryEventType.Custom : TelemetryEventType.Exception,
                Name = $"mcp.tool.{toolName}",
                Severity = success ? TelemetrySeverity.Info : TelemetrySeverity.Error,
                Message = success ? $"Tool {toolName} completed" : $"Tool {toolName} failed: {errorType}",
                DurationMs = durationMs,
                Attributes = new Dictionary<string, object>
                {
                    ["mcp.tool.name"] = toolName,
                    ["mcp.tool.success"] = success,
                    ["mcp.session.id"] = SessionId ?? "",
                    ["mcp.agent.id"] = AgentId ?? "",
                    ["mcp.conversation.turn"] = _conversationTurn,
                    ["mcp.result.size"] = resultSize ?? 0
                }
            });

            // Record metric
            Tracer.RecordMetric("mcp.tool.duration", durationMs, new Dictionary<string, string>
            {
                ["tool_name"] = toolName,
                ["success"] = success.ToString().ToLowerInvariant()
            });

            return;
        }

        // Fallback to HTTP tracking
        try
        {
            var payload = new JObject
            {
                ["eventType"] = "mcp.tool_call",
                ["toolName"] = toolName,
                ["toolParameters"] = SanitizeParameters(arguments),
                ["toolSuccess"] = success,
                ["durationMs"] = durationMs,
                ["sessionId"] = SessionId,
                ["agentId"] = AgentId,
                ["conversationTurn"] = _conversationTurn,
                ["resultSize"] = resultSize,
                ["errorType"] = errorType,
                ["machineId"] = GetMachineIdHash(),
                ["cliVersion"] = "mcp-2026.1.2",
                ["workingDirectory"] = NormalizeWorkingDirectory(Directory.GetCurrentDirectory())
            };

            var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
            await _httpClient.PostAsync("/api/analytics/tools/mcp/event", content);
        }
        catch
        {
            // Silently ignore tracking failures
        }
    }

    /// <summary>
    /// Sanitize parameters to remove sensitive data before tracking.
    /// </summary>
    private static string? SanitizeParameters(JObject? args)
    {
        if (args == null) return null;

        var sanitized = new JObject(args);
        var sensitiveKeys = new[] { "password", "secret", "token", "key", "credential", "apiKey", "authorization" };

        foreach (var key in sanitized.Properties().Select(p => p.Name).ToList())
        {
            if (sensitiveKeys.Any(s => key.Contains(s, StringComparison.OrdinalIgnoreCase)))
            {
                sanitized[key] = "[REDACTED]";
            }
        }

        return sanitized.ToString(Formatting.None);
    }

    private static string GetMachineIdHash()
    {
        var machineId = Environment.MachineName;
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(machineId));
        return Convert.ToBase64String(hash)[..16];
    }

    private static string NormalizeWorkingDirectory(string path)
    {
        // Normalize to remove user-specific paths
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path.StartsWith(home))
        {
            return "~" + path[home.Length..];
        }
        return path;
    }

    public async Task RunAsync()
    {
        Console.Error.WriteLine("Served MCP Server Started. Waiting for input...");
        
        using var stdin = Console.OpenStandardInput();
        using var reader = new StreamReader(stdin);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var request = JObject.Parse(line);
                await HandleRequestAsync(request);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error processing request: {ex}");
            }
        }
    }

    private async Task HandleRequestAsync(JObject request)
    {
        var id = request["id"]?.ToString();
        var method = request["method"]?.ToString();

        if (method == "tools/list")
        {
            var response = new
            {
                jsonrpc = "2.0",
                id,
                result = new
                {
                    tools = _tools.Keys.Select(k => new { name = k }).ToList()
                }
            };
            SendResponse(response);
        }
        else if (method == "tools/call")
        {
            var paramsObj = request["params"] as JObject;
            var toolName = paramsObj?["name"]?.ToString();
            var arguments = paramsObj?["arguments"] as JObject ?? new JObject();

            if (toolName != null && _tools.TryGetValue(toolName, out var handler))
            {
                // Create tracing span for the tool call
                using var span = Tracer?.StartSpan($"mcp.tool.{toolName}", SpanKind.Server);
                span?.SetAttribute("mcp.tool.name", toolName);
                span?.SetAttribute("mcp.session.id", SessionId ?? "");
                span?.SetAttribute("mcp.conversation.turn", _conversationTurn + 1);

                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var result = await handler(arguments);
                    stopwatch.Stop();

                    var resultJson = JsonConvert.SerializeObject(result, Formatting.Indented);

                    span?.SetAttribute("mcp.result.size", resultJson.Length);
                    span?.SetAttribute("mcp.success", true);

                    // Track successful tool call
                    _ = TrackToolCallAsync(toolName, arguments, true, stopwatch.ElapsedMilliseconds, null, resultJson.Length);

                    var response = new
                    {
                        jsonrpc = "2.0",
                        id,
                        result = new
                        {
                            content = new[]
                            {
                                new { type = "text", text = resultJson }
                            }
                        }
                    };
                    SendResponse(response);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();

                    span?.SetError(true);
                    span?.SetAttribute("mcp.success", false);
                    span?.RecordException(ex);

                    // Track failed tool call
                    _ = TrackToolCallAsync(toolName, arguments, false, stopwatch.ElapsedMilliseconds, ex.GetType().Name);

                    SendError(id, -32603, ex.Message);
                }
            }
            else
            {
                SendError(id, -32601, $"Tool '{toolName}' not found.");
            }
        }
    }

    private void SendResponse(object response)
    {
        Console.WriteLine(JsonConvert.SerializeObject(response, Formatting.None));
    }

    private void SendError(string? id, int code, string message)
    {
        var response = new
        {
            jsonrpc = "2.0",
            id,
            error = new { code, message }
        };
        Console.WriteLine(JsonConvert.SerializeObject(response, Formatting.None));
    }
}
