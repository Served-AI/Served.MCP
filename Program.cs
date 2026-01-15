using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using Served.MCP;
using Served.SDK.Client;
using Served.SDK.Models.Projects;
using Served.SDK.Models.Dashboards;
using Served.SDK.Models.Datasource;

// Configuration - In a real app, load from env vars or config file
var baseUrl = Environment.GetEnvironmentVariable("SERVED_API_URL") ?? "https://app.served.dk";
var token = Environment.GetEnvironmentVariable("SERVED_API_TOKEN") ?? "";
var tenant = Environment.GetEnvironmentVariable("SERVED_TENANT") ?? "";

// SDK Initialization
var client = new ServedClient(baseUrl, token, tenant);
var server = new McpServer(client, baseUrl, token, tenant);

// ----------------------------------------------------------------------
// Project Tools
// ----------------------------------------------------------------------

server.RegisterTool("GetProjects", async (args) =>
{
    var query = new ProjectQueryParams { Take = 50 };
    var projects = await client.Projects.GetAllAsync(query);
    return projects;
});

server.RegisterTool("CreateProject", async (args) =>
{
    var request = args.ToObject<CreateProjectRequest>()
                  ?? throw new ArgumentException("Invalid arguments");
    var project = await client.Projects.CreateAsync(request);
    return project;
});

server.RegisterTool("GetProjectDetails", async (args) =>
{
    var projectId = args["projectId"]?.Value<int>() ?? throw new ArgumentException("ProjectId required");
    var project = await client.Projects.GetAsync(projectId);
    return project;
});

// ----------------------------------------------------------------------
// API Key Tools
// ----------------------------------------------------------------------

server.RegisterTool("GetApiKeys", async (args) =>
{
    var apiKeys = await client.ApiKeys.ListAsync();
    var sb = new StringBuilder();
    sb.AppendLine($"Her er {apiKeys.Count} API nøgler:");
    sb.AppendLine();

    foreach (var key in apiKeys)
    {
        sb.AppendLine($"@apikey[{key.Id}] {{");
        sb.AppendLine($"  name: \"{key.Name}\"");
        sb.AppendLine($"  prefix: \"{key.KeyHint}\"");
        sb.AppendLine($"  scopes: [{string.Join(", ", key.Scopes.ConvertAll(s => $"\"{s}\""))}]");
        sb.AppendLine($"  lastUsed: \"{key.LastUsedAt?.ToString("yyyy-MM-dd HH:mm") ?? "Aldrig"}\"");
        sb.AppendLine($"  status: \"{(key.IsActive ? "Aktiv" : "Inaktiv")}\"");
        sb.AppendLine("}");
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetApiKeyScopes", async (args) =>
{
    var scopes = await client.ApiKeys.GetScopesAsync();
    var sb = new StringBuilder();
    sb.AppendLine("Tilgængelige API Key scopes:");
    sb.AppendLine();
    foreach (var scope in scopes)
    {
        sb.AppendLine($"- {scope.Scope}: {scope.Description}");
    }
    return sb.ToString();
});

server.RegisterTool("CreateApiKey", async (args) =>
{
    var name = args["name"]?.Value<string>() ?? throw new ArgumentException("name required");
    var scopesToken = args["scopes"];
    var expiresInDays = args["expiresInDays"]?.Value<int>() ?? 365;

    var scopes = new List<string>();
    if (scopesToken is JArray scopesArray)
    {
        foreach (var s in scopesArray)
            scopes.Add(s.Value<string>() ?? "");
    }
    else if (scopesToken != null)
    {
        var scopeStr = scopesToken.Value<string>() ?? "";
        scopes.AddRange(scopeStr.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    if (scopes.Count == 0)
        throw new ArgumentException("scopes required (array or comma-separated string)");

    var expiresAt = DateTime.UtcNow.AddDays(expiresInDays);
    var result = await client.ApiKeys.CreateAsync(name, scopes, expiresAt);

    var sb = new StringBuilder();
    sb.AppendLine("API nøgle oprettet succesfuldt!");
    sb.AppendLine();
    sb.AppendLine($"@apikey[{result.ApiKey.Id}] {{");
    sb.AppendLine($"  name: \"{result.ApiKey.Name}\"");
    sb.AppendLine($"  prefix: \"{result.ApiKey.KeyHint}\"");
    sb.AppendLine("}");
    sb.AppendLine();
    sb.AppendLine("🔑 VIGTIG: Gem denne nøgle sikkert - den vises kun én gang:");
    sb.AppendLine();
    sb.AppendLine(result.PlainKey);
    return sb.ToString();
});

server.RegisterTool("RevokeApiKey", async (args) =>
{
    var apiKeyId = args["apiKeyId"]?.Value<int>() ?? throw new ArgumentException("apiKeyId required");
    await client.ApiKeys.DeactivateAsync(apiKeyId);
    return $"API nøgle med ID {apiKeyId} er blevet deaktiveret.";
});

// ----------------------------------------------------------------------
// Dashboard Tools (using SDK)
// ----------------------------------------------------------------------

server.RegisterTool("GetDashboards", async (args) =>
{
    var dashboards = await client.Dashboards.GetAllAsync();

    var sb = new StringBuilder();
    sb.AppendLine($"Der er {dashboards.Count} dashboards:");
    sb.AppendLine();

    foreach (var d in dashboards)
    {
        sb.AppendLine($"@dashboard[{d.Id}] {{");
        sb.AppendLine($"  name: \"{d.Name}\"");
        sb.AppendLine($"  description: \"{d.Description ?? ""}\"");
        sb.AppendLine($"  widgetCount: {d.WidgetCount}");
        sb.AppendLine($"  isDefault: {d.IsDefault}");
        sb.AppendLine($"  scope: \"{d.Scope}\"");
        sb.AppendLine("}");
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetDashboardDetails", async (args) =>
{
    var dashboardId = args["dashboardId"]?.Value<int>() ?? throw new ArgumentException("dashboardId required");
    var dashboard = await client.Dashboards.GetAsync(dashboardId);

    var sb = new StringBuilder();
    sb.AppendLine($"@dashboard[{dashboard.Id}] {{");
    sb.AppendLine($"  name: \"{dashboard.Name}\"");
    sb.AppendLine($"  description: \"{dashboard.Description ?? ""}\"");
    sb.AppendLine($"  isDefault: {dashboard.IsDefault}");
    sb.AppendLine($"  theme: \"{dashboard.Theme ?? "light"}\"");
    sb.AppendLine($"  refreshInterval: {dashboard.RefreshIntervalSeconds ?? 0}");
    sb.AppendLine($"  scope: \"{dashboard.Scope}\"");
    sb.AppendLine();
    sb.AppendLine($"  widgets: [{dashboard.Widgets.Count}] {{");
    foreach (var w in dashboard.Widgets)
    {
        sb.AppendLine($"    @widget[{w.Id}] {{");
        sb.AppendLine($"      type: \"{w.TypeName}\"");
        sb.AppendLine($"      title: \"{w.Title}\"");
        sb.AppendLine($"      position: ({w.GridX}, {w.GridY})");
        sb.AppendLine($"      size: {w.GridWidth}x{w.GridHeight}");
        sb.AppendLine($"    }}");
    }
    sb.AppendLine($"  }}");
    sb.AppendLine("}");
    return sb.ToString();
});

server.RegisterTool("CreateDashboard", async (args) =>
{
    var request = new CreateDashboardRequest
    {
        Name = args["name"]?.Value<string>() ?? throw new ArgumentException("name required"),
        Description = args["description"]?.Value<string>(),
        Theme = args["theme"]?.Value<string>(),
        RefreshIntervalSeconds = args["refreshIntervalSeconds"]?.Value<int?>(),
        WorkspaceId = args["workspaceId"]?.Value<int?>(),
        ProjectId = args["projectId"]?.Value<int?>()
    };

    var scopeStr = args["scope"]?.Value<string>();
    if (!string.IsNullOrEmpty(scopeStr) && Enum.TryParse<DashboardScope>(scopeStr, true, out var scope))
        request.Scope = scope;

    var dashboard = await client.Dashboards.CreateAsync(request);
    return $"Dashboard oprettet: @dashboard[{dashboard.Id}] \"{dashboard.Name}\"";
});

server.RegisterTool("UpdateDashboard", async (args) =>
{
    var dashboardId = args["dashboardId"]?.Value<int>() ?? throw new ArgumentException("dashboardId required");
    var request = new UpdateDashboardRequest
    {
        Name = args["name"]?.Value<string>(),
        Description = args["description"]?.Value<string>(),
        Theme = args["theme"]?.Value<string>(),
        RefreshIntervalSeconds = args["refreshIntervalSeconds"]?.Value<int?>()
    };
    await client.Dashboards.UpdateAsync(dashboardId, request);
    return $"Dashboard {dashboardId} opdateret.";
});

server.RegisterTool("DeleteDashboard", async (args) =>
{
    var dashboardId = args["dashboardId"]?.Value<int>() ?? throw new ArgumentException("dashboardId required");
    await client.Dashboards.DeleteAsync(dashboardId);
    return $"Dashboard {dashboardId} slettet.";
});

server.RegisterTool("SetDefaultDashboard", async (args) =>
{
    var dashboardId = args["dashboardId"]?.Value<int>() ?? throw new ArgumentException("dashboardId required");
    await client.Dashboards.SetDefaultAsync(dashboardId);
    return $"Dashboard {dashboardId} er nu sat som standard.";
});

server.RegisterTool("DuplicateDashboard", async (args) =>
{
    var dashboardId = args["dashboardId"]?.Value<int>() ?? throw new ArgumentException("dashboardId required");
    var newName = args["newName"]?.Value<string>();
    var dashboard = await client.Dashboards.DuplicateAsync(dashboardId, newName);
    return $"Dashboard duplikeret: @dashboard[{dashboard.Id}] \"{dashboard.Name}\"";
});

// ----------------------------------------------------------------------
// Widget Tools (using SDK)
// ----------------------------------------------------------------------

server.RegisterTool("GetWidgets", async (args) =>
{
    var dashboardId = args["dashboardId"]?.Value<int>() ?? throw new ArgumentException("dashboardId required");
    var widgets = await client.Dashboards.GetWidgetsAsync(dashboardId);

    var sb = new StringBuilder();
    sb.AppendLine($"Dashboard {dashboardId} har {widgets.Count} widgets:");
    sb.AppendLine();

    foreach (var w in widgets)
    {
        sb.AppendLine($"@widget[{w.Id}] {{");
        sb.AppendLine($"  type: \"{w.TypeName}\"");
        sb.AppendLine($"  title: \"{w.Title}\"");
        sb.AppendLine($"  subtitle: \"{w.Subtitle ?? ""}\"");
        sb.AppendLine($"  position: ({w.GridX}, {w.GridY})");
        sb.AppendLine($"  size: {w.GridWidth}x{w.GridHeight}");
        sb.AppendLine("}");
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetWidgetDetails", async (args) =>
{
    var dashboardId = args["dashboardId"]?.Value<int>() ?? throw new ArgumentException("dashboardId required");
    var widgetId = args["widgetId"]?.Value<int>() ?? throw new ArgumentException("widgetId required");
    var widget = await client.Dashboards.GetWidgetAsync(dashboardId, widgetId);

    var sb = new StringBuilder();
    sb.AppendLine($"@widget[{widget.Id}] {{");
    sb.AppendLine($"  dashboardId: {dashboardId}");
    sb.AppendLine($"  type: \"{widget.TypeName}\"");
    sb.AppendLine($"  title: \"{widget.Title}\"");
    sb.AppendLine($"  subtitle: \"{widget.Subtitle ?? ""}\"");
    sb.AppendLine($"  icon: \"{widget.Icon ?? ""}\"");
    sb.AppendLine($"  position: ({widget.GridX}, {widget.GridY})");
    sb.AppendLine($"  size: {widget.GridWidth}x{widget.GridHeight}");
    sb.AppendLine();
    sb.AppendLine($"  config: {widget.Config ?? "{}"}");
    sb.AppendLine($"  datasourceConfig: {widget.DatasourceConfig ?? "{}"}");
    sb.AppendLine($"  styleConfig: {widget.StyleConfig ?? "{}"}");
    sb.AppendLine("}");
    return sb.ToString();
});

server.RegisterTool("CreateWidget", async (args) =>
{
    var dashboardId = args["dashboardId"]?.Value<int>() ?? throw new ArgumentException("dashboardId required");
    var request = new CreateWidgetRequest
    {
        Type = args["type"]?.Value<string>() ?? throw new ArgumentException("type required"),
        Title = args["title"]?.Value<string>() ?? throw new ArgumentException("title required"),
        Subtitle = args["subtitle"]?.Value<string>(),
        Icon = args["icon"]?.Value<string>(),
        GridX = args["gridX"]?.Value<int>() ?? 0,
        GridY = args["gridY"]?.Value<int>() ?? 0,
        GridWidth = args["gridWidth"]?.Value<int>() ?? 3,
        GridHeight = args["gridHeight"]?.Value<int>() ?? 2,
        Config = args["config"],
        DatasourceConfig = args["datasourceConfig"]
    };
    var widget = await client.Dashboards.AddWidgetAsync(dashboardId, request);
    return $"Widget oprettet: @widget[{widget.Id}] \"{widget.Title}\" ({widget.TypeName})";
});

server.RegisterTool("UpdateWidget", async (args) =>
{
    var dashboardId = args["dashboardId"]?.Value<int>() ?? throw new ArgumentException("dashboardId required");
    var widgetId = args["widgetId"]?.Value<int>() ?? throw new ArgumentException("widgetId required");
    var request = new UpdateWidgetRequest
    {
        Title = args["title"]?.Value<string>(),
        Subtitle = args["subtitle"]?.Value<string>(),
        Icon = args["icon"]?.Value<string>(),
        GridX = args["gridX"]?.Value<int?>(),
        GridY = args["gridY"]?.Value<int?>(),
        GridWidth = args["gridWidth"]?.Value<int?>(),
        GridHeight = args["gridHeight"]?.Value<int?>(),
        Config = args["config"],
        DatasourceConfig = args["datasourceConfig"],
        StyleConfig = args["styleConfig"]
    };
    await client.Dashboards.UpdateWidgetAsync(dashboardId, widgetId, request);
    return $"Widget {widgetId} opdateret.";
});

server.RegisterTool("DeleteWidget", async (args) =>
{
    var dashboardId = args["dashboardId"]?.Value<int>() ?? throw new ArgumentException("dashboardId required");
    var widgetId = args["widgetId"]?.Value<int>() ?? throw new ArgumentException("widgetId required");
    await client.Dashboards.DeleteWidgetAsync(dashboardId, widgetId);
    return $"Widget {widgetId} slettet fra dashboard {dashboardId}.";
});

server.RegisterTool("UpdateWidgetLayout", async (args) =>
{
    var dashboardId = args["dashboardId"]?.Value<int>() ?? throw new ArgumentException("dashboardId required");
    var layoutsArray = args["layouts"] as JArray ?? throw new ArgumentException("layouts required");

    var layouts = new List<WidgetLayoutItem>();
    foreach (var item in layoutsArray)
    {
        layouts.Add(new WidgetLayoutItem
        {
            WidgetId = item["widgetId"]?.Value<int>() ?? 0,
            GridX = item["gridX"]?.Value<int>() ?? 0,
            GridY = item["gridY"]?.Value<int>() ?? 0,
            GridWidth = item["gridWidth"]?.Value<int>() ?? 3,
            GridHeight = item["gridHeight"]?.Value<int>() ?? 2
        });
    }

    await client.Dashboards.UpdateWidgetLayoutAsync(dashboardId, layouts);
    return $"Layout opdateret for {layouts.Count} widgets.";
});

// ----------------------------------------------------------------------
// Datasource / Query Builder Tools (using SDK)
// ----------------------------------------------------------------------

server.RegisterTool("GetDatasourceEntities", async (args) =>
{
    var category = args["category"]?.Value<string>();
    var entities = category != null
        ? await client.Datasource.GetEntitiesByCategoryAsync(category)
        : await client.Datasource.GetEntitiesAsync();

    var sb = new StringBuilder();
    sb.AppendLine($"Tilgængelige entities ({entities.Count}):");
    sb.AppendLine();

    var grouped = entities.GroupBy(e => e.Category ?? "Andet");
    foreach (var group in grouped)
    {
        sb.AppendLine($"## {group.Key}");
        foreach (var entity in group)
        {
            sb.AppendLine($"  - {entity.Name}: {entity.DisplayName}");
        }
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetEntitySchema", async (args) =>
{
    var entityName = args["entityName"]?.Value<string>() ?? throw new ArgumentException("entityName required");
    var schema = await client.Datasource.GetEntitySchemaAsync(entityName);

    var sb = new StringBuilder();
    sb.AppendLine($"@entity[{schema.Name}] {{");
    sb.AppendLine($"  displayName: \"{schema.DisplayName}\"");
    sb.AppendLine($"  category: \"{schema.Category}\"");
    sb.AppendLine();
    sb.AppendLine($"  fields: [{schema.Fields.Count}] {{");
    foreach (var field in schema.Fields)
    {
        var flags = new List<string>();
        if (field.IsFilterable) flags.Add("filterable");
        if (field.IsSortable) flags.Add("sortable");
        if (field.IsGroupable) flags.Add("groupable");
        var flagStr = flags.Count > 0 ? $" [{string.Join(", ", flags)}]" : "";
        sb.AppendLine($"    {field.Name} ({field.DataType}): \"{field.DisplayName}\"{flagStr}");
    }
    sb.AppendLine($"  }}");

    if (schema.Relations.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine($"  relations: [{schema.Relations.Count}] {{");
        foreach (var rel in schema.Relations)
        {
            sb.AppendLine($"    {rel.Name} -> {rel.TargetEntity} ({rel.RelationType})");
        }
        sb.AppendLine($"  }}");
    }
    sb.AppendLine("}");
    return sb.ToString();
});

server.RegisterTool("ExecuteDatasourceQuery", async (args) =>
{
    var entity = args["entity"]?.Value<string>() ?? throw new ArgumentException("entity required");

    // Build query using SDK helper
    var query = client.Datasource.CreateQuery(entity);
    query = client.Datasource.SetPagination(query,
        args["limit"]?.Value<int>() ?? 50,
        args["offset"]?.Value<int>() ?? 0);

    // Add fields if specified
    if (args["fields"] is JArray fieldsArray)
    {
        foreach (var f in fieldsArray)
        {
            var fieldName = f["name"]?.Value<string>() ?? f.Value<string>();
            if (!string.IsNullOrEmpty(fieldName))
                query = client.Datasource.AddField(query, fieldName, f["alias"]?.Value<string>());
        }
    }

    // Add filters if specified
    if (args["filters"] is JArray filtersArray)
    {
        foreach (var f in filtersArray)
        {
            query = client.Datasource.AddFilter(query,
                f["field"]?.Value<string>() ?? "",
                f["operator"]?.Value<string>() ?? "eq",
                f["value"],
                f["logicalOperator"]?.Value<string>());
        }
    }

    // Add sorting if specified
    if (args["sorting"] is JArray sortingArray)
    {
        foreach (var s in sortingArray)
        {
            query = client.Datasource.AddSort(query,
                s["field"]?.Value<string>() ?? "",
                s["direction"]?.Value<string>() ?? "asc");
        }
    }

    // Add groupBy if specified
    if (args["groupBy"] is JArray groupByArray)
    {
        foreach (var g in groupByArray)
        {
            query = client.Datasource.AddGroupBy(query,
                g["field"]?.Value<string>() ?? "",
                g["datePart"]?.Value<string>());
        }
    }

    // Add aggregations if specified
    if (args["aggregations"] is JArray aggArray)
    {
        foreach (var a in aggArray)
        {
            query = client.Datasource.AddAggregation(query,
                a["field"]?.Value<string>() ?? "",
                a["function"]?.Value<string>() ?? "count",
                a["alias"]?.Value<string>());
        }
    }

    var result = await client.Datasource.ExecuteQueryAsync(query);

    var sb = new StringBuilder();
    sb.AppendLine($"Query resultat: {result.Meta?.ReturnedCount} / {result.Meta?.TotalCount} rækker");
    sb.AppendLine($"Udført på {result.Meta?.ExecutionTimeMs}ms");
    sb.AppendLine();

    if (result.Meta?.Columns != null && result.Meta.Columns.Count > 0)
    {
        // Header
        sb.AppendLine(string.Join(" | ", result.Meta.Columns.Select(c => c.DisplayName)));
        sb.AppendLine(new string('-', 80));

        // Rows (max 20 for display)
        foreach (var row in result.Data.Take(20))
        {
            var values = result.Meta.Columns.Select(c => row.TryGetValue(c.Name, out var v) ? v?.ToString() ?? "" : "");
            sb.AppendLine(string.Join(" | ", values));
        }

        if (result.Data.Count > 20)
        {
            sb.AppendLine($"... og {result.Data.Count - 20} flere rækker");
        }
    }
    return sb.ToString();
});

server.RegisterTool("PreviewDatasourceQuery", async (args) =>
{
    var entity = args["entity"]?.Value<string>() ?? throw new ArgumentException("entity required");
    var maxRows = args["maxRows"]?.Value<int>() ?? 10;

    var query = client.Datasource.CreateQuery(entity);
    query = client.Datasource.SetPagination(query, maxRows, 0);

    var result = await client.Datasource.PreviewQueryAsync(query, maxRows);
    return result;
});

server.RegisterTool("ValidateDatasourceQuery", async (args) =>
{
    var entity = args["entity"]?.Value<string>() ?? throw new ArgumentException("entity required");
    var query = client.Datasource.CreateQuery(entity);

    var result = await client.Datasource.ValidateQueryAsync(query);

    if (result.IsValid)
    {
        return "Query er valid og kan udføres.";
    }
    else
    {
        var sb = new StringBuilder();
        sb.AppendLine("Query validation fejlede:");
        foreach (var error in result.Errors)
        {
            sb.AppendLine($"  - {error}");
        }
        return sb.ToString();
    }
});

server.RegisterTool("GetDatasourceCategories", async (args) =>
{
    var categories = await client.Datasource.GetCategoriesAsync();
    var sb = new StringBuilder();
    sb.AppendLine("Tilgængelige entity kategorier:");
    foreach (var cat in categories)
    {
        sb.AppendLine($"  - {cat}");
    }
    return sb.ToString();
});

// ----------------------------------------------------------------------
// Task Tools (using SDK)
// ----------------------------------------------------------------------

server.RegisterTool("GetTasks", async (args) =>
{
    var projectId = args["projectId"]?.Value<int?>();
    var includeCompleted = args["includeCompleted"]?.Value<bool>() ?? false;
    var limit = args["limit"]?.Value<int>() ?? 50;

    List<Served.SDK.Models.Tasks.TaskSummary> tasks;
    if (projectId.HasValue)
    {
        tasks = await client.Tasks.GetByProjectAsync(projectId.Value, includeCompleted);
        if (tasks.Count > limit) tasks = tasks.Take(limit).ToList();
    }
    else
    {
        var query = new Served.SDK.Models.Tasks.TaskQueryParams
        {
            Take = limit,
            IncludeCompleted = includeCompleted
        };
        tasks = await client.Tasks.GetAllAsync(query);
    }

    var sb = new StringBuilder();
    sb.AppendLine($"Fundet {tasks.Count} opgaver:");
    sb.AppendLine();

    foreach (var t in tasks)
    {
        sb.AppendLine($"@task[{t.Id}] {{");
        sb.AppendLine($"  name: \"{t.Name}\"");
        sb.AppendLine($"  taskNo: \"{t.TaskNo ?? ""}\"");
        sb.AppendLine($"  projectId: {t.ProjectId}");
        sb.AppendLine($"  status: \"{t.Status}\"");
        sb.AppendLine($"  dueDate: \"{t.DueDate?.ToString("yyyy-MM-dd") ?? ""}\"");
        sb.AppendLine($"  isCompleted: {t.IsCompleted}");
        sb.AppendLine("}");
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetTaskDetails", async (args) =>
{
    var taskId = args["taskId"]?.Value<int>() ?? throw new ArgumentException("taskId required");
    var task = await client.Tasks.GetAsync(taskId);

    var sb = new StringBuilder();
    sb.AppendLine($"@task[{task.Id}] {{");
    sb.AppendLine($"  name: \"{task.Name}\"");
    sb.AppendLine($"  taskNo: \"{task.TaskNo ?? ""}\"");
    sb.AppendLine($"  description: \"{task.Description ?? ""}\"");
    sb.AppendLine($"  projectId: {task.ProjectId}");
    sb.AppendLine($"  projectName: \"{task.ProjectName ?? ""}\"");
    sb.AppendLine($"  status: \"{task.Status}\"");
    sb.AppendLine($"  priority: \"{task.Priority}\"");
    sb.AppendLine($"  assignedTo: {task.AssignedTo?.ToString() ?? "null"}");
    sb.AppendLine($"  dueDate: \"{task.DueDate?.ToString("yyyy-MM-dd") ?? ""}\"");
    sb.AppendLine($"  estimatedHours: {task.EstimatedHours?.ToString() ?? "null"}");
    sb.AppendLine($"  progress: {task.Progress}");
    sb.AppendLine($"  isCompleted: {task.IsCompleted}");
    sb.AppendLine("}");
    return sb.ToString();
});

server.RegisterTool("CreateTask", async (args) =>
{
    var priorityStr = args["priority"]?.Value<string>();
    Served.SDK.Models.Tasks.TaskPriority? priority = null;
    if (!string.IsNullOrEmpty(priorityStr) && Enum.TryParse<Served.SDK.Models.Tasks.TaskPriority>(priorityStr, true, out var p))
        priority = p;

    var request = new Served.SDK.Models.Tasks.CreateTaskRequest
    {
        Name = args["name"]?.Value<string>() ?? throw new ArgumentException("name required"),
        ProjectId = args["projectId"]?.Value<int>() ?? throw new ArgumentException("projectId required"),
        Description = args["description"]?.Value<string>(),
        AssignedTo = args["assignedTo"]?.Value<int?>(),
        Priority = priority,
        DueDate = args["dueDate"]?.Value<DateTime?>(),
        EstimatedHours = args["estimatedHours"]?.Value<double?>()
    };

    var task = await client.Tasks.CreateAsync(request);
    return $"Opgave oprettet: @task[{task.Id}] \"{task.Name}\"";
});

server.RegisterTool("UpdateTask", async (args) =>
{
    var taskId = args["taskId"]?.Value<int>() ?? throw new ArgumentException("taskId required");

    var statusStr = args["status"]?.Value<string>();
    Served.SDK.Models.Tasks.TaskStatus? status = null;
    if (!string.IsNullOrEmpty(statusStr) && Enum.TryParse<Served.SDK.Models.Tasks.TaskStatus>(statusStr, true, out var s))
        status = s;

    var priorityStr = args["priority"]?.Value<string>();
    Served.SDK.Models.Tasks.TaskPriority? priority = null;
    if (!string.IsNullOrEmpty(priorityStr) && Enum.TryParse<Served.SDK.Models.Tasks.TaskPriority>(priorityStr, true, out var p))
        priority = p;

    var request = new Served.SDK.Models.Tasks.UpdateTaskRequest
    {
        Name = args["name"]?.Value<string>(),
        Description = args["description"]?.Value<string>(),
        Status = status,
        Priority = priority,
        AssignedTo = args["assignedTo"]?.Value<int?>(),
        DueDate = args["dueDate"]?.Value<DateTime?>(),
        EstimatedHours = args["estimatedHours"]?.Value<double?>(),
        Progress = args["progress"]?.Value<double?>()
    };
    await client.Tasks.UpdateAsync(taskId, request);
    return $"Opgave {taskId} opdateret.";
});

server.RegisterTool("DeleteTask", async (args) =>
{
    var taskId = args["taskId"]?.Value<int>() ?? throw new ArgumentException("taskId required");
    await client.Tasks.DeleteAsync(taskId);
    return $"Opgave {taskId} slettet.";
});

server.RegisterTool("UpdateTaskStatus", async (args) =>
{
    var taskId = args["taskId"]?.Value<int>() ?? throw new ArgumentException("taskId required");
    var statusStr = args["status"]?.Value<string>() ?? throw new ArgumentException("status required");
    if (!Enum.TryParse<Served.SDK.Models.Tasks.TaskStatus>(statusStr, true, out var status))
        throw new ArgumentException($"Invalid status: {statusStr}. Valid values: New, InProgress, Completed, OnHold, Cancelled");
    var request = new Served.SDK.Models.Tasks.UpdateTaskStatusRequest { Status = status };
    await client.Tasks.UpdateStatusAsync(taskId, request);
    return $"Status for opgave {taskId} ændret til '{status}'.";
});

// ----------------------------------------------------------------------
// Time Registration Tools (using SDK)
// ----------------------------------------------------------------------

server.RegisterTool("GetTimeRegistrations", async (args) =>
{
    var startDate = args["startDate"]?.Value<DateTime?>();
    var endDate = args["endDate"]?.Value<DateTime?>();
    var projectId = args["projectId"]?.Value<int?>();
    var limit = args["limit"]?.Value<int>() ?? 50;

    List<Served.SDK.Models.TimeRegistration.TimeRegistrationDetail> registrations;

    if (startDate.HasValue && endDate.HasValue)
    {
        registrations = await client.TimeRegistrations.GetByDateRangeAsync(startDate.Value, endDate.Value, limit);
    }
    else if (projectId.HasValue)
    {
        registrations = await client.TimeRegistrations.GetByProjectAsync(projectId.Value, limit);
    }
    else
    {
        var query = new Served.SDK.Models.TimeRegistration.TimeRegistrationQueryParams { Take = limit };
        registrations = await client.TimeRegistrations.GetAllAsync(query);
    }

    var sb = new StringBuilder();
    sb.AppendLine($"Fundet {registrations.Count} tidsregistreringer:");
    sb.AppendLine();

    foreach (var r in registrations)
    {
        sb.AppendLine($"@timereg[{r.Id}] {{");
        sb.AppendLine($"  date: \"{r.Date.ToString("yyyy-MM-dd")}\"");
        sb.AppendLine($"  hours: {(r.Hours ?? 0):F2}");
        sb.AppendLine($"  projectId: {r.ProjectId}");
        sb.AppendLine($"  taskId: {r.TaskId?.ToString() ?? "null"}");
        sb.AppendLine($"  comment: \"{r.Comment ?? ""}\"");
        sb.AppendLine($"  billable: {r.Billable}");
        sb.AppendLine("}");
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("CreateTimeRegistration", async (args) =>
{
    var start = args["start"]?.Value<DateTime?>() ?? args["date"]?.Value<DateTime?>() ?? DateTime.Today;
    var end = args["end"]?.Value<DateTime?>() ?? start.AddHours(1);
    var minutes = args["minutes"]?.Value<int>() ?? (int)((args["hours"]?.Value<double>() ?? 1) * 60);
    var billable = args["billable"]?.Value<bool>() ?? true;

    var request = new Served.SDK.Models.TimeRegistration.CreateTimeRegistrationRequest
    {
        ProjectId = args["projectId"]?.Value<int>(),
        TaskId = args["taskId"]?.Value<int?>(),
        Start = start,
        End = end,
        Minutes = minutes,
        Description = args["description"]?.Value<string>(),
        Billable = billable
    };

    var reg = await client.TimeRegistrations.CreateAsync(request);
    return $"Tidsregistrering oprettet: @timereg[{reg.Id}] ({reg.Hours:F2} timer)";
});

server.RegisterTool("UpdateTimeRegistration", async (args) =>
{
    var id = args["id"]?.Value<int>() ?? throw new ArgumentException("id required");
    var request = new Served.SDK.Models.TimeRegistration.UpdateTimeRegistrationRequest
    {
        TaskId = args["taskId"]?.Value<int?>(),
        ProjectId = args["projectId"]?.Value<int?>(),
        Start = args["start"]?.Value<DateTime?>(),
        End = args["end"]?.Value<DateTime?>(),
        Description = args["description"]?.Value<string>(),
        Billable = args["billable"]?.Value<bool?>()
    };
    await client.TimeRegistrations.UpdateAsync(id, request);
    return $"Tidsregistrering {id} opdateret.";
});

server.RegisterTool("DeleteTimeRegistration", async (args) =>
{
    var id = args["id"]?.Value<int>() ?? throw new ArgumentException("id required");
    await client.TimeRegistrations.DeleteAsync(id);
    return $"Tidsregistrering {id} slettet.";
});

// ----------------------------------------------------------------------
// Customer Tools (using SDK)
// ----------------------------------------------------------------------

server.RegisterTool("GetCustomers", async (args) =>
{
    var search = args["search"]?.Value<string>();
    var limit = args["limit"]?.Value<int>() ?? 50;

    List<Served.SDK.Models.Customers.CustomerSummary> customers;
    if (!string.IsNullOrEmpty(search))
    {
        customers = await client.Customers.SearchAsync(search, limit);
    }
    else
    {
        var query = new Served.SDK.Models.Customers.CustomerQueryParams { Take = limit };
        customers = await client.Customers.GetAllAsync(query);
    }

    var sb = new StringBuilder();
    sb.AppendLine($"Fundet {customers.Count} kunder:");
    sb.AppendLine();

    foreach (var c in customers)
    {
        sb.AppendLine($"@customer[{c.Id}] {{");
        sb.AppendLine($"  name: \"{c.Name}\"");
        sb.AppendLine($"  customerNo: \"{c.CustomerNo ?? ""}\"");
        sb.AppendLine($"  email: \"{c.Email ?? ""}\"");
        sb.AppendLine($"  phone: \"{c.Phone ?? ""}\"");
        sb.AppendLine($"  isActive: {c.IsActive}");
        sb.AppendLine("}");
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetCustomerDetails", async (args) =>
{
    var customerId = args["customerId"]?.Value<int>() ?? throw new ArgumentException("customerId required");
    var customer = await client.Customers.GetAsync(customerId);

    var sb = new StringBuilder();
    sb.AppendLine($"@customer[{customer.Id}] {{");
    sb.AppendLine($"  name: \"{customer.Name}\"");
    sb.AppendLine($"  customerNo: \"{customer.CustomerNo ?? ""}\"");
    sb.AppendLine($"  email: \"{customer.Email ?? ""}\"");
    sb.AppendLine($"  phone: \"{customer.Phone ?? ""}\"");
    sb.AppendLine($"  website: \"{customer.Website ?? ""}\"");
    sb.AppendLine($"  vatNumber: \"{customer.VatNumber ?? ""}\"");
    sb.AppendLine($"  address: \"{customer.Address ?? ""}\"");
    sb.AppendLine($"  city: \"{customer.City ?? ""}\"");
    sb.AppendLine($"  postalCode: \"{customer.PostalCode ?? ""}\"");
    sb.AppendLine($"  country: \"{customer.Country ?? ""}\"");
    sb.AppendLine($"  isActive: {customer.IsActive}");
    sb.AppendLine("}");
    return sb.ToString();
});

// ----------------------------------------------------------------------
// Agent Discovery & Coordination Tools (for Atlas)
// ----------------------------------------------------------------------

server.RegisterTool("GetActiveAgents", async (args) =>
{
    var statusFilter = args["status"]?.Value<string>();
    var agentType = args["agentType"]?.Value<string>();
    var isOnline = args["isOnline"]?.Value<bool?>();

    var criteria = new JObject();
    if (!string.IsNullOrEmpty(statusFilter)) criteria["status"] = statusFilter;
    if (!string.IsNullOrEmpty(agentType)) criteria["agentType"] = agentType;
    if (isOnline.HasValue) criteria["isOnline"] = isOnline.Value;

    var response = await server.Http.PostAsync("/api/agents/coordination/GetActiveAgents",
        new StringContent(criteria.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get active agents: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var agents = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"Fundet {agents.Count} aktive agenter:");
    sb.AppendLine();

    foreach (var agent in agents)
    {
        sb.AppendLine($"@agent[{agent["agentId"]}] {{");
        sb.AppendLine($"  name: \"{agent["agentName"]}\"");
        sb.AppendLine($"  type: \"{agent["agentType"] ?? ""}\"");
        sb.AppendLine($"  status: \"{agent["status"]}\"");
        sb.AppendLine($"  currentActivity: \"{agent["currentActivity"] ?? ""}\"");
        sb.AppendLine($"  progressPercent: {agent["progressPercent"] ?? 0}");
        sb.AppendLine($"  currentTaskId: \"{agent["currentTaskId"] ?? ""}\"");
        sb.AppendLine($"  currentBranch: \"{agent["currentBranch"] ?? ""}\"");
        sb.AppendLine($"  activeSessionId: {agent["activeSessionId"] ?? "null"}");
        sb.AppendLine($"  lastActivityAt: \"{agent["lastActivityAt"] ?? ""}\"");
        sb.AppendLine("}");
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetAgentContext", async (args) =>
{
    var agentId = args["agentId"]?.Value<int>() ?? throw new ArgumentException("agentId required");

    var response = await server.Http.PostAsync($"/api/agents/coordination/GetAgentContext?agentId={agentId}",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get agent context: {response.StatusCode}");

    var context = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"@agentContext[{context["agentId"]}] {{");
    sb.AppendLine($"  agentName: \"{context["agentName"]}\"");
    sb.AppendLine($"  sessionId: {context["sessionId"] ?? "null"}");
    sb.AppendLine($"  taskId: \"{context["taskId"] ?? ""}\"");
    sb.AppendLine($"  taskName: \"{context["taskName"] ?? ""}\"");
    sb.AppendLine();

    // Active Files
    var files = context["activeFiles"] as JArray ?? new JArray();
    sb.AppendLine($"  activeFiles: [{files.Count}] {{");
    foreach (var file in files.Take(10))
    {
        sb.AppendLine($"    {file["lastOperation"]}: {file["filePath"]}");
    }
    if (files.Count > 10) sb.AppendLine($"    ... and {files.Count - 10} more");
    sb.AppendLine("  }");
    sb.AppendLine();

    // Todo List
    var todos = context["todoList"] as JArray ?? new JArray();
    sb.AppendLine($"  todoList: [{todos.Count}] {{");
    foreach (var todo in todos)
    {
        var status = todo["status"]?.ToString();
        var marker = status == "Completed" ? "[x]" : status == "InProgress" ? "[>]" : "[ ]";
        sb.AppendLine($"    {marker} {todo["content"]}");
    }
    sb.AppendLine("  }");
    sb.AppendLine();

    // Git State
    var git = context["gitState"];
    if (git != null)
    {
        sb.AppendLine($"  gitState: {{");
        sb.AppendLine($"    branch: \"{git["branch"] ?? ""}\"");
        sb.AppendLine($"    uncommittedChanges: {git["uncommittedChanges"] ?? 0}");
        sb.AppendLine($"    commitCount: {git["commitCount"] ?? 0}");
        sb.AppendLine($"    prUrl: \"{git["pullRequestUrl"] ?? ""}\"");
        sb.AppendLine($"    ciStatus: \"{git["ciStatus"] ?? ""}\"");
        sb.AppendLine("  }");
    }

    // Recent Actions
    var actions = context["recentActions"] as JArray ?? new JArray();
    sb.AppendLine($"  recentActions: [{actions.Count}] {{");
    foreach (var action in actions.Take(5))
    {
        sb.AppendLine($"    [{action["timestamp"]}] {action["actionType"]}: {action["description"]}");
    }
    sb.AppendLine("  }");

    sb.AppendLine("}");
    return sb.ToString();
});

server.RegisterTool("SearchAgentActivity", async (args) =>
{
    var query = args["query"]?.Value<string>() ?? throw new ArgumentException("query required");
    var limit = args["limit"]?.Value<int>() ?? 20;

    var response = await server.Http.PostAsync($"/api/agents/coordination/SearchActivity?query={Uri.EscapeDataString(query)}&limit={limit}",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to search agent activity: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var results = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"Søgeresultater for '{query}': {results.Count} hits");
    sb.AppendLine();

    foreach (var r in results)
    {
        sb.AppendLine($"[{r["timestamp"]}] Agent #{r["agentId"]} ({r["agentName"]}):");
        sb.AppendLine($"  Type: {r["activityType"]}");
        sb.AppendLine($"  Description: {r["description"]}");
        if (r["filePath"] != null) sb.AppendLine($"  File: {r["filePath"]}");
        if (r["taskId"] != null) sb.AppendLine($"  Task: {r["taskId"]}");
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetCoordinationInfo", async (args) =>
{
    var response = await server.Http.PostAsync("/api/agents/coordination/GetCoordinationInfo",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get coordination info: {response.StatusCode}");

    var info = JObject.Parse(await response.Content.ReadAsStringAsync());
    var agents = info["activeAgents"] as JArray ?? new JArray();
    var tasks = info["taskAssignments"] as JArray ?? new JArray();
    var files = info["filesInUse"] as JArray ?? new JArray();
    var conflicts = info["potentialConflicts"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine("=== Agent Coordination Overview ===");
    sb.AppendLine();

    sb.AppendLine($"## Active Agents ({agents.Count})");
    foreach (var agent in agents)
    {
        sb.AppendLine($"  - Agent #{agent["agentId"]} ({agent["agentName"]}): {agent["status"]} - {agent["currentActivity"] ?? "Idle"}");
    }
    sb.AppendLine();

    sb.AppendLine($"## Task Assignments ({tasks.Count})");
    foreach (var task in tasks)
    {
        sb.AppendLine($"  - Task {task["taskId"]} -> Agent #{task["agentId"]} ({task["agentName"]}) [{task["progressPercent"] ?? 0}%]");
    }
    sb.AppendLine();

    sb.AppendLine($"## Files In Use ({files.Count})");
    foreach (var file in files.Take(10))
    {
        sb.AppendLine($"  - {file["filePath"]} ({file["operation"]}) by Agent #{file["agentId"]}");
    }
    if (files.Count > 10) sb.AppendLine($"  ... and {files.Count - 10} more");
    sb.AppendLine();

    if (conflicts.Count > 0)
    {
        sb.AppendLine($"## ⚠️ Potential Conflicts ({conflicts.Count})");
        foreach (var conflict in conflicts)
        {
            sb.AppendLine($"  - [{conflict["severity"]}] {conflict["type"]}: {conflict["description"]}");
            sb.AppendLine($"    Agents: {string.Join(", ", (conflict["agentIds"] as JArray ?? new JArray()).Select(a => $"#{a}"))}");
        }
    }
    else
    {
        sb.AppendLine("## ✓ No conflicts detected");
    }

    return sb.ToString();
});

server.RegisterTool("CoordinateWithAgent", async (args) =>
{
    var fromAgentId = args["fromAgentId"]?.Value<int>() ?? throw new ArgumentException("fromAgentId required");
    var targetAgentId = args["targetAgentId"]?.Value<int>() ?? throw new ArgumentException("targetAgentId required");
    var action = args["action"]?.Value<string>() ?? throw new ArgumentException("action required");
    var message = args["message"]?.Value<string>();
    var reason = args["reason"]?.Value<string>();

    var request = new JObject
    {
        ["targetAgentId"] = targetAgentId,
        ["action"] = action,
        ["message"] = message,
        ["reason"] = reason
    };

    var response = await server.Http.PostAsync($"/api/agents/coordination/Coordinate?fromAgentId={fromAgentId}",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to send coordination request: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine("Coordination Request Result:");
    sb.AppendLine($"  Success: {result["success"]}");
    sb.AppendLine($"  Status: {result["status"]}");
    sb.AppendLine($"  Message: {result["message"]}");
    return sb.ToString();
});

server.RegisterTool("GetFilesInUse", async (args) =>
{
    var pathFilter = args["pathFilter"]?.Value<string>();
    var url = "/api/agents/coordination/GetFilesInUse";
    if (!string.IsNullOrEmpty(pathFilter)) url += $"?pathFilter={Uri.EscapeDataString(pathFilter)}";

    var response = await server.Http.PostAsync(url,
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get files in use: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var files = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"Filer i brug ({files.Count}):");
    sb.AppendLine();

    foreach (var file in files)
    {
        sb.AppendLine($"  {file["filePath"]}");
        sb.AppendLine($"    Operation: {file["operation"]} by Agent #{file["agentId"]} ({file["agentName"]})");
        sb.AppendLine($"    Since: {file["since"]}");
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("DetectConflicts", async (args) =>
{
    var response = await server.Http.PostAsync("/api/agents/coordination/DetectConflicts",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to detect conflicts: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var conflicts = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    if (conflicts.Count == 0)
    {
        sb.AppendLine("✓ Ingen konflikter detekteret");
    }
    else
    {
        sb.AppendLine($"⚠️ {conflicts.Count} potentielle konflikter detekteret:");
        sb.AppendLine();

        foreach (var conflict in conflicts)
        {
            sb.AppendLine($"@conflict {{");
            sb.AppendLine($"  type: {conflict["type"]}");
            sb.AppendLine($"  severity: {conflict["severity"]}");
            sb.AppendLine($"  description: {conflict["description"]}");
            sb.AppendLine($"  agents: [{string.Join(", ", (conflict["agentIds"] as JArray ?? new JArray()).Select(a => $"#{a}"))}]");
            if (conflict["filePath"] != null) sb.AppendLine($"  file: {conflict["filePath"]}");
            if (conflict["taskId"] != null) sb.AppendLine($"  task: {conflict["taskId"]}");
            sb.AppendLine("}");
            sb.AppendLine();
        }
    }
    return sb.ToString();
});

// ----------------------------------------------------------------------
// Integration Management Tools (for Atlas)
// ----------------------------------------------------------------------

server.RegisterTool("GetAvailableIntegrations", async (args) =>
{
    var category = args["category"]?.Value<string>();

    var url = "/api/integrations/available";
    if (!string.IsNullOrEmpty(category)) url += $"?category={Uri.EscapeDataString(category)}";

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get integrations: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var integrations = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"Tilgængelige integrationer ({integrations.Count}):");
    sb.AppendLine();

    var grouped = integrations.GroupBy(i => i["category"]?.ToString() ?? "Other");
    foreach (var group in grouped)
    {
        sb.AppendLine($"## {group.Key}");
        foreach (var integration in group)
        {
            sb.AppendLine($"  @integration[{integration["integrationId"]}] {{");
            sb.AppendLine($"    name: \"{integration["name"]}\"");
            sb.AppendLine($"    displayName: \"{integration["displayName"]}\"");
            sb.AppendLine($"    description: \"{integration["description"] ?? ""}\"");
            sb.AppendLine($"    capabilities: [{string.Join(", ", (integration["capabilities"] as JArray ?? new JArray()).Select(c => c.ToString()))}]");
            sb.AppendLine($"    requiresApiKey: {integration["requiresApiKey"] ?? false}");
            sb.AppendLine($"    supportsOAuth: {integration["supportsOAuth"] ?? false}");
            sb.AppendLine($"  }}");
        }
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetConfiguredIntegrations", async (args) =>
{
    var category = args["category"]?.Value<string>();
    var onlyActive = args["onlyActive"]?.Value<bool>() ?? false;

    var url = "/api/integrations/configured";
    var queryParams = new List<string>();
    if (!string.IsNullOrEmpty(category)) queryParams.Add($"category={Uri.EscapeDataString(category)}");
    if (onlyActive) queryParams.Add("onlyActive=true");
    if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get configured integrations: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var handlers = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"Konfigurerede integrationer ({handlers.Count}):");
    sb.AppendLine();

    foreach (var handler in handlers)
    {
        var status = handler["status"];
        var statusIcon = status?["isConnected"]?.Value<bool>() == true ? "✓" : "✗";

        sb.AppendLine($"@integrationHandler[{handler["id"]}] {{");
        sb.AppendLine($"  name: \"{handler["name"] ?? handler["integrationId"]}\"");
        sb.AppendLine($"  integrationId: \"{handler["integrationId"]}\"");
        sb.AppendLine($"  displayName: \"{handler["displayName"]}\"");
        sb.AppendLine($"  category: \"{handler["category"] ?? ""}\"");
        sb.AppendLine($"  status: {statusIcon} {(status?["isConnected"]?.Value<bool>() == true ? "Connected" : "Disconnected")}");
        sb.AppendLine($"  isActivated: {handler["isActivated"] ?? false}");
        if (status?["hasError"]?.Value<bool>() == true)
        {
            sb.AppendLine($"  error: \"{status["errorMessage"]}\"");
        }
        sb.AppendLine($"  createdAt: \"{handler["createdAt"] ?? ""}\"");
        sb.AppendLine($"}}");
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetIntegrationStatus", async (args) =>
{
    var handlerId = args["handlerId"]?.Value<int>() ?? throw new ArgumentException("handlerId required");

    var response = await server.Http.GetAsync($"/api/integrations/handlers/{handlerId}/status");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get integration status: {response.StatusCode}");

    var status = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"@integrationStatus[{handlerId}] {{");
    sb.AppendLine($"  isConnected: {status["isConnected"] ?? false}");
    sb.AppendLine($"  isActivated: {status["isActivated"] ?? false}");
    sb.AppendLine($"  isConfigured: {status["isConfigured"] ?? false}");
    sb.AppendLine($"  hasError: {status["hasError"] ?? false}");
    if (status["errorMessage"] != null)
    {
        sb.AppendLine($"  errorMessage: \"{status["errorMessage"]}\"");
    }
    sb.AppendLine($"  lastCheckedAt: \"{status["lastCheckedAt"] ?? DateTime.UtcNow}\"");
    sb.AppendLine($"}}");
    return sb.ToString();
});

server.RegisterTool("TestIntegrationConnection", async (args) =>
{
    var handlerId = args["handlerId"]?.Value<int>() ?? throw new ArgumentException("handlerId required");

    var response = await server.Http.PostAsync($"/api/integrations/handlers/{handlerId}/test",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to test integration: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"Integration Connection Test Result:");
    sb.AppendLine($"  Success: {(result["success"]?.Value<bool>() == true ? "✓ Yes" : "✗ No")}");
    sb.AppendLine($"  Message: {result["message"] ?? "No message"}");
    if (result["latencyMs"] != null)
    {
        sb.AppendLine($"  Latency: {result["latencyMs"]}ms");
    }
    return sb.ToString();
});

server.RegisterTool("GetIntegrationUsage", async (args) =>
{
    var handlerId = args["handlerId"]?.Value<int>();
    var startDate = args["startDate"]?.Value<DateTime?>() ?? DateTime.UtcNow.AddDays(-30);
    var endDate = args["endDate"]?.Value<DateTime?>() ?? DateTime.UtcNow;

    var url = handlerId.HasValue
        ? $"/api/integrations/handlers/{handlerId}/usage"
        : "/api/integrations/usage";
    url += $"?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}";

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get usage: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"Integration Usage ({startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd}):");
    sb.AppendLine();

    // Overall stats
    if (result["summary"] != null)
    {
        var summary = result["summary"];
        sb.AppendLine($"## Summary");
        sb.AppendLine($"  Total Calls: {summary["totalCalls"] ?? 0}");
        sb.AppendLine($"  Successful: {summary["successfulCalls"] ?? 0}");
        sb.AppendLine($"  Failed: {summary["failedCalls"] ?? 0}");
        sb.AppendLine($"  Avg Latency: {summary["avgLatencyMs"] ?? 0}ms");
        sb.AppendLine($"  Estimated Cost: ${summary["estimatedCost"] ?? 0:F4}");
        sb.AppendLine();
    }

    // Per-integration breakdown
    var byIntegration = result["byIntegration"] as JArray ?? new JArray();
    if (byIntegration.Count > 0)
    {
        sb.AppendLine($"## By Integration");
        foreach (var item in byIntegration)
        {
            sb.AppendLine($"  {item["integrationName"]}:");
            sb.AppendLine($"    Calls: {item["calls"]}, Success Rate: {item["successRate"] ?? 0:P1}");
            sb.AppendLine($"    Cost: ${item["estimatedCost"] ?? 0:F4}");
        }
        sb.AppendLine();
    }

    // Daily breakdown
    var daily = result["daily"] as JArray ?? new JArray();
    if (daily.Count > 0)
    {
        sb.AppendLine($"## Daily ({daily.Count} days)");
        foreach (var day in daily.Take(7))
        {
            sb.AppendLine($"  {day["date"]}: {day["calls"]} calls, ${day["cost"] ?? 0:F4}");
        }
        if (daily.Count > 7) sb.AppendLine($"  ... and {daily.Count - 7} more days");
    }

    return sb.ToString();
});

server.RegisterTool("GetIntegrationMetadataSchema", async (args) =>
{
    var integrationId = args["integrationId"]?.Value<string>() ?? throw new ArgumentException("integrationId required");

    var response = await server.Http.GetAsync($"/api/integrations/{integrationId}/schema");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get schema: {response.StatusCode}");

    var schema = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"@integrationSchema[{integrationId}] {{");

    var sections = schema["sections"] as JArray ?? new JArray();
    var fields = schema["fields"] as JArray ?? new JArray();

    foreach (var section in sections)
    {
        sb.AppendLine($"  ## {section["title"]} (id: {section["id"]})");
        var sectionFields = fields.Where(f => f["section"]?.ToString() == section["id"]?.ToString());
        foreach (var field in sectionFields)
        {
            var required = field["required"]?.Value<bool>() == true ? "*" : "";
            sb.AppendLine($"    {field["name"]}{required} ({field["fieldType"]}): \"{field["displayName"]}\"");
            if (field["description"] != null)
                sb.AppendLine($"      // {field["description"]}");
            if (field["options"] is JArray options && options.Count > 0)
            {
                sb.AppendLine($"      Options: [{string.Join(", ", options.Select(o => o["value"]?.ToString()))}]");
            }
        }
        sb.AppendLine();
    }

    sb.AppendLine($"}}");
    return sb.ToString();
});

server.RegisterTool("ActivateIntegration", async (args) =>
{
    var integrationId = args["integrationId"]?.Value<string>() ?? throw new ArgumentException("integrationId required");
    var settings = args["settings"] as JObject ?? new JObject();

    var request = new JObject
    {
        ["integrationId"] = integrationId,
        ["settings"] = settings
    };

    var response = await server.Http.PostAsync("/api/integrations/activate",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to activate integration: {response.StatusCode} - {error}");
    }

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"Integration Activation Result:");
    sb.AppendLine($"  Success: {(result["success"]?.Value<bool>() == true ? "✓ Yes" : "✗ No")}");
    sb.AppendLine($"  Handler ID: {result["handlerId"]}");
    if (result["message"] != null)
        sb.AppendLine($"  Message: {result["message"]}");
    if (result["authorizationUrl"] != null)
        sb.AppendLine($"  OAuth URL: {result["authorizationUrl"]}");
    return sb.ToString();
});

server.RegisterTool("DeactivateIntegration", async (args) =>
{
    var handlerId = args["handlerId"]?.Value<int>() ?? throw new ArgumentException("handlerId required");

    var response = await server.Http.PostAsync($"/api/integrations/handlers/{handlerId}/deactivate",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to deactivate integration: {response.StatusCode}");

    return $"Integration handler {handlerId} deactivated successfully.";
});

server.RegisterTool("UpdateIntegrationSettings", async (args) =>
{
    var handlerId = args["handlerId"]?.Value<int>() ?? throw new ArgumentException("handlerId required");
    var settings = args["settings"] as JObject ?? throw new ArgumentException("settings required");

    var response = await server.Http.PutAsync($"/api/integrations/handlers/{handlerId}/settings",
        new StringContent(settings.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to update settings: {response.StatusCode}");

    return $"Integration handler {handlerId} settings updated successfully.";
});

// AI Integration specific tools
server.RegisterTool("GetAIModels", async (args) =>
{
    var handlerId = args["handlerId"]?.Value<int>() ?? throw new ArgumentException("handlerId required");

    var response = await server.Http.GetAsync($"/api/integrations/ai/{handlerId}/models");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get AI models: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var models = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"AI Models ({models.Count}):");
    sb.AppendLine();

    foreach (var model in models)
    {
        sb.AppendLine($"@aiModel[{model["id"]}] {{");
        sb.AppendLine($"  name: \"{model["name"]}\"");
        if (model["description"] != null)
            sb.AppendLine($"  description: \"{model["description"]}\"");
        if (model["contextWindow"] != null)
            sb.AppendLine($"  contextWindow: {model["contextWindow"]}");
        if (model["maxTokens"] != null)
            sb.AppendLine($"  maxTokens: {model["maxTokens"]}");
        if (model["inputPricePerMillion"] != null)
            sb.AppendLine($"  inputPrice: ${model["inputPricePerMillion"]}/M tokens");
        if (model["outputPricePerMillion"] != null)
            sb.AppendLine($"  outputPrice: ${model["outputPricePerMillion"]}/M tokens");
        sb.AppendLine($"  supportsVision: {model["supportsVision"] ?? false}");
        sb.AppendLine($"  supportsTools: {model["supportsTools"] ?? false}");
        sb.AppendLine($"  supportsStreaming: {model["supportsStreaming"] ?? false}");
        sb.AppendLine($"}}");
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetAIUsageQuota", async (args) =>
{
    var handlerId = args["handlerId"]?.Value<int>() ?? throw new ArgumentException("handlerId required");

    var response = await server.Http.GetAsync($"/api/integrations/ai/{handlerId}/usage");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get AI usage: {response.StatusCode}");

    var usage = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"@aiUsage[{handlerId}] {{");
    sb.AppendLine($"  provider: \"{usage["provider"]}\"");
    if (usage["currentBalance"] != null)
        sb.AppendLine($"  currentBalance: ${usage["currentBalance"]:F2}");
    if (usage["spentThisMonth"] != null)
        sb.AppendLine($"  spentThisMonth: ${usage["spentThisMonth"]:F2}");
    if (usage["tokensUsedThisMonth"] != null)
        sb.AppendLine($"  tokensUsedThisMonth: {usage["tokensUsedThisMonth"]:N0}");
    if (usage["rateLimitRequestsPerMinute"] != null)
        sb.AppendLine($"  rateLimitRPM: {usage["rateLimitRequestsPerMinute"]}");
    if (usage["rateLimitTokensPerMinute"] != null)
        sb.AppendLine($"  rateLimitTPM: {usage["rateLimitTokensPerMinute"]}");
    if (usage["billingCycleStart"] != null)
        sb.AppendLine($"  billingPeriod: {usage["billingCycleStart"]} - {usage["billingCycleEnd"]}");
    sb.AppendLine($"}}");
    return sb.ToString();
});

// ----------------------------------------------------------------------
// Infrastructure Management Tools (Proxmox, Kubernetes, Docker, etc.)
// ----------------------------------------------------------------------

server.RegisterTool("GetInfrastructureConnections", async (args) =>
{
    var integrationType = args["integrationType"]?.Value<string>();
    var onlyActive = args["onlyActive"]?.Value<bool>() ?? true;

    var url = "/api/infrastructure/connections";
    var queryParams = new List<string>();
    if (!string.IsNullOrEmpty(integrationType)) queryParams.Add($"integrationType={Uri.EscapeDataString(integrationType)}");
    if (onlyActive) queryParams.Add("onlyActive=true");
    if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get infrastructure connections: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var connections = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"Infrastructure Connections ({connections.Count}):");
    sb.AppendLine();

    var grouped = connections.GroupBy(c => c["integrationType"]?.ToString() ?? "unknown");
    foreach (var group in grouped)
    {
        sb.AppendLine($"## {group.Key.ToUpper()}");
        foreach (var conn in group)
        {
            var statusIcon = conn["isConnected"]?.Value<bool>() == true ? "✓" : "✗";
            sb.AppendLine($"  @infraConnection[{conn["id"]}] {{");
            sb.AppendLine($"    name: \"{conn["name"]}\"");
            sb.AppendLine($"    slug: \"{conn["slug"]}\"");
            sb.AppendLine($"    endpoint: \"{conn["endpoint"]}:{conn["port"]}\"");
            sb.AppendLine($"    status: {statusIcon} {(conn["isConnected"]?.Value<bool>() == true ? "Connected" : "Disconnected")}");
            sb.AppendLine($"    environment: \"{conn["environment"] ?? ""}\"");
            if (conn["lastConnectedAt"] != null)
                sb.AppendLine($"    lastConnected: \"{conn["lastConnectedAt"]}\"");
            if (conn["lastError"] != null)
                sb.AppendLine($"    lastError: \"{conn["lastError"]}\"");
            sb.AppendLine($"  }}");
        }
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetInfrastructureConnectionDetails", async (args) =>
{
    var connectionId = args["connectionId"]?.Value<int>() ?? throw new ArgumentException("connectionId required");

    var response = await server.Http.GetAsync($"/api/infrastructure/connections/{connectionId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get connection details: {response.StatusCode}");

    var conn = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    var statusIcon = conn["isConnected"]?.Value<bool>() == true ? "✓" : "✗";
    sb.AppendLine($"@infraConnection[{conn["id"]}] {{");
    sb.AppendLine($"  name: \"{conn["name"]}\"");
    sb.AppendLine($"  slug: \"{conn["slug"]}\"");
    sb.AppendLine($"  integrationType: \"{conn["integrationType"]}\"");
    sb.AppendLine($"  description: \"{conn["description"] ?? ""}\"");
    sb.AppendLine($"  environment: \"{conn["environment"] ?? ""}\"");
    sb.AppendLine();
    sb.AppendLine($"  ## Connection Settings");
    sb.AppendLine($"  endpoint: \"{conn["endpoint"]}\"");
    sb.AppendLine($"  port: {conn["port"] ?? "default"}");
    sb.AppendLine($"  useSsl: {conn["useSsl"] ?? true}");
    sb.AppendLine($"  verifySsl: {conn["verifySsl"] ?? true}");
    sb.AppendLine($"  authType: \"{conn["authType"] ?? ""}\"");
    sb.AppendLine();
    sb.AppendLine($"  ## Status");
    sb.AppendLine($"  status: {statusIcon} {conn["status"] ?? "unknown"}");
    sb.AppendLine($"  isActive: {conn["isActive"] ?? false}");
    sb.AppendLine($"  isConnected: {conn["isConnected"] ?? false}");
    sb.AppendLine($"  lastConnectedAt: \"{conn["lastConnectedAt"] ?? "never"}\"");
    sb.AppendLine($"  lastSyncAt: \"{conn["lastSyncAt"] ?? "never"}\"");
    if (conn["lastError"] != null)
    {
        sb.AppendLine($"  lastError: \"{conn["lastError"]}\"");
        sb.AppendLine($"  lastErrorAt: \"{conn["lastErrorAt"]}\"");
    }
    sb.AppendLine();
    sb.AppendLine($"  ## Sync Settings");
    sb.AppendLine($"  syncIntervalSeconds: {conn["syncIntervalSeconds"] ?? 60}");
    sb.AppendLine($"  enableRealtime: {conn["enableRealtime"] ?? false}");
    sb.AppendLine($"  systemVersion: \"{conn["systemVersion"] ?? ""}\"");
    sb.AppendLine();

    // Configuration and metadata
    if (conn["enabledFeatures"] != null)
    {
        var features = conn["enabledFeatures"] as JArray ?? new JArray();
        sb.AppendLine($"  enabledFeatures: [{string.Join(", ", features.Select(f => f.ToString()))}]");
    }
    if (conn["capabilities"] != null)
    {
        var caps = conn["capabilities"] as JArray ?? new JArray();
        sb.AppendLine($"  capabilities: [{string.Join(", ", caps.Select(c => c.ToString()))}]");
    }
    if (conn["tags"] != null)
    {
        var tags = conn["tags"] as JArray ?? new JArray();
        sb.AppendLine($"  tags: [{string.Join(", ", tags.Select(t => t.ToString()))}]");
    }
    sb.AppendLine($"}}");
    return sb.ToString();
});

server.RegisterTool("TestInfrastructureConnection", async (args) =>
{
    var connectionId = args["connectionId"]?.Value<int>() ?? throw new ArgumentException("connectionId required");

    var response = await server.Http.PostAsync($"/api/infrastructure/connections/{connectionId}/test",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to test connection: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"Infrastructure Connection Test Result:");
    sb.AppendLine($"  Success: {(result["success"]?.Value<bool>() == true ? "✓ Yes" : "✗ No")}");
    sb.AppendLine($"  Message: {result["message"] ?? "No message"}");
    if (result["latencyMs"] != null)
        sb.AppendLine($"  Latency: {result["latencyMs"]}ms");
    if (result["systemVersion"] != null)
        sb.AppendLine($"  System Version: {result["systemVersion"]}");
    if (result["nodeCount"] != null)
        sb.AppendLine($"  Nodes: {result["nodeCount"]}");
    if (result["vmCount"] != null)
        sb.AppendLine($"  VMs: {result["vmCount"]}");
    if (result["containerCount"] != null)
        sb.AppendLine($"  Containers: {result["containerCount"]}");
    return sb.ToString();
});

server.RegisterTool("SyncInfrastructureResources", async (args) =>
{
    var connectionId = args["connectionId"]?.Value<int>() ?? throw new ArgumentException("connectionId required");
    var fullSync = args["fullSync"]?.Value<bool>() ?? false;

    var request = new JObject { ["fullSync"] = fullSync };
    var response = await server.Http.PostAsync($"/api/infrastructure/connections/{connectionId}/sync",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to sync resources: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"Infrastructure Sync Result:");
    sb.AppendLine($"  Success: {(result["success"]?.Value<bool>() == true ? "✓ Yes" : "✗ No")}");
    sb.AppendLine($"  Total Resources: {result["totalResources"] ?? 0}");
    sb.AppendLine($"  Added: {result["added"] ?? 0}");
    sb.AppendLine($"  Updated: {result["updated"] ?? 0}");
    sb.AppendLine($"  Removed: {result["removed"] ?? 0}");
    sb.AppendLine($"  Duration: {result["durationMs"] ?? 0}ms");
    return sb.ToString();
});

server.RegisterTool("GetInfrastructureResources", async (args) =>
{
    var connectionId = args["connectionId"]?.Value<int>() ?? throw new ArgumentException("connectionId required");
    var resourceType = args["resourceType"]?.Value<string>();
    var status = args["status"]?.Value<string>();
    var limit = args["limit"]?.Value<int>() ?? 50;

    var url = $"/api/infrastructure/connections/{connectionId}/resources";
    var queryParams = new List<string> { $"limit={limit}" };
    if (!string.IsNullOrEmpty(resourceType)) queryParams.Add($"resourceType={Uri.EscapeDataString(resourceType)}");
    if (!string.IsNullOrEmpty(status)) queryParams.Add($"status={Uri.EscapeDataString(status)}");
    url += "?" + string.Join("&", queryParams);

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get resources: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var resources = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"Infrastructure Resources ({resources.Count}):");
    sb.AppendLine();

    var grouped = resources.GroupBy(r => r["resourceType"]?.ToString() ?? "unknown");
    foreach (var group in grouped)
    {
        sb.AppendLine($"## {group.Key}s");
        foreach (var res in group)
        {
            var statusIcon = res["status"]?.ToString() == "running" ? "▶" :
                             res["status"]?.ToString() == "stopped" ? "⏹" :
                             res["status"]?.ToString() == "paused" ? "⏸" : "?";

            sb.AppendLine($"  @infraResource[{res["id"]}] {{");
            sb.AppendLine($"    externalId: \"{res["externalId"]}\"");
            sb.AppendLine($"    name: \"{res["name"]}\"");
            sb.AppendLine($"    status: {statusIcon} {res["status"]}");

            // Resource specs
            if (res["cpuCores"] != null)
                sb.AppendLine($"    cpu: {res["cpuCores"]} cores");
            if (res["memoryBytes"] != null)
            {
                var memGb = (res["memoryBytes"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0;
                sb.AppendLine($"    memory: {memGb:F1} GB");
            }
            if (res["storageBytes"] != null)
            {
                var storGb = (res["storageBytes"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0;
                sb.AppendLine($"    storage: {storGb:F1} GB");
            }

            // Resource usage
            if (res["cpuUsage"] != null)
                sb.AppendLine($"    cpuUsage: {res["cpuUsage"]:F1}%");
            if (res["memoryUsed"] != null && res["memoryBytes"] != null)
            {
                var memUsedGb = (res["memoryUsed"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0;
                var memTotalGb = (res["memoryBytes"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0;
                sb.AppendLine($"    memoryUsed: {memUsedGb:F1}/{memTotalGb:F1} GB");
            }

            if (res["hostname"] != null)
                sb.AppendLine($"    hostname: \"{res["hostname"]}\"");
            if (res["uptime"] != null)
            {
                var uptime = TimeSpan.FromSeconds(res["uptime"]?.Value<long>() ?? 0);
                sb.AppendLine($"    uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m");
            }
            if (res["projectId"] != null)
                sb.AppendLine($"    projectId: {res["projectId"]}");
            if (res["customerId"] != null)
                sb.AppendLine($"    customerId: {res["customerId"]}");

            sb.AppendLine($"  }}");
        }
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("GetInfrastructureResourceDetails", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");

    var response = await server.Http.GetAsync($"/api/infrastructure/resources/{resourceId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get resource details: {response.StatusCode}");

    var res = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    var statusIcon = res["status"]?.ToString() == "running" ? "▶" :
                     res["status"]?.ToString() == "stopped" ? "⏹" :
                     res["status"]?.ToString() == "paused" ? "⏸" : "?";

    sb.AppendLine($"@infraResource[{res["id"]}] {{");
    sb.AppendLine($"  externalId: \"{res["externalId"]}\"");
    sb.AppendLine($"  name: \"{res["name"]}\"");
    sb.AppendLine($"  resourceType: \"{res["resourceType"]}\"");
    sb.AppendLine($"  status: {statusIcon} {res["status"]}");
    sb.AppendLine($"  isTemplate: {res["isTemplate"] ?? false}");
    sb.AppendLine();

    sb.AppendLine($"  ## Specifications");
    if (res["cpuCores"] != null)
        sb.AppendLine($"  cpuCores: {res["cpuCores"]}");
    if (res["memoryBytes"] != null)
    {
        var memGb = (res["memoryBytes"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0;
        sb.AppendLine($"  memory: {memGb:F2} GB");
    }
    if (res["storageBytes"] != null)
    {
        var storGb = (res["storageBytes"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0;
        sb.AppendLine($"  storage: {storGb:F2} GB");
    }
    sb.AppendLine();

    sb.AppendLine($"  ## Current Usage");
    if (res["cpuUsage"] != null)
        sb.AppendLine($"  cpuUsage: {res["cpuUsage"]:F1}%");
    if (res["memoryUsed"] != null)
    {
        var memUsedGb = (res["memoryUsed"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0;
        sb.AppendLine($"  memoryUsed: {memUsedGb:F2} GB");
    }
    if (res["storageUsed"] != null)
    {
        var storUsedGb = (res["storageUsed"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0;
        sb.AppendLine($"  storageUsed: {storUsedGb:F2} GB");
    }
    if (res["networkIn"] != null)
    {
        var netInMb = (res["networkIn"]?.Value<long>() ?? 0) / 1024.0 / 1024.0;
        sb.AppendLine($"  networkIn: {netInMb:F2} MB");
    }
    if (res["networkOut"] != null)
    {
        var netOutMb = (res["networkOut"]?.Value<long>() ?? 0) / 1024.0 / 1024.0;
        sb.AppendLine($"  networkOut: {netOutMb:F2} MB");
    }
    if (res["uptime"] != null)
    {
        var uptime = TimeSpan.FromSeconds(res["uptime"]?.Value<long>() ?? 0);
        sb.AppendLine($"  uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s");
    }
    sb.AppendLine();

    sb.AppendLine($"  ## Network");
    if (res["hostname"] != null)
        sb.AppendLine($"  hostname: \"{res["hostname"]}\"");
    if (res["ipAddresses"] != null)
    {
        var ips = res["ipAddresses"] as JArray ?? new JArray();
        sb.AppendLine($"  ipAddresses: [{string.Join(", ", ips.Select(ip => ip.ToString()))}]");
    }
    sb.AppendLine();

    sb.AppendLine($"  ## Associations");
    if (res["connectionId"] != null)
        sb.AppendLine($"  connectionId: {res["connectionId"]}");
    if (res["parentId"] != null)
        sb.AppendLine($"  parentId: {res["parentId"]}");
    if (res["projectId"] != null)
        sb.AppendLine($"  projectId: {res["projectId"]}");
    if (res["customerId"] != null)
        sb.AppendLine($"  customerId: {res["customerId"]}");
    if (res["costCenter"] != null)
        sb.AppendLine($"  costCenter: \"{res["costCenter"]}\"");
    if (res["monthlyCost"] != null)
        sb.AppendLine($"  monthlyCost: ${res["monthlyCost"]:F2}");
    sb.AppendLine();

    if (res["tags"] != null || res["labels"] != null)
    {
        sb.AppendLine($"  ## Tags & Labels");
        if (res["tags"] != null)
        {
            var tags = res["tags"] as JArray ?? new JArray();
            sb.AppendLine($"  tags: [{string.Join(", ", tags.Select(t => t.ToString()))}]");
        }
        if (res["labels"] != null)
        {
            var labels = res["labels"] as JObject ?? new JObject();
            foreach (var prop in labels.Properties())
            {
                sb.AppendLine($"  {prop.Name}: \"{prop.Value}\"");
            }
        }
    }

    sb.AppendLine($"  lastSyncAt: \"{res["lastSyncAt"] ?? "never"}\"");
    sb.AppendLine($"}}");
    return sb.ToString();
});

server.RegisterTool("StartInfrastructureResource", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");

    var response = await server.Http.PostAsync($"/api/infrastructure/resources/{resourceId}/start",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to start resource: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    return $"▶ Resource {resourceId} start initiated. Task ID: {result["taskId"] ?? "N/A"}";
});

server.RegisterTool("StopInfrastructureResource", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");
    var force = args["force"]?.Value<bool>() ?? false;

    var request = new JObject { ["force"] = force };
    var response = await server.Http.PostAsync($"/api/infrastructure/resources/{resourceId}/stop",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to stop resource: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    return $"⏹ Resource {resourceId} stop initiated{(force ? " (forced)" : "")}. Task ID: {result["taskId"] ?? "N/A"}";
});

server.RegisterTool("RebootInfrastructureResource", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");

    var response = await server.Http.PostAsync($"/api/infrastructure/resources/{resourceId}/reboot",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to reboot resource: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    return $"🔄 Resource {resourceId} reboot initiated. Task ID: {result["taskId"] ?? "N/A"}";
});

server.RegisterTool("GetInfrastructureResourceMetrics", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");
    var startDate = args["startDate"]?.Value<DateTime?>() ?? DateTime.UtcNow.AddHours(-24);
    var endDate = args["endDate"]?.Value<DateTime?>() ?? DateTime.UtcNow;
    var resolution = args["resolution"]?.Value<string>() ?? "5m";

    var url = $"/api/infrastructure/resources/{resourceId}/metrics";
    url += $"?startDate={startDate:yyyy-MM-ddTHH:mm:ss}Z&endDate={endDate:yyyy-MM-ddTHH:mm:ss}Z&resolution={resolution}";

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get metrics: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var metrics = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"Resource Metrics ({startDate:HH:mm} - {endDate:HH:mm}, {resolution} intervals):");
    sb.AppendLine();

    // Summary stats
    if (metrics.Count > 0)
    {
        var avgCpu = metrics.Average(m => m["cpuUsage"]?.Value<decimal?>() ?? 0);
        var maxCpu = metrics.Max(m => m["cpuUsage"]?.Value<decimal?>() ?? 0);
        var avgMem = metrics.Average(m => m["memoryUsed"]?.Value<long?>() ?? 0);
        var maxMem = metrics.Max(m => m["memoryUsed"]?.Value<long?>() ?? 0);

        sb.AppendLine($"## Summary ({metrics.Count} data points)");
        sb.AppendLine($"  CPU: avg {avgCpu:F1}%, max {maxCpu:F1}%");
        sb.AppendLine($"  Memory: avg {avgMem / 1024.0 / 1024.0 / 1024.0:F2} GB, max {maxMem / 1024.0 / 1024.0 / 1024.0:F2} GB");
        sb.AppendLine();

        sb.AppendLine($"## Recent Data Points (last 10)");
        foreach (var m in metrics.TakeLast(10))
        {
            var ts = DateTime.Parse(m["timestamp"]?.ToString() ?? DateTime.UtcNow.ToString());
            sb.AppendLine($"  [{ts:HH:mm}] CPU: {m["cpuUsage"] ?? 0:F1}% | " +
                         $"Mem: {(m["memoryUsed"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0:F2} GB | " +
                         $"Net In/Out: {(m["networkIn"]?.Value<long>() ?? 0) / 1024.0:F0}/{(m["networkOut"]?.Value<long>() ?? 0) / 1024.0:F0} KB");
        }
    }
    else
    {
        sb.AppendLine("No metrics data available for the specified period.");
    }
    return sb.ToString();
});

server.RegisterTool("CreateInfrastructureSnapshot", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");
    var name = args["name"]?.Value<string>() ?? $"snapshot-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
    var description = args["description"]?.Value<string>();
    var includeMemory = args["includeMemory"]?.Value<bool>() ?? false;

    var request = new JObject
    {
        ["name"] = name,
        ["description"] = description,
        ["includeMemory"] = includeMemory
    };

    var response = await server.Http.PostAsync($"/api/infrastructure/resources/{resourceId}/snapshots",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to create snapshot: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    return $"📸 Snapshot created: {name}. Task ID: {result["taskId"] ?? "N/A"}";
});

server.RegisterTool("GetInfrastructureSnapshots", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");

    var response = await server.Http.GetAsync($"/api/infrastructure/resources/{resourceId}/snapshots");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get snapshots: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var snapshots = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"Snapshots for Resource {resourceId} ({snapshots.Count}):");
    sb.AppendLine();

    foreach (var snap in snapshots)
    {
        sb.AppendLine($"@snapshot[{snap["name"]}] {{");
        sb.AppendLine($"  description: \"{snap["description"] ?? ""}\"");
        sb.AppendLine($"  createdAt: \"{snap["createdAt"]}\"");
        if (snap["sizeBytes"] != null)
        {
            var sizeMb = (snap["sizeBytes"]?.Value<long>() ?? 0) / 1024.0 / 1024.0;
            sb.AppendLine($"  size: {sizeMb:F2} MB");
        }
        sb.AppendLine($"  includesMemory: {snap["includesMemory"] ?? false}");
        sb.AppendLine($"}}");
        sb.AppendLine();
    }
    return sb.ToString();
});

server.RegisterTool("RestoreInfrastructureSnapshot", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");
    var snapshotName = args["snapshotName"]?.Value<string>() ?? throw new ArgumentException("snapshotName required");
    var startAfterRestore = args["startAfterRestore"]?.Value<bool>() ?? true;

    var request = new JObject
    {
        ["snapshotName"] = snapshotName,
        ["startAfterRestore"] = startAfterRestore
    };

    var response = await server.Http.PostAsync($"/api/infrastructure/resources/{resourceId}/snapshots/restore",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to restore snapshot: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    return $"⏪ Restoring snapshot '{snapshotName}' for resource {resourceId}. Task ID: {result["taskId"] ?? "N/A"}";
});

server.RegisterTool("LinkResourceToProject", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");
    var projectId = args["projectId"]?.Value<int>() ?? throw new ArgumentException("projectId required");

    var request = new JObject { ["projectId"] = projectId };
    var response = await server.Http.PostAsync($"/api/infrastructure/resources/{resourceId}/link",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to link resource: {response.StatusCode}");

    return $"Resource {resourceId} linked to project {projectId}.";
});

server.RegisterTool("LinkResourceToCustomer", async (args) =>
{
    var resourceId = args["resourceId"]?.Value<int>() ?? throw new ArgumentException("resourceId required");
    var customerId = args["customerId"]?.Value<int>() ?? throw new ArgumentException("customerId required");
    var costCenter = args["costCenter"]?.Value<string>();
    var monthlyCost = args["monthlyCost"]?.Value<decimal?>();

    var request = new JObject
    {
        ["customerId"] = customerId,
        ["costCenter"] = costCenter,
        ["monthlyCost"] = monthlyCost
    };

    var response = await server.Http.PostAsync($"/api/infrastructure/resources/{resourceId}/link",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to link resource: {response.StatusCode}");

    return $"Resource {resourceId} linked to customer {customerId}" +
           (costCenter != null ? $" (cost center: {costCenter})" : "") +
           (monthlyCost.HasValue ? $" (monthly cost: ${monthlyCost:F2})" : "") + ".";
});

server.RegisterTool("GetInfrastructureSummary", async (args) =>
{
    var connectionId = args["connectionId"]?.Value<int?>();

    var url = connectionId.HasValue
        ? $"/api/infrastructure/connections/{connectionId}/summary"
        : "/api/infrastructure/summary";

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get infrastructure summary: {response.StatusCode}");

    var summary = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine("=== Infrastructure Summary ===");
    sb.AppendLine();

    // Overall stats
    sb.AppendLine($"## Overview");
    sb.AppendLine($"  Connections: {summary["totalConnections"] ?? 0} ({summary["connectedCount"] ?? 0} connected)");
    sb.AppendLine($"  Total Resources: {summary["totalResources"] ?? 0}");
    sb.AppendLine();

    // Resource breakdown by type
    var byType = summary["resourcesByType"] as JObject ?? new JObject();
    if (byType.Count > 0)
    {
        sb.AppendLine($"## Resources by Type");
        foreach (var prop in byType.Properties())
        {
            sb.AppendLine($"  {prop.Name}: {prop.Value}");
        }
        sb.AppendLine();
    }

    // Status breakdown
    var byStatus = summary["resourcesByStatus"] as JObject ?? new JObject();
    if (byStatus.Count > 0)
    {
        sb.AppendLine($"## Resources by Status");
        foreach (var prop in byStatus.Properties())
        {
            var icon = prop.Name == "running" ? "▶" :
                       prop.Name == "stopped" ? "⏹" :
                       prop.Name == "paused" ? "⏸" : "?";
            sb.AppendLine($"  {icon} {prop.Name}: {prop.Value}");
        }
        sb.AppendLine();
    }

    // Total resource usage
    if (summary["totalCpuCores"] != null || summary["totalMemoryBytes"] != null)
    {
        sb.AppendLine($"## Total Allocated Resources");
        if (summary["totalCpuCores"] != null)
            sb.AppendLine($"  CPU: {summary["totalCpuCores"]} cores");
        if (summary["totalMemoryBytes"] != null)
        {
            var memGb = (summary["totalMemoryBytes"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0;
            sb.AppendLine($"  Memory: {memGb:F1} GB");
        }
        if (summary["totalStorageBytes"] != null)
        {
            var storTb = (summary["totalStorageBytes"]?.Value<long>() ?? 0) / 1024.0 / 1024.0 / 1024.0 / 1024.0;
            sb.AppendLine($"  Storage: {storTb:F2} TB");
        }
        sb.AppendLine();
    }

    // Cost summary
    if (summary["totalMonthlyCost"] != null)
    {
        sb.AppendLine($"## Cost Summary");
        sb.AppendLine($"  Total Monthly Cost: ${summary["totalMonthlyCost"]:F2}");
        if (summary["linkedToProjects"] != null)
            sb.AppendLine($"  Linked to Projects: {summary["linkedToProjects"]}");
        if (summary["linkedToCustomers"] != null)
            sb.AppendLine($"  Linked to Customers: {summary["linkedToCustomers"]}");
    }

    return sb.ToString();
});

// ----------------------------------------------------------------------
// Cluster Health Monitoring Tools (Forge Lite)
// ----------------------------------------------------------------------

server.RegisterTool("GetClusterHealth", async (args) =>
{
    var includeDetails = args["includeDetails"]?.Value<bool>() ?? false;

    // Get all infrastructure connections and check their status
    var connectionsResponse = await server.Http.GetAsync("/api/infrastructure/connections?onlyActive=true");
    if (!connectionsResponse.IsSuccessStatusCode)
        throw new Exception($"Failed to get infrastructure status: {connectionsResponse.StatusCode}");

    var connectionsResult = JObject.Parse(await connectionsResponse.Content.ReadAsStringAsync());
    var connections = connectionsResult["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine("# Cluster Health Dashboard");
    sb.AppendLine($"Last checked: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
    sb.AppendLine();

    // Summary counts
    var totalConnections = connections.Count;
    var healthyConnections = connections.Count(c => c["isConnected"]?.Value<bool>() == true);
    var unhealthyConnections = totalConnections - healthyConnections;

    var overallStatus = unhealthyConnections == 0 ? "✅ HEALTHY" :
                        unhealthyConnections <= 2 ? "⚠️ DEGRADED" : "❌ CRITICAL";

    sb.AppendLine($"## Overall Status: {overallStatus}");
    sb.AppendLine($"- Healthy: {healthyConnections}/{totalConnections}");
    sb.AppendLine($"- Unhealthy: {unhealthyConnections}");
    sb.AppendLine();

    // Group by type
    var grouped = connections.GroupBy(c => c["integrationType"]?.ToString() ?? "unknown");

    foreach (var group in grouped.OrderBy(g => g.Key))
    {
        var groupHealthy = group.Count(c => c["isConnected"]?.Value<bool>() == true);
        var groupTotal = group.Count();
        var groupIcon = groupHealthy == groupTotal ? "✅" : groupHealthy == 0 ? "❌" : "⚠️";

        sb.AppendLine($"### {groupIcon} {group.Key.ToUpper()} ({groupHealthy}/{groupTotal})");

        if (includeDetails)
        {
            foreach (var conn in group)
            {
                var statusIcon = conn["isConnected"]?.Value<bool>() == true ? "✓" : "✗";
                sb.AppendLine($"  - {statusIcon} {conn["name"]} ({conn["endpoint"]})");
                if (conn["lastError"] != null && conn["isConnected"]?.Value<bool>() != true)
                    sb.AppendLine($"    Error: {conn["lastError"]}");
            }
        }
        sb.AppendLine();
    }

    // Alerts section
    var unhealthy = connections.Where(c => c["isConnected"]?.Value<bool>() != true).ToList();
    if (unhealthy.Count > 0)
    {
        sb.AppendLine("## ⚠️ Active Alerts");
        foreach (var conn in unhealthy)
        {
            sb.AppendLine($"- [{conn["integrationType"]}] {conn["name"]}: {conn["lastError"] ?? "Disconnected"}");
        }
    }

    return sb.ToString();
});

server.RegisterTool("GetKubernetesHealth", async (args) =>
{
    var connectionSlug = args["connectionSlug"]?.Value<string>() ?? "kubernetes-eden";
    var namespaces = args["namespaces"]?.ToObject<List<string>>() ?? new List<string> { "served", "served-redis", "served-kafka" };

    // Get connection by slug
    var connResponse = await server.Http.GetAsync($"/api/infrastructure/connections/slug/{connectionSlug}");
    if (!connResponse.IsSuccessStatusCode)
        throw new Exception($"Kubernetes connection '{connectionSlug}' not found");

    var conn = JObject.Parse(await connResponse.Content.ReadAsStringAsync());
    var connectionId = conn["id"]?.Value<int>() ?? throw new Exception("Invalid connection");

    var sb = new StringBuilder();
    sb.AppendLine($"# Kubernetes Health: {connectionSlug}");
    sb.AppendLine($"Endpoint: {conn["endpoint"]}");
    sb.AppendLine($"Version: {conn["systemVersion"] ?? "Unknown"}");
    sb.AppendLine();

    // Get resources for each namespace
    foreach (var ns in namespaces)
    {
        try
        {
            var resourcesResponse = await server.Http.GetAsync(
                $"/api/infrastructure/connections/{connectionId}/kubernetes/namespaces/{ns}/pods");

            if (resourcesResponse.IsSuccessStatusCode)
            {
                var podsResult = JObject.Parse(await resourcesResponse.Content.ReadAsStringAsync());
                var pods = podsResult["data"] as JArray ?? new JArray();

                var runningPods = pods.Count(p => p["status"]?.ToString() == "Running");
                var totalPods = pods.Count;
                var nsIcon = runningPods == totalPods ? "✅" : runningPods > 0 ? "⚠️" : "❌";

                sb.AppendLine($"## {nsIcon} Namespace: {ns}");
                sb.AppendLine($"Pods: {runningPods}/{totalPods} running");
                sb.AppendLine();

                foreach (var pod in pods)
                {
                    var podStatus = pod["status"]?.ToString() ?? "Unknown";
                    var podIcon = podStatus == "Running" ? "✓" : podStatus == "Pending" ? "◐" : "✗";
                    var restarts = pod["restartCount"]?.Value<int>() ?? 0;
                    var restartWarning = restarts > 5 ? $" ⚠️ {restarts} restarts" : "";

                    sb.AppendLine($"  {podIcon} {pod["name"]} ({podStatus}){restartWarning}");

                    if (podStatus != "Running" && pod["message"] != null)
                        sb.AppendLine($"    Reason: {pod["message"]}");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine($"## ❌ Namespace: {ns}");
                sb.AppendLine("  Failed to fetch pods");
                sb.AppendLine();
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"## ❌ Namespace: {ns}");
            sb.AppendLine($"  Error: {ex.Message}");
            sb.AppendLine();
        }
    }

    return sb.ToString();
});

server.RegisterTool("GetDatabaseHealth", async (args) =>
{
    var sb = new StringBuilder();
    sb.AppendLine("# Database Health");
    sb.AppendLine($"Checked: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
    sb.AppendLine();

    // Get all database connections
    var connResponse = await server.Http.GetAsync("/api/infrastructure/connections?onlyActive=true");
    if (!connResponse.IsSuccessStatusCode)
        throw new Exception($"Failed to get connections: {connResponse.StatusCode}");

    var connResult = JObject.Parse(await connResponse.Content.ReadAsStringAsync());
    var connections = connResult["data"] as JArray ?? new JArray();

    var dbTypes = new[] { "mysql", "postgresql", "mssql", "redis" };
    var dbConnections = connections.Where(c => dbTypes.Contains(c["integrationType"]?.ToString()?.ToLower())).ToList();

    foreach (var db in dbConnections)
    {
        var isConnected = db["isConnected"]?.Value<bool>() == true;
        var statusIcon = isConnected ? "✅" : "❌";
        var dbType = db["integrationType"]?.ToString()?.ToUpper() ?? "UNKNOWN";

        sb.AppendLine($"## {statusIcon} {db["name"]} ({dbType})");
        sb.AppendLine($"  Endpoint: {db["endpoint"]}:{db["port"]}");
        sb.AppendLine($"  Status: {(isConnected ? "Connected" : "Disconnected")}");

        if (db["lastConnectedAt"] != null)
            sb.AppendLine($"  Last Connected: {db["lastConnectedAt"]}");

        if (!isConnected && db["lastError"] != null)
            sb.AppendLine($"  Error: {db["lastError"]}");

        // Get database-specific metrics if connected
        if (isConnected)
        {
            var dbId = db["id"]?.Value<int>();
            try
            {
                var metricsResponse = await server.Http.GetAsync($"/api/infrastructure/connections/{dbId}/status");
                if (metricsResponse.IsSuccessStatusCode)
                {
                    var metrics = JObject.Parse(await metricsResponse.Content.ReadAsStringAsync());
                    if (metrics["activeConnections"] != null)
                        sb.AppendLine($"  Active Connections: {metrics["activeConnections"]}");
                    if (metrics["uptime"] != null)
                        sb.AppendLine($"  Uptime: {metrics["uptime"]}");
                    if (metrics["version"] != null)
                        sb.AppendLine($"  Version: {metrics["version"]}");
                }
            }
            catch { /* Ignore metric fetch errors */ }
        }

        sb.AppendLine();
    }

    if (dbConnections.Count == 0)
    {
        sb.AppendLine("No database connections configured.");
    }

    return sb.ToString();
});

server.RegisterTool("GetProxmoxHealth", async (args) =>
{
    var connectionSlug = args["connectionSlug"]?.Value<string>() ?? "proxmox-eden";

    // Get connection by slug
    var connResponse = await server.Http.GetAsync($"/api/infrastructure/connections/slug/{connectionSlug}");
    if (!connResponse.IsSuccessStatusCode)
        throw new Exception($"Proxmox connection '{connectionSlug}' not found");

    var conn = JObject.Parse(await connResponse.Content.ReadAsStringAsync());
    var connectionId = conn["id"]?.Value<int>() ?? throw new Exception("Invalid connection");

    var sb = new StringBuilder();
    sb.AppendLine($"# Proxmox Health: {connectionSlug}");
    sb.AppendLine($"Endpoint: {conn["endpoint"]}");
    sb.AppendLine($"Status: {(conn["isConnected"]?.Value<bool>() == true ? "✅ Connected" : "❌ Disconnected")}");
    sb.AppendLine();

    // Get VMs
    try
    {
        var vmsResponse = await server.Http.GetAsync($"/api/infrastructure/connections/{connectionId}/proxmox/vms");
        if (vmsResponse.IsSuccessStatusCode)
        {
            var vmsResult = JObject.Parse(await vmsResponse.Content.ReadAsStringAsync());
            var vms = vmsResult["data"] as JArray ?? new JArray();

            var runningVMs = vms.Count(v => v["status"]?.ToString() == "running");
            sb.AppendLine($"## Virtual Machines ({runningVMs}/{vms.Count} running)");

            foreach (var vm in vms)
            {
                var vmStatus = vm["status"]?.ToString() ?? "unknown";
                var vmIcon = vmStatus == "running" ? "✅" : vmStatus == "stopped" ? "⭕" : "⚠️";
                var cpuUsage = vm["cpu"]?.Value<double>() * 100 ?? 0;
                var memUsage = vm["mem"]?.Value<long>() ?? 0;
                var maxMem = vm["maxmem"]?.Value<long>() ?? 1;
                var memPct = (double)memUsage / maxMem * 100;

                sb.AppendLine($"  {vmIcon} {vm["name"]} (VMID: {vm["vmid"]})");
                sb.AppendLine($"     Status: {vmStatus}");
                if (vmStatus == "running")
                {
                    sb.AppendLine($"     CPU: {cpuUsage:F1}%");
                    sb.AppendLine($"     Memory: {memPct:F1}% ({memUsage / 1024 / 1024 / 1024:F1} GB)");
                }
            }
            sb.AppendLine();
        }
    }
    catch (Exception ex)
    {
        sb.AppendLine($"## Virtual Machines");
        sb.AppendLine($"  Error fetching VMs: {ex.Message}");
        sb.AppendLine();
    }

    // Get LXC containers
    try
    {
        var lxcResponse = await server.Http.GetAsync($"/api/infrastructure/connections/{connectionId}/proxmox/lxc");
        if (lxcResponse.IsSuccessStatusCode)
        {
            var lxcResult = JObject.Parse(await lxcResponse.Content.ReadAsStringAsync());
            var containers = lxcResult["data"] as JArray ?? new JArray();

            var runningContainers = containers.Count(c => c["status"]?.ToString() == "running");
            sb.AppendLine($"## LXC Containers ({runningContainers}/{containers.Count} running)");

            foreach (var lxc in containers)
            {
                var lxcStatus = lxc["status"]?.ToString() ?? "unknown";
                var lxcIcon = lxcStatus == "running" ? "✅" : lxcStatus == "stopped" ? "⭕" : "⚠️";

                sb.AppendLine($"  {lxcIcon} {lxc["name"]} (VMID: {lxc["vmid"]})");
                sb.AppendLine($"     Status: {lxcStatus}");
            }
            sb.AppendLine();
        }
    }
    catch (Exception ex)
    {
        sb.AppendLine($"## LXC Containers");
        sb.AppendLine($"  Error fetching containers: {ex.Message}");
        sb.AppendLine();
    }

    return sb.ToString();
});

server.RegisterTool("GetServiceHealth", async (args) =>
{
    var serviceName = args["serviceName"]?.Value<string>();

    var sb = new StringBuilder();
    sb.AppendLine("# Service Health");
    sb.AppendLine();

    // Check API health endpoints
    var services = new Dictionary<string, string>
    {
        { "ServedAPI", "/healthz/ready" },
        { "ServedAPI-Live", "/healthz/live" }
    };

    if (serviceName != null && services.ContainsKey(serviceName))
    {
        // Check specific service
        var endpoint = services[serviceName];
        try
        {
            var response = await server.Http.GetAsync(endpoint);
            var isHealthy = response.IsSuccessStatusCode;
            sb.AppendLine($"{(isHealthy ? "✅" : "❌")} {serviceName}: {(isHealthy ? "Healthy" : "Unhealthy")}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"❌ {serviceName}: Error - {ex.Message}");
        }
    }
    else
    {
        // Check all services
        foreach (var service in services)
        {
            try
            {
                var response = await server.Http.GetAsync(service.Value);
                var isHealthy = response.IsSuccessStatusCode;
                sb.AppendLine($"{(isHealthy ? "✅" : "❌")} {service.Key}: {(isHealthy ? "Healthy" : "Unhealthy")}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ {service.Key}: Error - {ex.Message}");
            }
        }
    }

    return sb.ToString();
});

// ----------------------------------------------------------------------
// Agent Monitoring Tools (Forge Lite)
// ----------------------------------------------------------------------

server.RegisterTool("GetActiveAgents", async (args) =>
{
    var agentType = args["agentType"]?.Value<string>();

    // Query DevOps agents endpoint
    var url = "/api/devops/agents";
    if (!string.IsNullOrEmpty(agentType))
        url += $"?type={Uri.EscapeDataString(agentType)}";

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get agents: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var agents = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine("# Active Agents");
    sb.AppendLine($"Total: {agents.Count}");
    sb.AppendLine();

    // Group by type
    var grouped = agents.GroupBy(a => a["type"]?.ToString() ?? "unknown");
    foreach (var group in grouped)
    {
        sb.AppendLine($"## {group.Key.ToUpper()}");
        foreach (var agent in group)
        {
            var statusIcon = agent["status"]?.ToString() switch
            {
                "Active" or "Running" => "🟢",
                "Idle" => "🟡",
                "Paused" => "⏸️",
                "Error" => "🔴",
                _ => "⚪"
            };

            sb.AppendLine($"  {statusIcon} @agent[{agent["id"]}] \"{agent["name"]}\"");
            sb.AppendLine($"     Status: {agent["status"]}");

            if (agent["currentTask"] != null)
                sb.AppendLine($"     Task: {agent["currentTask"]}");

            if (agent["filesInUse"] is JArray files && files.Count > 0)
            {
                sb.AppendLine($"     Files: {files.Count} in use");
                foreach (var file in files.Take(3))
                    sb.AppendLine($"       - {file}");
                if (files.Count > 3)
                    sb.AppendLine($"       ... and {files.Count - 3} more");
            }

            if (agent["progress"] != null)
            {
                var progress = agent["progress"]?.Value<double>() ?? 0;
                sb.AppendLine($"     Progress: {progress:F0}%");
            }

            if (agent["lastActivity"] != null)
                sb.AppendLine($"     Last Activity: {agent["lastActivity"]}");
        }
        sb.AppendLine();
    }

    if (agents.Count == 0)
    {
        sb.AppendLine("No active agents found.");
    }

    return sb.ToString();
});

server.RegisterTool("GetAgentDetails", async (args) =>
{
    var agentId = args["agentId"]?.Value<string>() ?? throw new ArgumentException("agentId required");

    var response = await server.Http.GetAsync($"/api/devops/agents/{agentId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Agent not found: {response.StatusCode}");

    var agent = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"@agent[{agent["id"]}] {{");
    sb.AppendLine($"  name: \"{agent["name"]}\"");
    sb.AppendLine($"  type: \"{agent["type"]}\"");
    sb.AppendLine($"  status: \"{agent["status"]}\"");
    sb.AppendLine();

    if (agent["currentTask"] != null)
    {
        sb.AppendLine($"  ## Current Task");
        sb.AppendLine($"  task: \"{agent["currentTask"]}\"");
        if (agent["taskStartedAt"] != null)
            sb.AppendLine($"  startedAt: \"{agent["taskStartedAt"]}\"");
        if (agent["progress"] != null)
            sb.AppendLine($"  progress: {agent["progress"]}%");
    }

    if (agent["filesInUse"] is JArray files && files.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine($"  ## Files In Use ({files.Count})");
        foreach (var file in files)
            sb.AppendLine($"  - {file}");
    }

    if (agent["todoItems"] is JArray todos && todos.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine($"  ## Todo List ({todos.Count})");
        foreach (var todo in todos)
        {
            var statusIcon = todo["status"]?.ToString() switch
            {
                "completed" => "✅",
                "in_progress" => "🔄",
                _ => "⭕"
            };
            sb.AppendLine($"  {statusIcon} {todo["content"]}");
        }
    }

    if (agent["recentCommits"] is JArray commits && commits.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine($"  ## Recent Commits ({commits.Count})");
        foreach (var commit in commits.Take(5))
        {
            sb.AppendLine($"  - [{commit["hash"]?.ToString()?.Substring(0, 7)}] {commit["message"]}");
        }
    }

    sb.AppendLine("}");
    return sb.ToString();
});

server.RegisterTool("SendAgentTask", async (args) =>
{
    var agentId = args["agentId"]?.Value<string>() ?? throw new ArgumentException("agentId required");
    var taskDescription = args["task"]?.Value<string>() ?? throw new ArgumentException("task required");
    var priority = args["priority"]?.Value<string>() ?? "normal";

    var payload = new JObject
    {
        ["agentId"] = agentId,
        ["task"] = taskDescription,
        ["priority"] = priority
    };

    var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
    var response = await server.Http.PostAsync("/api/devops/agents/tasks", content);

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to send task: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    return $"✅ Task sent to agent {agentId}: \"{taskDescription}\"\nTask ID: {result["taskId"]}";
});

server.RegisterTool("PauseAgent", async (args) =>
{
    var agentId = args["agentId"]?.Value<string>() ?? throw new ArgumentException("agentId required");

    var response = await server.Http.PostAsync($"/api/devops/agents/{agentId}/pause", null);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to pause agent: {response.StatusCode}");

    return $"⏸️ Agent {agentId} paused";
});

server.RegisterTool("ResumeAgent", async (args) =>
{
    var agentId = args["agentId"]?.Value<string>() ?? throw new ArgumentException("agentId required");

    var response = await server.Http.PostAsync($"/api/devops/agents/{agentId}/resume", null);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to resume agent: {response.StatusCode}");

    return $"▶️ Agent {agentId} resumed";
});

server.RegisterTool("WakeAgent", async (args) =>
{
    var agentType = args["agentType"]?.Value<string>() ?? throw new ArgumentException("agentType required");
    var task = args["task"]?.Value<string>();
    var repository = args["repository"]?.Value<string>();

    var payload = new JObject
    {
        ["type"] = agentType
    };
    if (!string.IsNullOrEmpty(task))
        payload["initialTask"] = task;
    if (!string.IsNullOrEmpty(repository))
        payload["repository"] = repository;

    var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
    var response = await server.Http.PostAsync("/api/devops/agents/wake", content);

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to wake agent: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    return $"🚀 Agent {agentType} woken up\nAgent ID: {result["agentId"]}";
});

server.RegisterTool("CheckAgentConflicts", async (args) =>
{
    var response = await server.Http.GetAsync("/api/devops/agents/conflicts");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to check conflicts: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var conflicts = result["conflicts"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine("# Agent File Conflicts");
    sb.AppendLine();

    if (conflicts.Count == 0)
    {
        sb.AppendLine("✅ No conflicts detected");
    }
    else
    {
        sb.AppendLine($"⚠️ {conflicts.Count} conflict(s) detected:");
        sb.AppendLine();

        foreach (var conflict in conflicts)
        {
            sb.AppendLine($"## File: {conflict["file"]}");
            var agents = conflict["agents"] as JArray ?? new JArray();
            foreach (var agent in agents)
            {
                sb.AppendLine($"  - Agent: {agent["name"]} ({agent["id"]})");
                sb.AppendLine($"    Operation: {agent["operation"]}");
            }
            sb.AppendLine();
        }
    }

    return sb.ToString();
});

// ----------------------------------------------------------------------
// Workflow Management Tools (Forge Lite)
// ----------------------------------------------------------------------

server.RegisterTool("GetWorkflows", async (args) =>
{
    var workflowType = args["type"]?.Value<string>();
    var enabled = args["enabled"]?.Value<bool?>();

    var url = "/api/automation/workflows";
    var queryParams = new List<string>();
    if (!string.IsNullOrEmpty(workflowType)) queryParams.Add($"type={Uri.EscapeDataString(workflowType)}");
    if (enabled.HasValue) queryParams.Add($"enabled={enabled.Value}");
    if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get workflows: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var workflows = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine("# Workflows");
    sb.AppendLine($"Total: {workflows.Count}");
    sb.AppendLine();

    // Group by trigger type
    var grouped = workflows.GroupBy(w => w["trigger"]?["type"]?.ToString() ?? "manual");
    foreach (var group in grouped)
    {
        var triggerIcon = group.Key switch
        {
            "schedule" => "🕐",
            "file_change" => "📁",
            "webhook" => "🌐",
            "manual" => "👆",
            _ => "⚙️"
        };

        sb.AppendLine($"## {triggerIcon} {group.Key.ToUpper()} Triggers");
        foreach (var workflow in group)
        {
            var enabledIcon = workflow["enabled"]?.Value<bool>() == true ? "✅" : "⭕";
            sb.AppendLine($"  {enabledIcon} @workflow[{workflow["id"]}] \"{workflow["name"]}\"");
            sb.AppendLine($"     {workflow["description"] ?? "No description"}");

            // Show trigger details
            var trigger = workflow["trigger"];
            if (trigger != null)
            {
                if (trigger["schedule"] != null)
                    sb.AppendLine($"     Schedule: {trigger["schedule"]["cron"]}");
                if (trigger["filePatterns"] is JArray patterns)
                    sb.AppendLine($"     Patterns: {string.Join(", ", patterns.Select(p => p.ToString()))}");
            }
        }
        sb.AppendLine();
    }

    if (workflows.Count == 0)
    {
        sb.AppendLine("No workflows configured.");
    }

    return sb.ToString();
});

server.RegisterTool("GetWorkflowDetails", async (args) =>
{
    var workflowId = args["workflowId"]?.Value<string>() ?? throw new ArgumentException("workflowId required");

    var response = await server.Http.GetAsync($"/api/automation/workflows/{workflowId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Workflow not found: {response.StatusCode}");

    var workflow = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"@workflow[{workflow["id"]}] {{");
    sb.AppendLine($"  name: \"{workflow["name"]}\"");
    sb.AppendLine($"  description: \"{workflow["description"] ?? ""}\"");
    sb.AppendLine($"  enabled: {workflow["enabled"]}");
    sb.AppendLine();

    // Trigger
    var trigger = workflow["trigger"];
    if (trigger != null)
    {
        sb.AppendLine($"  ## Trigger");
        sb.AppendLine($"  type: \"{trigger["type"]}\"");

        if (trigger["schedule"] != null)
        {
            sb.AppendLine($"  schedule:");
            sb.AppendLine($"    cron: \"{trigger["schedule"]["cron"]}\"");
            if (trigger["schedule"]["timezone"] != null)
                sb.AppendLine($"    timezone: \"{trigger["schedule"]["timezone"]}\"");
        }

        if (trigger["filePatterns"] is JArray patterns)
        {
            sb.AppendLine($"  filePatterns:");
            foreach (var p in patterns)
                sb.AppendLine($"    - \"{p}\"");
        }

        sb.AppendLine($"  manual: {trigger["manual"] ?? false}");
    }

    // Steps
    if (workflow["steps"] is JArray steps)
    {
        sb.AppendLine();
        sb.AppendLine($"  ## Steps ({steps.Count})");
        var stepNum = 1;
        foreach (var step in steps)
        {
            sb.AppendLine($"  {stepNum}. [{step["id"]}] {step["name"]}");
            sb.AppendLine($"     Action: {step["action"]}");
            if (step["params"] is JObject stepParams && stepParams.Count > 0)
            {
                sb.AppendLine($"     Params: {stepParams.ToString(Newtonsoft.Json.Formatting.None)}");
            }
            stepNum++;
        }
    }

    // Notifications
    if (workflow["notifications"] is JObject notifications)
    {
        sb.AppendLine();
        sb.AppendLine($"  ## Notifications");
        if (notifications["onSuccess"] != null)
            sb.AppendLine($"  onSuccess: {notifications["onSuccess"]}");
        if (notifications["onFailure"] != null)
            sb.AppendLine($"  onFailure: {notifications["onFailure"]}");
    }

    sb.AppendLine("}");
    return sb.ToString();
});

server.RegisterTool("ExecuteWorkflow", async (args) =>
{
    var workflowId = args["workflowId"]?.Value<string>() ?? throw new ArgumentException("workflowId required");
    var variables = args["variables"] as JObject;

    var payload = new JObject
    {
        ["workflowId"] = workflowId
    };
    if (variables != null)
        payload["variables"] = variables;

    var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
    var response = await server.Http.PostAsync($"/api/automation/workflows/{workflowId}/execute", content);

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to execute workflow: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"🚀 Workflow execution started");
    sb.AppendLine($"  Workflow: {workflowId}");
    sb.AppendLine($"  Run ID: {result["runId"]}");
    sb.AppendLine($"  Status: {result["status"]}");

    return sb.ToString();
});

server.RegisterTool("GetWorkflowRuns", async (args) =>
{
    var workflowId = args["workflowId"]?.Value<string>();
    var status = args["status"]?.Value<string>();
    var take = args["take"]?.Value<int>() ?? 20;

    var url = "/api/automation/workflows/runs";
    var queryParams = new List<string> { $"take={take}" };
    if (!string.IsNullOrEmpty(workflowId)) queryParams.Add($"workflowId={Uri.EscapeDataString(workflowId)}");
    if (!string.IsNullOrEmpty(status)) queryParams.Add($"status={Uri.EscapeDataString(status)}");
    url += "?" + string.Join("&", queryParams);

    var response = await server.Http.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get workflow runs: {response.StatusCode}");

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var runs = result["data"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine("# Workflow Runs");
    sb.AppendLine($"Showing: {runs.Count} runs");
    sb.AppendLine();

    foreach (var run in runs)
    {
        var runStatus = run["status"]?.ToString() ?? "unknown";
        var statusIcon = runStatus switch
        {
            "Success" or "Completed" => "✅",
            "Running" or "InProgress" => "🔄",
            "Failed" or "Error" => "❌",
            "Cancelled" => "⏹️",
            "Pending" => "⏳",
            _ => "⚪"
        };

        sb.AppendLine($"{statusIcon} @workflowRun[{run["id"]}]");
        sb.AppendLine($"   Workflow: {run["workflowName"] ?? run["workflowId"]}");
        sb.AppendLine($"   Status: {runStatus}");
        sb.AppendLine($"   Started: {run["startedAt"]}");
        if (run["completedAt"] != null)
            sb.AppendLine($"   Completed: {run["completedAt"]}");
        if (run["duration"] != null)
            sb.AppendLine($"   Duration: {run["duration"]}");
        if (runStatus == "Failed" && run["error"] != null)
            sb.AppendLine($"   Error: {run["error"]}");
        sb.AppendLine();
    }

    if (runs.Count == 0)
    {
        sb.AppendLine("No workflow runs found.");
    }

    return sb.ToString();
});

server.RegisterTool("ToggleWorkflow", async (args) =>
{
    var workflowId = args["workflowId"]?.Value<string>() ?? throw new ArgumentException("workflowId required");
    var enabled = args["enabled"]?.Value<bool>() ?? throw new ArgumentException("enabled required");

    var payload = new JObject
    {
        ["enabled"] = enabled
    };

    var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
    var response = await server.Http.PatchAsync($"/api/automation/workflows/{workflowId}", content);

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to toggle workflow: {response.StatusCode}");

    return $"{(enabled ? "✅ Enabled" : "⭕ Disabled")} workflow {workflowId}";
});

// ----------------------------------------------------------------------
// Context Navigation Tools (User, Tenant, Project)
// ----------------------------------------------------------------------

server.RegisterTool("GetUserContext", async (args) =>
{
    var response = await server.Http.GetAsync("/api/context/bootstrap");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get user context: {response.StatusCode}");

    var bootstrap = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine("@userContext {");
    sb.AppendLine($"  userId: {bootstrap["id"]}");
    sb.AppendLine($"  email: \"{bootstrap["email"]}\"");
    sb.AppendLine($"  name: \"{bootstrap["firstName"]} {bootstrap["lastName"]}\"");
    sb.AppendLine();

    var tenants = bootstrap["tenants"] as JArray ?? new JArray();
    sb.AppendLine($"  tenants: [{tenants.Count}] {{");
    foreach (var tenant in tenants)
    {
        sb.AppendLine($"    @tenant[{tenant["id"]}] {{ name: \"{tenant["name"]}\", slug: \"{tenant["slug"]}\" }}");
    }
    sb.AppendLine("  }");
    sb.AppendLine();

    var workspaces = bootstrap["workspaces"] as JArray ?? new JArray();
    sb.AppendLine($"  workspaces: [{workspaces.Count}] {{");
    foreach (var ws in workspaces)
    {
        sb.AppendLine($"    @workspace[{ws["id"]}] {{ name: \"{ws["name"]}\", slug: \"{ws["slug"]}\", type: \"{ws["workspaceType"]}\" }}");
    }
    sb.AppendLine("  }");
    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("GetTenantContext", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");

    var response = await server.Http.GetAsync($"/api/context/tenant/{tenantId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get tenant context: {response.StatusCode}");

    var tenant = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"@tenantContext[{tenantId}] {{");
    sb.AppendLine($"  name: \"{tenant["name"]}\"");
    sb.AppendLine($"  slug: \"{tenant["slug"]}\"");
    sb.AppendLine($"  features: [{string.Join(", ", (tenant["features"] as JArray ?? new JArray()).Select(f => f.ToString()))}]");
    sb.AppendLine();

    var workspaces = tenant["workspaces"] as JArray ?? new JArray();
    sb.AppendLine($"  workspaces: [{workspaces.Count}] {{");
    foreach (var ws in workspaces)
    {
        sb.AppendLine($"    @workspace[{ws["id"]}] {{");
        sb.AppendLine($"      name: \"{ws["name"]}\"");
        sb.AppendLine($"      slug: \"{ws["slug"]}\"");
        sb.AppendLine($"      type: \"{ws["workspaceType"]}\"");
        sb.AppendLine($"    }}");
    }
    sb.AppendLine("  }");
    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("GetProjectContext", async (args) =>
{
    var projectId = args["projectId"]?.Value<int>() ?? throw new ArgumentException("projectId required");

    var response = await server.Http.GetAsync($"/api/projects/{projectId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get project: {response.StatusCode}");

    var project = JObject.Parse(await response.Content.ReadAsStringAsync());

    // Get tasks for this project
    var tasksResponse = await server.Http.GetAsync($"/api/tasks?projectId={projectId}&limit=50");
    var tasks = tasksResponse.IsSuccessStatusCode
        ? JObject.Parse(await tasksResponse.Content.ReadAsStringAsync())["data"] as JArray ?? new JArray()
        : new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"@projectContext[{projectId}] {{");
    sb.AppendLine($"  name: \"{project["name"]}\"");
    sb.AppendLine($"  description: \"{project["description"] ?? ""}\"");
    sb.AppendLine($"  status: \"{project["status"]}\"");
    sb.AppendLine($"  workspaceId: {project["workspaceId"]}");
    sb.AppendLine();

    sb.AppendLine($"  tasks: [{tasks.Count}] {{");
    foreach (var task in tasks.Take(20))
    {
        var status = task["percentComplete"]?.Value<int>() == 100 ? "✓" :
                     task["percentComplete"]?.Value<int>() > 0 ? "●" : "○";
        sb.AppendLine($"    {status} @task[{task["id"]}] {{ name: \"{task["name"]}\", progress: {task["percentComplete"] ?? 0}% }}");
    }
    if (tasks.Count > 20) sb.AppendLine($"    ... and {tasks.Count - 20} more tasks");
    sb.AppendLine("  }");

    sb.AppendLine("}");
    return sb.ToString();
});

// ----------------------------------------------------------------------
// Agent Plan Tools (TodoWrite-style)
// ----------------------------------------------------------------------

server.RegisterTool("AgentPlanGet", async (args) =>
{
    var agentId = args["agentId"]?.Value<int>() ?? throw new ArgumentException("agentId required");

    var response = await server.Http.PostAsync($"/api/agents/coordination/GetAgentContext?agentId={agentId}",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get agent plan: {response.StatusCode}");

    var context = JObject.Parse(await response.Content.ReadAsStringAsync());
    var todos = context["todoList"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"@agentPlan[{agentId}] {{");
    sb.AppendLine($"  taskId: \"{context["taskId"] ?? ""}\"");
    sb.AppendLine($"  taskName: \"{context["taskName"] ?? ""}\"");
    sb.AppendLine();

    var completed = todos.Count(t => t["status"]?.ToString() == "Completed");
    var inProgress = todos.Count(t => t["status"]?.ToString() == "InProgress");
    var pending = todos.Count - completed - inProgress;

    sb.AppendLine($"  progress: {completed}/{todos.Count} ({(todos.Count > 0 ? completed * 100 / todos.Count : 0)}%)");
    sb.AppendLine();

    sb.AppendLine($"  todos: [{todos.Count}] {{");
    int idx = 0;
    foreach (var todo in todos)
    {
        var status = todo["status"]?.ToString();
        var marker = status == "Completed" ? "✓" : status == "InProgress" ? "●" : "○";
        sb.AppendLine($"    [{idx}] {marker} {todo["content"]}");
        idx++;
    }
    sb.AppendLine("  }");
    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("AgentPlanAdd", async (args) =>
{
    var agentId = args["agentId"]?.Value<int>() ?? throw new ArgumentException("agentId required");
    var content = args["content"]?.Value<string>() ?? throw new ArgumentException("content required");
    var status = args["status"]?.Value<string>() ?? "Pending";

    var todoItem = new JObject
    {
        ["content"] = content,
        ["status"] = status
    };

    var response = await server.Http.PostAsync($"/api/agents/coordination/AddTodo?agentId={agentId}",
        new StringContent(todoItem.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to add plan item: {response.StatusCode}");

    return $"✓ Added to agent {agentId} plan: \"{content}\" [{status}]";
});

server.RegisterTool("AgentPlanUpdate", async (args) =>
{
    var agentId = args["agentId"]?.Value<int>() ?? throw new ArgumentException("agentId required");
    var index = args["index"]?.Value<int>() ?? throw new ArgumentException("index required");
    var status = args["status"]?.Value<string>() ?? throw new ArgumentException("status required (Pending, InProgress, Completed, Skipped)");

    var update = new JObject
    {
        ["index"] = index,
        ["status"] = status
    };

    var response = await server.Http.PostAsync($"/api/agents/coordination/UpdateTodoStatus?agentId={agentId}",
        new StringContent(update.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to update plan item: {response.StatusCode}");

    var statusIcon = status switch
    {
        "Completed" => "✓",
        "InProgress" => "●",
        "Skipped" => "⊘",
        _ => "○"
    };

    return $"{statusIcon} Updated item [{index}] to {status}";
});

// ----------------------------------------------------------------------
// Canvas Tools
// ----------------------------------------------------------------------

server.RegisterTool("GetCanvasList", async (args) =>
{
    var workspaceId = args["workspaceId"]?.Value<int>() ?? throw new ArgumentException("workspaceId required");

    var response = await server.Http.GetAsync($"/api/canvas?workspaceId={workspaceId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get canvases: {response.StatusCode}");

    var canvases = JArray.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"Canvases i workspace {workspaceId} ({canvases.Count}):");
    sb.AppendLine();

    var grouped = canvases.GroupBy(c => c["parentFolderName"]?.ToString() ?? "(root)");
    foreach (var group in grouped)
    {
        sb.AppendLine($"📁 {group.Key}");
        foreach (var canvas in group)
        {
            var icon = canvas["isPinned"]?.Value<bool>() == true ? "📌" : "📋";
            var archived = canvas["isArchived"]?.Value<bool>() == true ? " [archived]" : "";
            sb.AppendLine($"  {icon} @canvas[{canvas["id"]}] {{");
            sb.AppendLine($"      name: \"{canvas["name"]}\"");
            sb.AppendLine($"      nodes: {canvas["nodeCount"] ?? 0}");
            sb.AppendLine($"      edges: {canvas["edgeCount"] ?? 0}{archived}");
            sb.AppendLine($"  }}");
        }
        sb.AppendLine();
    }

    return sb.ToString();
});

server.RegisterTool("GetCanvasDetail", async (args) =>
{
    var canvasId = args["canvasId"]?.Value<int>() ?? throw new ArgumentException("canvasId required");

    var response = await server.Http.GetAsync($"/api/canvas/{canvasId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get canvas: {response.StatusCode}");

    var canvas = JObject.Parse(await response.Content.ReadAsStringAsync());
    var nodes = canvas["nodes"] as JArray ?? new JArray();
    var edges = canvas["edges"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    sb.AppendLine($"@canvasDetail[{canvasId}] {{");
    sb.AppendLine($"  name: \"{canvas["name"]}\"");
    sb.AppendLine($"  description: \"{canvas["description"] ?? ""}\"");
    sb.AppendLine($"  storageMode: \"{canvas["storageMode"]}\"");
    sb.AppendLine($"  createdBy: \"{canvas["createdByName"]}\"");
    sb.AppendLine();

    sb.AppendLine($"  nodes: [{nodes.Count}] {{");
    foreach (var node in nodes.Take(15))
    {
        var nodeType = node["type"]?.ToString();
        var content = nodeType switch
        {
            "Text" or "0" => node["textContent"]?.ToString()?.Substring(0, Math.Min(50, node["textContent"]?.ToString()?.Length ?? 0)),
            "Entity" or "4" => $"{node["entityType"]} #{node["entityId"]}",
            "Link" or "2" => node["linkUrl"]?.ToString(),
            "File" or "1" => node["filePath"]?.ToString(),
            "Group" or "3" => $"[Group: {node["groupLabel"]}]",
            _ => nodeType
        };
        var nodeId = node["id"]?.ToString() ?? "";
        var shortId = nodeId.Length > 8 ? nodeId.Substring(0, 8) : nodeId;
        sb.AppendLine($"    [{shortId}] {nodeType}: {content}");
    }
    if (nodes.Count > 15) sb.AppendLine($"    ... and {nodes.Count - 15} more");
    sb.AppendLine("  }");
    sb.AppendLine();

    sb.AppendLine($"  edges: [{edges.Count}] {{");
    foreach (var edge in edges.Take(10))
    {
        var fromId = edge["fromNodeId"]?.ToString() ?? "";
        var toId = edge["toNodeId"]?.ToString() ?? "";
        var shortFrom = fromId.Length > 8 ? fromId.Substring(0, 8) : fromId;
        var shortTo = toId.Length > 8 ? toId.Substring(0, 8) : toId;
        var label = edge["label"]?.ToString();
        var labelStr = string.IsNullOrEmpty(label) ? "" : $" ({label})";
        sb.AppendLine($"    {shortFrom} -> {shortTo}{labelStr}");
    }
    if (edges.Count > 10) sb.AppendLine($"    ... and {edges.Count - 10} more");
    sb.AppendLine("  }");

    sb.AppendLine("}");
    return sb.ToString();
});

server.RegisterTool("CreateCanvas", async (args) =>
{
    var workspaceId = args["workspaceId"]?.Value<int>() ?? throw new ArgumentException("workspaceId required");
    var name = args["name"]?.Value<string>() ?? throw new ArgumentException("name required");
    var description = args["description"]?.Value<string>();

    var request = new JObject
    {
        ["workspaceId"] = workspaceId,
        ["name"] = name,
        ["description"] = description,
        ["isPersonal"] = false,
        ["isTemplate"] = false
    };

    var response = await server.Http.PostAsync("/api/canvas",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to create canvas: {response.StatusCode} - {error}");
    }

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    return $"✓ Canvas oprettet med ID: {result["id"]}";
});

server.RegisterTool("AddCanvasNode", async (args) =>
{
    var canvasId = args["canvasId"]?.Value<int>() ?? throw new ArgumentException("canvasId required");
    var nodeType = args["type"]?.Value<string>() ?? "Text";
    var content = args["content"]?.Value<string>();
    var entityId = args["entityId"]?.Value<int?>();
    var x = args["x"]?.Value<double>() ?? 100;
    var y = args["y"]?.Value<double>() ?? 100;

    var typeValue = nodeType.ToLower() switch
    {
        "text" => 0,
        "file" => 1,
        "link" => 2,
        "group" => 3,
        "entity" => 4,
        _ => 0
    };

    var request = new JObject
    {
        ["type"] = typeValue,
        ["x"] = x,
        ["y"] = y,
        ["width"] = 300,
        ["height"] = 200
    };

    switch (nodeType.ToLower())
    {
        case "text":
            request["textContent"] = content;
            break;
        case "link":
            request["linkUrl"] = content;
            break;
        case "file":
            request["filePath"] = content;
            break;
        case "group":
            request["groupLabel"] = content;
            break;
        case "entity":
            request["entityType"] = content;
            request["entityId"] = entityId;
            break;
    }

    var response = await server.Http.PostAsync($"/api/canvas/{canvasId}/nodes",
        new StringContent(request.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to add node: {response.StatusCode} - {error}");
    }

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    return $"✓ Node tilføjet med ID: {result["nodeId"]}";
});

server.RegisterTool("SaveContextToCanvas", async (args) =>
{
    var agentId = args["agentId"]?.Value<int>() ?? throw new ArgumentException("agentId required");
    var canvasId = args["canvasId"]?.Value<int>() ?? throw new ArgumentException("canvasId required");

    // Get agent context
    var contextResponse = await server.Http.PostAsync($"/api/agents/coordination/GetAgentContext?agentId={agentId}",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!contextResponse.IsSuccessStatusCode)
        throw new Exception($"Failed to get agent context: {contextResponse.StatusCode}");

    var context = JObject.Parse(await contextResponse.Content.ReadAsStringAsync());
    var todos = context["todoList"] as JArray ?? new JArray();
    var files = context["activeFiles"] as JArray ?? new JArray();

    var sb = new StringBuilder();
    var nodesCreated = 0;

    // Create a group node for the context
    var groupRequest = new JObject
    {
        ["type"] = 3, // Group
        ["x"] = 50,
        ["y"] = 50,
        ["width"] = 600,
        ["height"] = 400,
        ["groupLabel"] = $"Agent #{agentId} - {context["taskName"]}"
    };

    var groupResponse = await server.Http.PostAsync($"/api/canvas/{canvasId}/nodes",
        new StringContent(groupRequest.ToString(), Encoding.UTF8, "application/json"));

    if (groupResponse.IsSuccessStatusCode)
    {
        nodesCreated++;
        sb.AppendLine($"✓ Oprettet gruppe: Agent #{agentId} - {context["taskName"]}");
    }

    // Create text nodes for todos
    var yOffset = 100;
    foreach (var todo in todos.Take(10))
    {
        var status = todo["status"]?.ToString();
        var marker = status == "Completed" ? "✓" : status == "InProgress" ? "●" : "○";
        var todoText = $"{marker} {todo["content"]}";

        var todoRequest = new JObject
        {
            ["type"] = 0, // Text
            ["x"] = 80,
            ["y"] = yOffset,
            ["width"] = 300,
            ["height"] = 40,
            ["textContent"] = todoText
        };

        var todoResponse = await server.Http.PostAsync($"/api/canvas/{canvasId}/nodes",
            new StringContent(todoRequest.ToString(), Encoding.UTF8, "application/json"));

        if (todoResponse.IsSuccessStatusCode)
        {
            nodesCreated++;
        }
        yOffset += 50;
    }

    // Create text nodes for active files
    foreach (var file in files.Take(5))
    {
        var fileRequest = new JObject
        {
            ["type"] = 0, // Text
            ["x"] = 400,
            ["y"] = yOffset - (todos.Count * 50) + 100,
            ["width"] = 200,
            ["height"] = 30,
            ["textContent"] = $"📄 {file["filePath"]}"
        };

        var fileResponse = await server.Http.PostAsync($"/api/canvas/{canvasId}/nodes",
            new StringContent(fileRequest.ToString(), Encoding.UTF8, "application/json"));

        if (fileResponse.IsSuccessStatusCode)
        {
            nodesCreated++;
        }
    }

    sb.AppendLine($"✓ Gemt {nodesCreated} nodes til canvas #{canvasId}");
    sb.AppendLine($"  - {Math.Min(todos.Count, 10)} todos");
    sb.AppendLine($"  - {Math.Min(files.Count, 5)} aktive filer");

    return sb.ToString();
});

// ----------------------------------------------------------------------
// Tenant Feature Management Tools
// ----------------------------------------------------------------------

server.RegisterTool("GetTenantModules", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");

    var response = await server.Http.GetAsync($"/api/tenants/{tenantId}/features/GetModulesWithFeatures");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get tenant modules: {response.StatusCode}");

    var modules = JArray.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"@tenantModules[{tenantId}] {{");
    sb.AppendLine($"  totalModules: {modules.Count}");
    sb.AppendLine();

    foreach (var module in modules)
    {
        var features = module["features"] as JArray ?? new JArray();
        var enabledCount = features.Count(f => f["isEnabled"]?.Value<bool>() == true);
        var isCore = module["isCore"]?.Value<bool>() == true;

        sb.AppendLine($"  @module[{module["id"]}] {{");
        sb.AppendLine($"    name: \"{module["name"]}\"");
        sb.AppendLine($"    icon: \"{module["icon"] ?? ""}\"");
        sb.AppendLine($"    isCore: {isCore.ToString().ToLower()}");
        sb.AppendLine($"    features: {enabledCount}/{features.Count} enabled");

        if (features.Count > 0)
        {
            sb.AppendLine($"    featureList: [");
            foreach (var feature in features)
            {
                var isEnabled = feature["isEnabled"]?.Value<bool>() == true;
                var marker = isEnabled ? "✓" : "○";
                sb.AppendLine($"      {marker} @feature[{feature["id"]}] \"{feature["name"]}\"");
            }
            sb.AppendLine($"    ]");
        }
        sb.AppendLine($"  }}");
    }
    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("GetTenantFeatures", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var moduleId = args["moduleId"]?.Value<int>();

    var response = await server.Http.GetAsync($"/api/tenants/{tenantId}/features/GetFeatures");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get tenant features: {response.StatusCode}");

    var features = JArray.Parse(await response.Content.ReadAsStringAsync());

    // Filter by module if specified
    if (moduleId.HasValue)
    {
        features = new JArray(features.Where(f => f["moduleId"]?.Value<int>() == moduleId.Value));
    }

    var sb = new StringBuilder();
    sb.AppendLine($"@tenantFeatures[{tenantId}] {{");
    sb.AppendLine($"  totalFeatures: {features.Count}");
    sb.AppendLine($"  enabled: {features.Count(f => f["isEnabled"]?.Value<bool>() == true)}");
    sb.AppendLine($"  disabled: {features.Count(f => f["isEnabled"]?.Value<bool>() != true)}");
    sb.AppendLine();

    // Group by module for cleaner output
    var groupedFeatures = features.GroupBy(f => f["moduleName"]?.ToString() ?? "Unknown");
    foreach (var group in groupedFeatures)
    {
        sb.AppendLine($"  [{group.Key}] {{");
        foreach (var feature in group)
        {
            var isEnabled = feature["isEnabled"]?.Value<bool>() == true;
            var marker = isEnabled ? "✓" : "○";
            var canOverride = feature["canUserOverride"]?.Value<bool>() == true;
            var overrideHint = canOverride ? "" : " [locked]";
            sb.AppendLine($"    {marker} @feature[{feature["id"]}] \"{feature["name"]}\"{overrideHint}");

            // Show dependencies if any
            var deps = feature["dependsOnFeatureIds"] as JArray;
            if (deps != null && deps.Count > 0)
            {
                sb.AppendLine($"       → depends on: [{string.Join(", ", deps.Select(d => d.ToString()))}]");
            }
        }
        sb.AppendLine($"  }}");
    }
    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("EnableTenantFeature", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var featureId = args["featureId"]?.Value<int>() ?? throw new ArgumentException("featureId required");
    var notes = args["notes"]?.Value<string>();

    var payload = new JObject();
    if (!string.IsNullOrEmpty(notes))
    {
        payload["notes"] = notes;
    }

    var response = await server.Http.PostAsync(
        $"/api/tenants/{tenantId}/features/EnableFeature?featureId={featureId}",
        new StringContent(payload.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to enable feature: {response.StatusCode} - {error}");
    }

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var success = result["success"]?.Value<bool>() == true;

    if (success)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"✅ Feature {featureId} enabled for tenant {tenantId}");

        var enabledPrereqs = result["enabledPrerequisites"] as JArray;
        if (enabledPrereqs != null && enabledPrereqs.Count > 0)
        {
            sb.AppendLine($"   Also enabled prerequisites: {string.Join(", ", enabledPrereqs.Select(p => p.ToString()))}");
        }
        return sb.ToString();
    }
    else
    {
        var message = result["message"]?.ToString() ?? "Unknown error";
        var blockedBy = result["blockedByFeatures"] as JArray;
        var sb = new StringBuilder();
        sb.AppendLine($"❌ Failed to enable feature: {message}");
        if (blockedBy != null && blockedBy.Count > 0)
        {
            sb.AppendLine($"   Blocked by: {string.Join(", ", blockedBy.Select(b => b.ToString()))}");
        }
        return sb.ToString();
    }
});

server.RegisterTool("DisableTenantFeature", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var featureId = args["featureId"]?.Value<int>() ?? throw new ArgumentException("featureId required");
    var force = args["force"]?.Value<bool>() ?? false;
    var notes = args["notes"]?.Value<string>();

    var payload = new JObject
    {
        ["force"] = force
    };
    if (!string.IsNullOrEmpty(notes))
    {
        payload["notes"] = notes;
    }

    var response = await server.Http.PostAsync(
        $"/api/tenants/{tenantId}/features/DisableFeature?featureId={featureId}",
        new StringContent(payload.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to disable feature: {response.StatusCode} - {error}");
    }

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var success = result["success"]?.Value<bool>() == true;

    if (success)
    {
        return $"✅ Feature {featureId} disabled for tenant {tenantId}";
    }
    else
    {
        var message = result["message"]?.ToString() ?? "Unknown error";
        var dependents = result["blockingDependents"] as JArray;
        var sb = new StringBuilder();
        sb.AppendLine($"❌ Failed to disable feature: {message}");
        if (dependents != null && dependents.Count > 0)
        {
            sb.AppendLine($"   Blocked by dependents: {string.Join(", ", dependents.Select(d => d.ToString()))}");
            sb.AppendLine($"   Use force=true to disable anyway (will also disable dependents)");
        }
        return sb.ToString();
    }
});

server.RegisterTool("DisableTenantModule", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var moduleId = args["moduleId"]?.Value<int>() ?? throw new ArgumentException("moduleId required");
    var force = args["force"]?.Value<bool>() ?? false;

    var payload = new JObject
    {
        ["force"] = force
    };

    var response = await server.Http.PostAsync(
        $"/api/tenants/{tenantId}/features/DisableModule?moduleId={moduleId}",
        new StringContent(payload.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to disable module: {response.StatusCode} - {error}");
    }

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var success = result["success"]?.Value<bool>() == true;

    if (success)
    {
        var disabledCount = result["disabledFeatures"]?.Value<int>() ?? 0;
        return $"✅ Module {moduleId} disabled for tenant {tenantId} ({disabledCount} features disabled)";
    }
    else
    {
        var message = result["message"]?.ToString() ?? "Unknown error";
        return $"❌ Failed to disable module: {message}";
    }
});

server.RegisterTool("GetFeaturePrerequisites", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var featureId = args["featureId"]?.Value<int>() ?? throw new ArgumentException("featureId required");

    var response = await server.Http.GetAsync($"/api/tenants/{tenantId}/features/GetPrerequisites?featureId={featureId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get prerequisites: {response.StatusCode}");

    var prereqs = JArray.Parse(await response.Content.ReadAsStringAsync());

    if (prereqs.Count == 0)
    {
        return $"Feature {featureId} has no prerequisites - it can be enabled immediately.";
    }

    var sb = new StringBuilder();
    sb.AppendLine($"@featurePrerequisites[{featureId}] {{");
    sb.AppendLine($"  count: {prereqs.Count}");
    sb.AppendLine($"  features: [");
    foreach (var prereq in prereqs)
    {
        var isEnabled = prereq["isEnabled"]?.Value<bool>() == true;
        var marker = isEnabled ? "✓" : "○";
        sb.AppendLine($"    {marker} @feature[{prereq["id"]}] \"{prereq["name"]}\" ({prereq["moduleName"]})");
    }
    sb.AppendLine($"  ]");
    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("GetFeatureDependents", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var featureId = args["featureId"]?.Value<int>() ?? throw new ArgumentException("featureId required");

    var response = await server.Http.GetAsync($"/api/tenants/{tenantId}/features/GetDependents?featureId={featureId}");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get dependents: {response.StatusCode}");

    var dependents = JArray.Parse(await response.Content.ReadAsStringAsync());

    if (dependents.Count == 0)
    {
        return $"No features depend on feature {featureId} - it can be disabled safely.";
    }

    var sb = new StringBuilder();
    sb.AppendLine($"@featureDependents[{featureId}] {{");
    sb.AppendLine($"  count: {dependents.Count}");
    sb.AppendLine($"  ⚠️  These features will be affected if you disable feature {featureId}:");
    sb.AppendLine($"  features: [");
    foreach (var dep in dependents)
    {
        var isEnabled = dep["isEnabled"]?.Value<bool>() == true;
        var marker = isEnabled ? "✓" : "○";
        sb.AppendLine($"    {marker} @feature[{dep["id"]}] \"{dep["name"]}\" ({dep["moduleName"]})");
    }
    sb.AppendLine($"  ]");
    sb.AppendLine("}");

    return sb.ToString();
});

// ----------------------------------------------------------------------
// Admin Feature Tools (System-level management)
// ----------------------------------------------------------------------

server.RegisterTool("GetAllSystemModules", async (args) =>
{
    var response = await server.Http.GetAsync("/api/administration/features/GetAllModules");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get system modules: {response.StatusCode}");

    var modules = JArray.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine("@systemModules {");
    sb.AppendLine($"  totalModules: {modules.Count}");
    sb.AppendLine($"  totalFeatures: {modules.Sum(m => (m["features"] as JArray)?.Count ?? 0)}");
    sb.AppendLine();

    foreach (var module in modules)
    {
        var features = module["features"] as JArray ?? new JArray();
        var isCore = module["isCore"]?.Value<bool>() == true;
        var coreHint = isCore ? " [CORE]" : "";

        sb.AppendLine($"  @module[{module["id"]}] \"{module["name"]}\"{coreHint} {{");
        sb.AppendLine($"    description: \"{module["description"] ?? ""}\"");
        sb.AppendLine($"    icon: \"{module["icon"] ?? ""}\"");
        sb.AppendLine($"    featureCount: {features.Count}");
        sb.AppendLine($"    features: [");
        foreach (var feature in features)
        {
            var isDefault = feature["isDefault"]?.Value<bool>() == true;
            var defaultHint = isDefault ? " [default on]" : "";
            var canOverride = feature["canUserOverride"]?.Value<bool>() == true;
            var overrideHint = canOverride ? "" : " [locked]";
            sb.AppendLine($"      @feature[{feature["id"]}] \"{feature["name"]}\"{defaultHint}{overrideHint}");
        }
        sb.AppendLine($"    ]");
        sb.AppendLine($"  }}");
    }
    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("AdminOverrideFeature", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var featureId = args["featureId"]?.Value<int>() ?? throw new ArgumentException("featureId required");
    var action = args["action"]?.Value<string>()?.ToLower() ?? throw new ArgumentException("action required (grant, revoke, remove)");
    var notes = args["notes"]?.Value<string>() ?? "Admin override via MCP";

    // Map action to enum value
    var actionValue = action switch
    {
        "grant" => 0,
        "revoke" => 1,
        "remove" => 2,
        _ => throw new ArgumentException("action must be 'grant', 'revoke', or 'remove'")
    };

    var payload = new JObject
    {
        ["featureId"] = featureId,
        ["action"] = actionValue,
        ["notes"] = notes
    };

    var response = await server.Http.PostAsync(
        $"/api/administration/features/OverrideFeature?tenantId={tenantId}",
        new StringContent(payload.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to override feature: {response.StatusCode} - {error}");
    }

    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
    var success = result["success"]?.Value<bool>() == true;

    if (success)
    {
        return action switch
        {
            "grant" => $"✅ Feature {featureId} granted to tenant {tenantId}",
            "revoke" => $"✅ Feature {featureId} revoked from tenant {tenantId}",
            "remove" => $"✅ Feature override removed for tenant {tenantId} (will use plan default)",
            _ => $"✅ Feature override applied"
        };
    }
    else
    {
        var message = result["message"]?.ToString() ?? "Unknown error";
        return $"❌ Failed to override feature: {message}";
    }
});

server.RegisterTool("AdminBulkEnableFeatures", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var featureIdsToken = args["featureIds"] ?? throw new ArgumentException("featureIds required (array of feature IDs)");

    var featureIds = new List<int>();
    if (featureIdsToken is JArray arr)
    {
        foreach (var id in arr)
        {
            featureIds.Add(id.Value<int>());
        }
    }
    else
    {
        // Support comma-separated string
        var idStr = featureIdsToken.Value<string>() ?? "";
        foreach (var id in idStr.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(id.Trim(), out var parsed))
                featureIds.Add(parsed);
        }
    }

    if (featureIds.Count == 0)
        throw new ArgumentException("No valid feature IDs provided");

    var response = await server.Http.PostAsync(
        $"/api/administration/features/BulkEnableFeatures?tenantId={tenantId}",
        new StringContent(new JArray(featureIds).ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to bulk enable features: {response.StatusCode} - {error}");
    }

    var results = JObject.Parse(await response.Content.ReadAsStringAsync());

    var sb = new StringBuilder();
    sb.AppendLine($"@bulkEnableResult[tenant:{tenantId}] {{");

    int successCount = 0, failCount = 0;
    foreach (var prop in results.Properties())
    {
        var featureResult = prop.Value as JObject;
        var success = featureResult?["success"]?.Value<bool>() == true;
        var marker = success ? "✓" : "✗";
        if (success) successCount++; else failCount++;

        sb.AppendLine($"  {marker} feature[{prop.Name}]: {(success ? "enabled" : featureResult?["message"]?.ToString() ?? "failed")}");
    }

    sb.AppendLine();
    sb.AppendLine($"  summary: {successCount} succeeded, {failCount} failed");
    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("GetAllFeatureOverrides", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>();

    var response = await server.Http.GetAsync("/api/administration/features/GetAllOverrides");
    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get feature overrides: {response.StatusCode}");

    var overrides = JArray.Parse(await response.Content.ReadAsStringAsync());

    // Filter by tenant if specified
    if (tenantId.HasValue)
    {
        overrides = new JArray(overrides.Where(o => o["tenantId"]?.Value<int>() == tenantId.Value));
    }

    var sb = new StringBuilder();
    sb.AppendLine("@featureOverrides {");
    sb.AppendLine($"  count: {overrides.Count}");
    sb.AppendLine();

    // Group by tenant
    var byTenant = overrides.GroupBy(o => o["tenantId"]?.Value<int>() ?? 0);
    foreach (var tenantGroup in byTenant)
    {
        sb.AppendLine($"  @tenant[{tenantGroup.Key}] \"{tenantGroup.First()["tenantName"]}\" {{");
        foreach (var ov in tenantGroup)
        {
            var ovType = ov["overrideType"]?.ToString() ?? "Unknown";
            var marker = ovType == "Grant" ? "✓" : "✗";
            sb.AppendLine($"    {marker} @feature[{ov["featureId"]}] \"{ov["featureName"]}\" ({ovType})");
            if (ov["expiresAt"] != null)
            {
                sb.AppendLine($"       expires: {ov["expiresAt"]}");
            }
        }
        sb.AppendLine($"  }}");
    }
    sb.AppendLine("}");

    return sb.ToString();
});

// ----------------------------------------------------------------------
// Tenant Settings Tools
// ----------------------------------------------------------------------

server.RegisterTool("GetTenantSettings", async (args) =>
{
    var category = args["category"]?.Value<string>();

    var response = await server.Http.PostAsync("/api/administration/tenant/settings/Get",
        new StringContent("{}", Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        throw new Exception($"Failed to get tenant settings: {response.StatusCode}");

    var settings = JArray.Parse(await response.Content.ReadAsStringAsync());

    // Filter by category if specified
    if (!string.IsNullOrEmpty(category))
    {
        settings = new JArray(settings.Where(s =>
            (s["category"]?.ToString() ?? "").Contains(category, StringComparison.OrdinalIgnoreCase)));
    }

    var sb = new StringBuilder();
    sb.AppendLine("@tenantSettings {");
    sb.AppendLine($"  count: {settings.Count}");
    sb.AppendLine();

    // Group by category
    var byCategory = settings.GroupBy(s => s["category"]?.ToString() ?? "General");
    foreach (var catGroup in byCategory)
    {
        sb.AppendLine($"  [{catGroup.Key}] {{");
        foreach (var setting in catGroup)
        {
            var name = setting["name"]?.ToString() ?? "";
            var value = setting["value"]?.ToString() ?? "";
            var displayValue = value.Length > 50 ? value.Substring(0, 47) + "..." : value;
            sb.AppendLine($"    @setting[{setting["id"]}] \"{name}\": \"{displayValue}\"");
        }
        sb.AppendLine($"  }}");
    }
    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("UpdateTenantSetting", async (args) =>
{
    var settingId = args["settingId"]?.Value<int>() ?? throw new ArgumentException("settingId required");
    var value = args["value"]?.Value<string>() ?? throw new ArgumentException("value required");

    var payload = new JObject
    {
        ["id"] = settingId,
        ["value"] = value
    };

    var response = await server.Http.PostAsync("/api/administration/tenant/settings/Update",
        new StringContent(payload.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to update setting: {response.StatusCode} - {error}");
    }

    return $"✅ Setting {settingId} updated successfully";
});

// ----------------------------------------------------------------------
// Tenant Configuration Tools (Combined utilities)
// ----------------------------------------------------------------------

server.RegisterTool("ConfigureTenantFeatureSet", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var preset = args["preset"]?.Value<string>()?.ToLower();

    // Define presets
    var presets = new Dictionary<string, List<int>>
    {
        // These are example module IDs - adjust based on actual system modules
        ["minimal"] = new List<int> { 1, 2 },           // Core + Identity only
        ["standard"] = new List<int> { 1, 2, 3, 4 },    // Core + Identity + ProjectManagement + Calendar
        ["professional"] = new List<int> { 1, 2, 3, 4, 5, 6, 7 }, // + Registration + Finance + Reporting
        ["enterprise"] = new List<int> { }              // All features (empty = all)
    };

    if (string.IsNullOrEmpty(preset) || !presets.ContainsKey(preset))
    {
        var sb = new StringBuilder();
        sb.AppendLine("@featurePresets {");
        sb.AppendLine("  Available presets:");
        sb.AppendLine("    - minimal: Core + Identity modules only");
        sb.AppendLine("    - standard: + ProjectManagement + Calendar");
        sb.AppendLine("    - professional: + Registration + Finance + Reporting");
        sb.AppendLine("    - enterprise: All features enabled");
        sb.AppendLine();
        sb.AppendLine("  Usage: ConfigureTenantFeatureSet(tenantId, preset=\"standard\")");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // Get all features for the tenant to understand current state
    var featuresResponse = await server.Http.GetAsync($"/api/tenants/{tenantId}/features/GetModulesWithFeatures");
    if (!featuresResponse.IsSuccessStatusCode)
        throw new Exception($"Failed to get current features: {featuresResponse.StatusCode}");

    var modules = JArray.Parse(await featuresResponse.Content.ReadAsStringAsync());

    var resultSb = new StringBuilder();
    resultSb.AppendLine($"@configureResult[tenant:{tenantId}, preset:{preset}] {{");

    var enabledCount = 0;
    var moduleIds = presets[preset];

    foreach (var module in modules)
    {
        var modId = module["id"]?.Value<int>() ?? 0;
        var isCore = module["isCore"]?.Value<bool>() == true;

        // Enterprise enables all, others check module ID list
        var shouldEnable = preset == "enterprise" || moduleIds.Contains(modId) || isCore;

        var features = module["features"] as JArray ?? new JArray();
        foreach (var feature in features)
        {
            var featureId = feature["id"]?.Value<int>() ?? 0;
            var isEnabled = feature["isEnabled"]?.Value<bool>() == true;
            var canOverride = feature["canUserOverride"]?.Value<bool>() == true;

            if (shouldEnable && !isEnabled && canOverride)
            {
                // Enable this feature
                var enablePayload = new JObject();
                var enableResponse = await server.Http.PostAsync(
                    $"/api/tenants/{tenantId}/features/EnableFeature?featureId={featureId}",
                    new StringContent(enablePayload.ToString(), Encoding.UTF8, "application/json"));

                if (enableResponse.IsSuccessStatusCode)
                {
                    resultSb.AppendLine($"  ✓ Enabled: {feature["name"]}");
                    enabledCount++;
                }
            }
            else if (!shouldEnable && isEnabled && canOverride && !isCore)
            {
                // Disable this feature
                var disablePayload = new JObject { ["force"] = true };
                var disableResponse = await server.Http.PostAsync(
                    $"/api/tenants/{tenantId}/features/DisableFeature?featureId={featureId}",
                    new StringContent(disablePayload.ToString(), Encoding.UTF8, "application/json"));

                if (disableResponse.IsSuccessStatusCode)
                {
                    resultSb.AppendLine($"  ○ Disabled: {feature["name"]}");
                }
            }
        }
    }

    resultSb.AppendLine();
    resultSb.AppendLine($"  summary: Applied '{preset}' preset, {enabledCount} features adjusted");
    resultSb.AppendLine("}");

    return resultSb.ToString();
});

server.RegisterTool("GetTenantConfiguration", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");

    // Get tenant context
    var contextResponse = await server.Http.GetAsync($"/api/context/tenant/{tenantId}");
    if (!contextResponse.IsSuccessStatusCode)
        throw new Exception($"Failed to get tenant context: {contextResponse.StatusCode}");
    var tenantContext = JObject.Parse(await contextResponse.Content.ReadAsStringAsync());

    // Get features
    var featuresResponse = await server.Http.GetAsync($"/api/tenants/{tenantId}/features/GetModulesWithFeatures");
    var modules = featuresResponse.IsSuccessStatusCode
        ? JArray.Parse(await featuresResponse.Content.ReadAsStringAsync())
        : new JArray();

    // Get settings
    var settingsResponse = await server.Http.PostAsync("/api/administration/tenant/settings/Get",
        new StringContent("{}", Encoding.UTF8, "application/json"));
    var settings = settingsResponse.IsSuccessStatusCode
        ? JArray.Parse(await settingsResponse.Content.ReadAsStringAsync())
        : new JArray();

    // Build comprehensive output
    var sb = new StringBuilder();
    sb.AppendLine($"@tenantConfiguration[{tenantId}] {{");
    sb.AppendLine();

    // Basic info
    sb.AppendLine("  @info {");
    sb.AppendLine($"    name: \"{tenantContext["name"]}\"");
    sb.AppendLine($"    slug: \"{tenantContext["slug"]}\"");
    sb.AppendLine($"    workspaces: {(tenantContext["workspaces"] as JArray)?.Count ?? 0}");
    sb.AppendLine("  }");
    sb.AppendLine();

    // Feature summary
    var totalFeatures = modules.Sum(m => (m["features"] as JArray)?.Count ?? 0);
    var enabledFeatures = modules.Sum(m => (m["features"] as JArray)?.Count(f => f["isEnabled"]?.Value<bool>() == true) ?? 0);

    sb.AppendLine("  @features {");
    sb.AppendLine($"    enabled: {enabledFeatures}/{totalFeatures}");
    sb.AppendLine($"    modules: {modules.Count}");

    var coreModules = modules.Where(m => m["isCore"]?.Value<bool>() == true).ToList();
    var optionalModules = modules.Where(m => m["isCore"]?.Value<bool>() != true).ToList();

    sb.AppendLine($"    coreModules: [{string.Join(", ", coreModules.Select(m => $"\"{m["name"]}\""))}]");

    var enabledOptional = optionalModules
        .Where(m => (m["features"] as JArray)?.Any(f => f["isEnabled"]?.Value<bool>() == true) == true)
        .Select(m => m["name"]?.ToString())
        .ToList();
    sb.AppendLine($"    enabledOptionalModules: [{string.Join(", ", enabledOptional.Select(n => $"\"{n}\""))}]");
    sb.AppendLine("  }");
    sb.AppendLine();

    // Settings summary
    var settingCategories = settings.GroupBy(s => s["category"]?.ToString() ?? "General")
        .Select(g => g.Key)
        .ToList();
    sb.AppendLine("  @settings {");
    sb.AppendLine($"    count: {settings.Count}");
    sb.AppendLine($"    categories: [{string.Join(", ", settingCategories.Select(c => $"\"{c}\""))}]");
    sb.AppendLine("  }");

    sb.AppendLine("}");

    return sb.ToString();
});

server.RegisterTool("InvalidateTenantCache", async (args) =>
{
    var tenantId = args["tenantId"]?.Value<int>() ?? throw new ArgumentException("tenantId required");
    var reason = args["reason"]?.Value<string>() ?? "Cache invalidation via MCP";

    var payload = new JObject
    {
        ["reason"] = reason
    };

    var response = await server.Http.PostAsync(
        $"/api/tenants/{tenantId}/features/InvalidateTenantCache",
        new StringContent(payload.ToString(), Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to invalidate cache: {response.StatusCode} - {error}");
    }

    return $"✅ Cache invalidated for tenant {tenantId}. Reason: {reason}";
});

// Start Server
await server.RunAsync();
