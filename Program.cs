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
var enableTracing = Environment.GetEnvironmentVariable("SERVED_TRACING_ENABLED")?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true;

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

// ----------------------------------------------------------------------
// Start the server
// ----------------------------------------------------------------------
Console.Error.WriteLine($"[MCP] All tools registered. Starting JSON-RPC server...");
await server.RunAsync();
