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
var apiKey = Environment.GetEnvironmentVariable("SERVED_API_KEY") ?? "";
var tenant = Environment.GetEnvironmentVariable("SERVED_TENANT") ?? "";
var email = Environment.GetEnvironmentVariable("SERVED_EMAIL") ?? "";
var password = Environment.GetEnvironmentVariable("SERVED_PASSWORD") ?? "";
var enableTracing = Environment.GetEnvironmentVariable("SERVED_TRACING_ENABLED")?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true;

// Auth priority: API key > valid JWT token > auto-login with credentials
// API keys never expire and don't need refresh — most stable for development
if (!string.IsNullOrEmpty(apiKey))
{
    token = apiKey;
    Console.Error.WriteLine($"[MCP] Using API key (long-lived, no refresh needed)");
}
else if (!string.IsNullOrEmpty(token))
{
    // Check if JWT token is expired or about to expire (within 5 min)
    var expiry = ParseJwtExpiry(token);
    if (expiry < DateTime.UtcNow.AddMinutes(5))
    {
        Console.Error.WriteLine($"[MCP] Token expired ({expiry:HH:mm:ss} UTC). Auto-refreshing...");
        token = await AutoLogin(baseUrl, email, password) ?? token;
    }
    else
    {
        Console.Error.WriteLine($"[MCP] Token valid until {expiry:HH:mm:ss} UTC");
    }
}
else if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
{
    Console.Error.WriteLine($"[MCP] No token. Auto-login with {email}...");
    token = await AutoLogin(baseUrl, email, password) ?? "";
}

// Auto-login helper
static async Task<string?> AutoLogin(string baseUrl, string email, string password)
{
    if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)) return null;
    try
    {
        using var authHttp = new HttpClient { BaseAddress = new Uri(baseUrl) };
        authHttp.DefaultRequestHeaders.Add("User-Agent", "Served-MCP/2026.2 (Atlas)");
        var visitorId = Guid.NewGuid().ToString();
        var regResp = await authHttp.GetStringAsync($"/api/identity/account/Register?visitorId={visitorId}");
        var browserToken = JObject.Parse(regResp)["token"]?.ToString();

        if (string.IsNullOrEmpty(browserToken)) { Console.Error.WriteLine("[MCP] Browser registration failed"); return null; }

        authHttp.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", browserToken);
        var loginPayload = new StringContent(
            Newtonsoft.Json.JsonConvert.SerializeObject(new { email, password }),
            Encoding.UTF8, "application/json");
        var loginResp = await authHttp.PostAsync("/api/identity/account/login", loginPayload);
        var loginJson = JObject.Parse(await loginResp.Content.ReadAsStringAsync());
        var newToken = loginJson["token"]?.ToString();

        if (!string.IsNullOrEmpty(newToken))
        {
            Console.Error.WriteLine($"[MCP] Auto-login successful.");
            return newToken;
        }
        Console.Error.WriteLine($"[MCP] Auto-login failed: no token in response.");
        return null;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[MCP] Auto-login failed: {ex.Message}");
        return null;
    }
}

// JWT expiry parser (shared with McpServer)
static DateTime ParseJwtExpiry(string? jwt)
{
    if (string.IsNullOrEmpty(jwt)) return DateTime.MinValue;
    try
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2) return DateTime.MinValue;
        var payload = parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=');
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        var exp = JObject.Parse(json)["exp"]?.Value<long>();
        return exp == null ? DateTime.MaxValue : DateTimeOffset.FromUnixTimeSeconds(exp.Value).UtcDateTime;
    }
    catch { return DateTime.MinValue; }
}

// Resolve tenant slug to numeric ID for X-Tenant-Id header
// The API's GetManager.AssignMetaData needs tenant context for data queries.
// Served-Tenant (slug) requires LoginService.UserId to resolve, but X-Tenant-Id (numeric) works directly.
var tenantId = "";
if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(tenant))
{
    try
    {
        using var bootstrapHttp = new HttpClient { BaseAddress = new Uri(baseUrl) };
        bootstrapHttp.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        bootstrapHttp.DefaultRequestHeaders.Add("Served-Tenant", tenant);
        var userBootstrap = await bootstrapHttp.GetStringAsync("/api/core/bootstrap/user");
        var bootstrapJson = JObject.Parse(userBootstrap);
        var tenants = bootstrapJson["tenants"] as JArray;
        var matchedTenant = tenants?.FirstOrDefault(t =>
            string.Equals(t["slug"]?.ToString(), tenant, StringComparison.OrdinalIgnoreCase));
        if (matchedTenant != null)
        {
            tenantId = matchedTenant["id"]?.ToString() ?? "";
            Console.Error.WriteLine($"[MCP] Resolved tenant '{tenant}' to ID {tenantId}");
        }
        else
        {
            Console.Error.WriteLine($"[MCP] Warning: tenant '{tenant}' not found in user's tenants");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[MCP] Warning: could not resolve tenant ID: {ex.Message}");
    }
}

// SDK Initialization with Tracing
var clientBuilder = new ServedClientBuilder()
    .WithBaseUrl(baseUrl)
    .WithToken(token)
    .WithTenant(tenant);

// Add numeric tenant ID header for reliable tenant resolution
// GetManager.AssignMetaData falls back to X-Tenant-Id when Served-Tenant slug resolution fails
if (!string.IsNullOrEmpty(tenantId))
{
    clientBuilder.WithDefaultHeader("X-Tenant-Id", tenantId);
}

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
var server = new McpServer(client, baseUrl, token, tenant, tenantId);

// Tool Group Registry — reduces context window waste by filtering inactive tools
var toolGroupRegistry = new ToolGroupRegistry();
server.SetToolGroupRegistry(toolGroupRegistry);
var activeCount = toolGroupRegistry.ListGroups().Count(g => g.Active);
var totalCount = toolGroupRegistry.ListGroups().Count;
Console.Error.WriteLine($"[MCP] Tool groups: {activeCount} active / {totalCount} total");

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
// ROOF: Tool Group Management (meta-tools, always visible)
// ----------------------------------------------------------------------
ToolGroupTools.Register(server, toolGroupRegistry);

// ----------------------------------------------------------------------
// Start the server
// ----------------------------------------------------------------------
Console.Error.WriteLine($"[MCP] All tools registered. Starting JSON-RPC server...");
await server.RunAsync();
