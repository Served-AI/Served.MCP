using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using Newtonsoft.Json.Linq;
using Served.MCP;
using Served.MCP.Tools;
using Served.SDK.Client;
using Served.SDK.Models.Projects;
using Served.SDK.Models.Dashboards;
using Served.SDK.Models.Datasource;
using Served.SDK.Models.Agents;
using Served.SDK.Tracing;
using Served.SDK.Utilities;

// Configuration - Load from env vars
var baseUrl = Environment.GetEnvironmentVariable("SERVED_API_URL") ?? "https://app.served.dk";
var token = Environment.GetEnvironmentVariable("SERVED_API_TOKEN") ?? "";
var tenant = Environment.GetEnvironmentVariable("SERVED_TENANT") ?? "";
var email = Environment.GetEnvironmentVariable("SERVED_EMAIL") ?? "";
var password = Environment.GetEnvironmentVariable("SERVED_PASSWORD") ?? "";
var enableTracing = Environment.GetEnvironmentVariable("SERVED_TRACING_ENABLED")?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true;

// Auto-login if no token but credentials provided
if (string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
{
    Console.Error.WriteLine($"[MCP] No token provided. Auto-login with {email}...");
    try
    {
        using var authHttp = new HttpClient { BaseAddress = new Uri(baseUrl) };
        authHttp.DefaultRequestHeaders.Add("User-Agent", "Served-MCP/2026.2 (Atlas)");
        var visitorId = Guid.NewGuid().ToString();
        var regResp = await authHttp.GetStringAsync($"/api/identity/account/Register?visitorId={visitorId}");
        var browserToken = JObject.Parse(regResp)["token"]?.ToString();

        if (!string.IsNullOrEmpty(browserToken))
        {
            authHttp.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", browserToken);
            var loginPayload = new StringContent(
                $"{{\"email\":\"{email}\",\"password\":\"{password}\"}}",
                Encoding.UTF8, "application/json");
            var loginResp = await authHttp.PostAsync("/api/identity/account/login", loginPayload);
            var loginJson = JObject.Parse(await loginResp.Content.ReadAsStringAsync());
            token = loginJson["token"]?.ToString() ?? "";

            if (!string.IsNullOrEmpty(token))
                Console.Error.WriteLine($"[MCP] Auto-login successful. Token expires in ~1h.");
            else
                Console.Error.WriteLine($"[MCP] Auto-login failed: no token in response.");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[MCP] Auto-login failed: {ex.Message}");
    }
}

// SDK Initialization with Tracing
var clientBuilder = new ServedClientBuilder()
    .WithBaseUrl(baseUrl)
    .WithToken(token)
    .WithTenant(tenant);

// Enable tracing if configured (default: on)
if (enableTracing)
{
    clientBuilder.WithTracing(options =>
    {
        options.ServiceName = "served-mcp-server";
        options.ServiceVersion = "2026.2.1";
        options.Environment = Environment.GetEnvironmentVariable("SERVED_ENVIRONMENT") ?? "development";
        options.EnableForge = true;
        options.ErrorDetection.CaptureSlowRequests = true;
        options.ErrorDetection.SlowRequestThresholdMs = 3000;
        options.SamplingRate = 1.0;
        options.AlwaysSampleErrors = true;
    });
}

using var client = clientBuilder.Build();
var server = new McpServer(client, baseUrl, token, tenant);

// Log startup info
Console.Error.WriteLine($"[MCP] Served MCP Server v2026.2.1");
Console.Error.WriteLine($"[MCP] Tracing enabled: {client.IsTracingEnabled}");
Console.Error.WriteLine($"[MCP] Registering tools...");

// ----------------------------------------------------------------------
// FOUNDATION: Auto-Generated SDK Tools (135 tools)
// ----------------------------------------------------------------------
GeneratedToolRegistrations.RegisterAllTools(server, client);
Console.Error.WriteLine($"[MCP] Registered 135 auto-generated SDK tools");

// ----------------------------------------------------------------------
// WALLS: Curated Manual Tools
// ----------------------------------------------------------------------

// Context Navigation - Essential entry points for AI sessions
ContextTools.Register(server, client);
Console.Error.WriteLine($"[MCP] Registered context navigation tools");

// Atlas Computer Control - Control Eden via IntelligenceManager
AtlasControlTools.Register(server, client);
Console.Error.WriteLine($"[MCP] Registered Atlas control tools");

// Supervisor Pattern - Multi-agent orchestration
SupervisorTools.Register(server, client);
Console.Error.WriteLine($"[MCP] Registered supervisor tools");

// Serva AI Marketing - Social monitoring and response management
ServaTools.Register(server, client);
Console.Error.WriteLine($"[MCP] Registered Serva marketing tools");

// Infrastructure - Cluster health and monitoring
InfrastructureTools.Register(server, client);
Console.Error.WriteLine($"[MCP] Registered infrastructure tools");

// Media Analytics - Download, analyze, lyrics (Ollama integration)
MediaTools.Register(server, client);
Console.Error.WriteLine($"[MCP] Registered media analytics tools");

// Grouping - Hierarchical project/task groupBy views
GroupingTools.Register(server, client);
Console.Error.WriteLine($"[MCP] Registered grouping tools");

// Notes - Obsidian vault access for Atlas
NotesTools.Register(server, client);
Console.Error.WriteLine($"[MCP] Registered notes/vault tools");

// Billing - SaaS plan catalog, subscriptions, funnel analytics
BillingTools.Register(server, client);
Console.Error.WriteLine($"[MCP] Registered billing tools");

// Suno - AI music generation (generate, lyrics, extend, credits)
SunoTools.Register(server, client);
Console.Error.WriteLine($"[MCP] Registered Suno music generation tools");

// Infra - UnifiedInfra IaC (plan, apply, recommend, status, cost)
InfraTools.Register(server, client);
Console.Error.WriteLine($"[MCP] Registered UnifiedInfra IaC tools");

// ----------------------------------------------------------------------
// Start the server
// ----------------------------------------------------------------------
Console.Error.WriteLine($"[MCP] All tools registered. Starting JSON-RPC server...");
await server.RunAsync();
