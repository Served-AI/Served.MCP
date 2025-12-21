using System;
using Newtonsoft.Json.Linq;
using Served.MCP;
using Served.SDK.Client;
using Served.SDK.Models.Projects;

// Configuration - In a real app, load from env vars or config file
var baseUrl = Environment.GetEnvironmentVariable("SERVED_API_URL") ?? "https://app.served.dk";
var token = Environment.GetEnvironmentVariable("SERVED_API_TOKEN") ?? ""; 
var tenant = Environment.GetEnvironmentVariable("SERVED_TENANT") ?? "";

// SDK Initialization
var client = new ServedClient(baseUrl, token, tenant);
var server = new McpServer(client);

// ----------------------------------------------------------------------
// Tool Registration
// ----------------------------------------------------------------------

// Tool: GetProjects
server.RegisterTool("GetProjects", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? 0;
    
    // Using Fluent SDK
    var keys = await client.Projects.GetKeysAsync(new ProjectGroupingQueryParams 
    { 
        Take = 50 
    });
    
    var projects = await client.Projects.GetRangeAsync(keys);
    return projects;
});

// Tool: CreateProject
server.RegisterTool("CreateProject", async (args) =>
{
    var request = args.ToObject<CreateProjectRequest>() 
                  ?? throw new ArgumentException("Invalid arguments");

    var project = await client.Projects.CreateAsync(request);
    return project;
});

// Tool: GetProjectDetails
server.RegisterTool("GetProjectDetails", async (args) =>
{
    var projectId = args["projectId"]?.Value<int>() ?? throw new ArgumentException("ProjectId required");
    var project = await client.Projects.GetDetailedAsync(projectId);
    return project;
});

// Start Server
await server.RunAsync();
