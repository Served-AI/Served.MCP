using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Served.SDK.Client;

namespace Served.MCP;

public class McpServer(IServedClient servedClient, string baseUrl, string token, string tenant)
{
    private readonly IServedClient _servedClient = servedClient;
    private readonly Dictionary<string, Func<JObject, Task<object>>> _tools = new();
    private readonly HttpClient _httpClient = CreateHttpClient(baseUrl, token, tenant);

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
                try
                {
                    var result = await handler(arguments);
                    var response = new
                    {
                        jsonrpc = "2.0",
                        id,
                        result = new
                        {
                            content = new[] 
                            { 
                                new { type = "text", text = JsonConvert.SerializeObject(result, Formatting.Indented) } 
                            }
                        }
                    };
                    SendResponse(response);
                }
                catch (Exception ex)
                {
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
