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

/// <summary>
/// Tool definition with metadata for MCP protocol compliance.
/// </summary>
public class ToolDefinition
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public JObject InputSchema { get; set; } = new JObject
    {
        ["type"] = "object",
        ["properties"] = new JObject(),
        ["required"] = new JArray()
    };
    public required Func<JObject, Task<object>> Handler { get; set; }
}

/// <summary>
/// MCP Server implementation following the Model Context Protocol specification.
/// Supports stdio transport with JSON-RPC 2.0 messaging.
///
/// BRICK Philosophy:
/// - Foundation: JSON-RPC protocol handling
/// - Walls: Tool registration and execution
/// - Roof: Tracing and analytics
/// </summary>
public class McpServer(ServedClient servedClient, string baseUrl, string token, string tenant)
{
    private readonly ServedClient _servedClient = servedClient;
    private readonly Dictionary<string, ToolDefinition> _tools = new();
    private readonly HttpClient _httpClient = CreateHttpClient(baseUrl, token, tenant);
    private readonly string _baseUrl = baseUrl;
    private readonly string _tenant = tenant;

    // Server metadata
    private const string ServerName = "served-mcp";
    private const string ServerVersion = "2026.2.0";
    private const string ProtocolVersion = "2024-11-05";

    // Auth refresh
    private string? _email = Environment.GetEnvironmentVariable("SERVED_EMAIL");
    private string? _password = Environment.GetEnvironmentVariable("SERVED_PASSWORD");
    private string? _apiKey = Environment.GetEnvironmentVariable("SERVED_API_KEY");
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private DateTime _tokenExpiry = DateTime.UtcNow.AddHours(1);

    // Session tracking
    public string? SessionId { get; set; }
    public string? AgentId { get; set; }
    private int _conversationTurn = 0;
    private bool _initialized = false;

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

    /// <summary>
    /// Refresh the JWT token by re-authenticating. Called automatically on 401/503.
    /// Supports: SERVED_API_KEY (long-lived), or SERVED_EMAIL + SERVED_PASSWORD (auto-login).
    /// </summary>
    public async Task<bool> RefreshTokenAsync()
    {
        if (!await _refreshLock.WaitAsync(5000)) return false;
        try
        {
            // Skip if token was recently refreshed by another call
            if (DateTime.UtcNow < _tokenExpiry.AddMinutes(-5)) return true;

            // Option 1: API key (never expires)
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                _servedClient.SetToken(_apiKey);
                _tokenExpiry = DateTime.UtcNow.AddYears(1);
                Console.Error.WriteLine("[MCP] Using API key (long-lived)");
                return true;
            }

            // Option 2: Auto-login with email/password
            if (string.IsNullOrEmpty(_email) || string.IsNullOrEmpty(_password))
            {
                Console.Error.WriteLine("[MCP] Token expired. Set SERVED_EMAIL + SERVED_PASSWORD or SERVED_API_KEY for auto-refresh.");
                return false;
            }

            Console.Error.WriteLine("[MCP] Token expired, auto-refreshing...");

            using var authClient = new HttpClient { BaseAddress = new Uri(_baseUrl) };
            authClient.DefaultRequestHeaders.Add("User-Agent", "Served-MCP/2026.2 (Atlas)");

            // Step 1: Register browser session
            var visitorId = Guid.NewGuid().ToString();
            var regResponse = await authClient.GetAsync($"/api/identity/account/Register?visitorId={visitorId}");
            if (!regResponse.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"[MCP] Browser registration failed: {regResponse.StatusCode}");
                return false;
            }

            var regJson = JObject.Parse(await regResponse.Content.ReadAsStringAsync());
            var browserToken = regJson["token"]?.ToString();
            if (string.IsNullOrEmpty(browserToken))
            {
                Console.Error.WriteLine("[MCP] Browser registration returned no token");
                return false;
            }

            // Step 2: Login
            authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", browserToken);
            var loginPayload = new StringContent(
                JsonConvert.SerializeObject(new { email = _email, password = _password }),
                Encoding.UTF8, "application/json");
            var loginResponse = await authClient.PostAsync("/api/identity/account/login", loginPayload);
            if (!loginResponse.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"[MCP] Login failed: {loginResponse.StatusCode}");
                return false;
            }

            var loginJson = JObject.Parse(await loginResponse.Content.ReadAsStringAsync());
            var newToken = loginJson["token"]?.ToString();
            if (string.IsNullOrEmpty(newToken))
            {
                Console.Error.WriteLine("[MCP] Login returned no token");
                return false;
            }

            // Step 3: Update HttpClient and SDK client
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
            _servedClient.SetToken(newToken);
            _tokenExpiry = DateTime.UtcNow.AddMinutes(55); // JWT is typically 1h

            Console.Error.WriteLine("[MCP] Token refreshed successfully");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[MCP] Token refresh failed: {ex.Message}");
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Register a tool with full metadata (description and schema).
    /// </summary>
    public void RegisterTool(string name, string description, Func<JObject, Task<object>> handler, JObject? inputSchema = null)
    {
        _tools[name] = new ToolDefinition
        {
            Name = name,
            Description = description,
            Handler = handler,
            InputSchema = inputSchema ?? new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject(),
                ["required"] = new JArray()
            }
        };
    }

    /// <summary>
    /// Register a tool (backwards compatible - auto-generates description).
    /// </summary>
    public void RegisterTool(string name, Func<JObject, Task<object>> handler)
    {
        RegisterTool(name, $"Execute the {name} operation", handler);
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
                ["cliVersion"] = $"mcp-{ServerVersion}",
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
        Console.Error.WriteLine($"Served MCP Server v{ServerVersion} Started. Waiting for input...");
        Console.Error.WriteLine($"Protocol Version: {ProtocolVersion}");
        Console.Error.WriteLine($"Registered Tools: {_tools.Count}");

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

        switch (method)
        {
            case "initialize":
                HandleInitialize(id, request);
                break;

            case "initialized":
                // Client acknowledgment - no response needed
                _initialized = true;
                Console.Error.WriteLine("MCP session initialized.");
                break;

            case "tools/list":
                HandleToolsList(id);
                break;

            case "tools/call":
                await HandleToolCall(id, request);
                break;

            case "resources/list":
                HandleResourcesList(id);
                break;

            case "prompts/list":
                HandlePromptsList(id);
                break;

            case "ping":
                HandlePing(id);
                break;

            default:
                SendError(id, -32601, $"Method '{method}' not found.");
                break;
        }
    }

    /// <summary>
    /// Handle MCP initialize request.
    /// </summary>
    private void HandleInitialize(string? id, JObject request)
    {
        var paramsObj = request["params"] as JObject;
        var clientInfo = paramsObj?["clientInfo"];

        Console.Error.WriteLine($"Client connected: {clientInfo?["name"]} v{clientInfo?["version"]}");

        var response = new
        {
            jsonrpc = "2.0",
            id,
            result = new
            {
                protocolVersion = ProtocolVersion,
                capabilities = new
                {
                    tools = new { listChanged = false },
                    resources = new { subscribe = false, listChanged = false },
                    prompts = new { listChanged = false },
                    logging = new { }
                },
                serverInfo = new
                {
                    name = ServerName,
                    version = ServerVersion
                },
                instructions = "Served MCP Server provides access to the Served platform - projects, tasks, time tracking, analytics, and more. Use GetUserContext first to understand which workspaces you have access to."
            }
        };
        SendResponse(response);
    }

    /// <summary>
    /// Handle tools/list request with full metadata.
    /// </summary>
    private void HandleToolsList(string? id)
    {
        var tools = _tools.Values.Select(t => new
        {
            name = t.Name,
            description = t.Description,
            inputSchema = t.InputSchema
        }).ToList();

        var response = new
        {
            jsonrpc = "2.0",
            id,
            result = new { tools }
        };
        SendResponse(response);
    }

    /// <summary>
    /// Handle tools/call request.
    /// </summary>
    private async Task HandleToolCall(string? id, JObject request)
    {
        var paramsObj = request["params"] as JObject;
        var toolName = paramsObj?["name"]?.ToString();
        var arguments = paramsObj?["arguments"] as JObject ?? new JObject();

        if (toolName != null && _tools.TryGetValue(toolName, out var toolDef))
        {
            // Create tracing span for the tool call
            using var span = Tracer?.StartSpan($"mcp.tool.{toolName}", SpanKind.Server);
            span?.SetAttribute("mcp.tool.name", toolName);
            span?.SetAttribute("mcp.session.id", SessionId ?? "");
            span?.SetAttribute("mcp.conversation.turn", _conversationTurn + 1);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                object result;
                try
                {
                    result = await toolDef.Handler(arguments);
                }
                catch (Exception firstEx) when (IsAuthError(firstEx))
                {
                    // Token expired — try auto-refresh and retry once
                    if (await RefreshTokenAsync())
                    {
                        result = await toolDef.Handler(arguments);
                    }
                    else
                    {
                        throw; // Re-throw original if refresh failed
                    }
                }

                stopwatch.Stop();

                var resultText = FormatResult(result);

                span?.SetAttribute("mcp.result.size", resultText.Length);
                span?.SetAttribute("mcp.success", true);

                // Track successful tool call
                _ = TrackToolCallAsync(toolName, arguments, true, stopwatch.ElapsedMilliseconds, null, resultText.Length);

                var response = new
                {
                    jsonrpc = "2.0",
                    id,
                    result = new
                    {
                        content = new[]
                        {
                            new { type = "text", text = resultText }
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
            SendError(id, -32601, $"Tool '{toolName}' not found. Use tools/list to see available tools.");
        }
    }

    /// <summary>
    /// Format tool result as compact markdown instead of verbose JSON.
    /// Strings pass through as-is. Objects/arrays get markdown table/list format.
    /// </summary>
    private static string FormatResult(object result)
    {
        if (result is string s) return s;

        var json = JsonConvert.SerializeObject(result);
        var token = JToken.Parse(json);

        return token.Type switch
        {
            JTokenType.Array => FormatArray((JArray)token),
            JTokenType.Object => FormatObject((JObject)token),
            _ => token.ToString()
        };
    }

    private static string FormatArray(JArray arr)
    {
        if (arr.Count == 0) return "*(empty)*";

        // If array of objects, use markdown table
        if (arr[0] is JObject firstObj)
        {
            var keys = firstObj.Properties().Select(p => p.Name).Take(8).ToList();
            var sb = new StringBuilder();
            sb.AppendLine($"**{arr.Count} items**\n");
            sb.AppendLine("| " + string.Join(" | ", keys) + " |");
            sb.AppendLine("| " + string.Join(" | ", keys.Select(_ => "---")) + " |");
            foreach (var item in arr.Take(50))
            {
                if (item is JObject obj)
                {
                    var vals = keys.Select(k =>
                    {
                        var v = obj[k];
                        if (v == null || v.Type == JTokenType.Null) return "-";
                        var str = v.ToString().Replace("|", "\\|");
                        return str.Length > 60 ? str[..57] + "..." : str;
                    });
                    sb.AppendLine("| " + string.Join(" | ", vals) + " |");
                }
            }
            if (arr.Count > 50) sb.AppendLine($"\n*...and {arr.Count - 50} more*");
            return sb.ToString();
        }

        // Simple array
        return string.Join(", ", arr.Select(x => x.ToString()));
    }

    private static string FormatObject(JObject obj)
    {
        var sb = new StringBuilder();
        foreach (var prop in obj.Properties())
        {
            var val = prop.Value;
            if (val == null || val.Type == JTokenType.Null) continue;

            if (val is JArray childArr && childArr.Count > 0 && childArr[0] is JObject)
            {
                sb.AppendLine($"\n### {prop.Name}");
                sb.AppendLine(FormatArray(childArr));
            }
            else if (val is JArray simpleArr)
            {
                sb.AppendLine($"- **{prop.Name}**: {string.Join(", ", simpleArr.Select(x => x.ToString()))}");
            }
            else if (val is JObject childObj)
            {
                sb.AppendLine($"\n**{prop.Name}**:");
                foreach (var cp in childObj.Properties())
                {
                    if (cp.Value.Type != JTokenType.Null)
                        sb.AppendLine($"  - {cp.Name}: {cp.Value}");
                }
            }
            else
            {
                sb.AppendLine($"- **{prop.Name}**: {val}");
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Check if an exception indicates an auth/token error (401, 403, ServiceUnavailable).
    /// </summary>
    private static bool IsAuthError(Exception ex)
    {
        var msg = ex.Message + (ex.InnerException?.Message ?? "");
        return msg.Contains("Unauthorized") || msg.Contains("401")
            || msg.Contains("ServiceUnavailable") || msg.Contains("503")
            || msg.Contains("Forbidden") || msg.Contains("403");
    }

    /// <summary>
    /// Handle resources/list request (empty for now).
    /// </summary>
    private void HandleResourcesList(string? id)
    {
        var response = new
        {
            jsonrpc = "2.0",
            id,
            result = new
            {
                resources = Array.Empty<object>()
            }
        };
        SendResponse(response);
    }

    /// <summary>
    /// Handle prompts/list request (empty for now).
    /// </summary>
    private void HandlePromptsList(string? id)
    {
        var response = new
        {
            jsonrpc = "2.0",
            id,
            result = new
            {
                prompts = Array.Empty<object>()
            }
        };
        SendResponse(response);
    }

    /// <summary>
    /// Handle ping request.
    /// </summary>
    private void HandlePing(string? id)
    {
        var response = new
        {
            jsonrpc = "2.0",
            id,
            result = new { }
        };
        SendResponse(response);
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
