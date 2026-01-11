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

// Start Server
await server.RunAsync();
