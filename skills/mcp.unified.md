---
type: skill
name: ServedMCP
version: 2026.1.2
domain: context
tags: [skill, mcp, ai, tools, workflow]
description: Claude skill for using Served MCP tools effectively - workflows, best practices, and tool reference.
---

# Served MCP Skills

Guide for AI assistants to use Served MCP tools effectively.

---

## Overview

Served MCP Server exposes tools for:
- **Projects** - Create, read, update projects
- **Tasks** - Handle tasks with bulk operations
- **Customers** - Customer management
- **Agreements** - Calendar and bookings
- **Time Tracking** - AI-powered time suggestions
- **Project Intelligence** - AI analysis tools

---

## Quick Start

### 1. Always Start with GetUserContext

```
Call GetUserContext FIRST to:
- Identify the user
- Find available workspaces
- Get the correct tenantId
```

### 2. Get Workspace Context with GetTenantContext

```
Call GetTenantContext(tenantId) to understand the workspace:
- Categories (project types, statuses, task states, priorities)
- Custom field definitions
- AI models available
- Statistics
```

### 3. Use tenantId in All Subsequent Calls

All MCP tools require `tenantId` parameter. Use the value from GetUserContext.

### 4. Work with @entity[id] References

Tools return data in format:
```
@project[101] { name: "...", progress: 45% }
@task[201] { name: "...", priority: "High" }
@customer[301] { name: "...", email: "..." }
```

---

## Workflow Examples

### Create Project with Tasks

```
1. GetUserContext -> Get tenantId
2. GetTenantContext(tenantId) -> Understand workspace
3. CreateProject(tenantId, "New Project", ...) -> Get projectId
4. CreateTask(tenantId, projectId, "Task 1", ...)
5. CreateTask(tenantId, projectId, "Task 2", ...)
```

### Analyze Project Status

```
1. GetUserContext -> Get tenantId
2. GetTenantContext(tenantId) -> Understand categories
3. GetProjects(tenantId) -> Find relevant projects
4. AnalyzeProjectHealth(tenantId, projectId) -> Get health report
5. GetTasks(tenantId, projectId) -> See task status
```

### Help with Time Registration

```
1. GetUserContext -> Get tenantId
2. SuggestTimeEntries(tenantId, startDate, endDate) -> Get AI suggestions
3. Present suggestions to user
4. (User accepts/rejects in UI)
```

### Plan New Project

```
1. GetUserContext -> Get tenantId
2. GetTenantContext(tenantId) -> See categories and custom fields
3. FindSimilarProjects(tenantId, "description") -> Learn from history
4. SuggestTaskDecomposition(tenantId, "feature", "type") -> Get task suggestions
5. EstimateEffort(tenantId, "description", "features") -> Get estimate
6. CreateProject(...) and CreateTasksBulk(...) -> Create with correct IDs
```

---

## Best Practices

### Always Verify Workspace

```
Correct:
1. GetUserContext
2. "You have access to workspace 'Acme Corp' (tenantId: 1). Continue?"
3. [After confirmation] GetTenantContext(1)
4. GetProjects(1)

Wrong:
1. GetProjects(1)  // Assumes tenantId without verification
```

### Bulk Operations Require Confirmation

```
Correct:
1. CreateTasksBulk returns confirmation request
2. Show task list to user
3. [After confirmation] ExecuteCreateTasksBulk

Wrong:
1. ExecuteCreateTasksBulk without CreateTasksBulk first
```

---

## Tool Reference

### Context Tools (Start Here!)

| Tool | Action | Parameters |
|------|--------|------------|
| GetUserContext | Get user and workspaces | - |
| GetTenantContext | Get workspace details | tenantId |

### Data Tools (CRUD)

| Tool | Action | Parameters |
|------|--------|------------|
| GetProjects | List projects | tenantId |
| CreateProject | Create project | tenantId, name, ... |
| GetTasks | List tasks | tenantId, projectId |
| CreateTask | Create task | tenantId, projectId, name, ... |
| CreateTasksBulk | Bulk create (confirm) | tenantId, tasksJson |
| ExecuteCreateTasksBulk | Execute bulk | tenantId, tasksJson |
| GetCustomers | List customers | tenantId |
| GetAgreements | List agreements | tenantId |
| GetEmployees | List team | tenantId, activeOnly |

### AI Tools (Intelligence)

| Tool | Purpose | Output |
|------|---------|--------|
| SuggestTimeEntries | AI time suggestions | Suggestions with confidence |
| AnalyzeTimePatterns | Pattern analysis | User work patterns |
| SuggestTaskDecomposition | Task breakdown | Suggestions from history |
| EstimateEffort | Estimation | Hours/days from data |
| AnalyzeProjectHealth | Health check | Score, risks, recommendations |
| FindSimilarProjects | Search projects | Similar projects with patterns |

---

## Priority Reference

### Task Priority

| Value | Meaning |
|-------|---------|
| 1 | Critical |
| 2 | High |
| 3 | Normal (default) |
| 4 | Low |
| 5 | Very low |

### Health Score

| Score | Status | Alert |
|-------|--------|-------|
| 80-100 | Healthy | Green |
| 60-79 | At Risk | Yellow |
| 0-59 | Critical | Red |

### Confidence Levels

| Score | Level |
|-------|-------|
| >= 0.8 | High confidence |
| 0.6-0.79 | Medium confidence |
| < 0.6 | Low confidence |

---

## Error Handling

| Error | Cause | Solution |
|-------|-------|----------|
| Not authenticated | Missing token | User must log in again |
| No access to workspace | Wrong tenantId | Use GetUserContext first |
| Project not found | Invalid projectId | Verify with GetProjects |
| Invalid date format | Wrong format | Use YYYY-MM-DD |

---

## MCP Configuration

```json
{
  "mcpServers": {
    "served": {
      "url": "https://app.served.dk/mcp",
      "auth": {
        "type": "oauth",
        "clientId": "claude-mcp",
        "scopes": ["projects", "tasks", "customers", "calendar", "timetracking"]
      }
    }
  }
}
```

### Available Scopes

| Scope | Access |
|-------|--------|
| projects | Projects (read/write) |
| tasks | Tasks (read/write) |
| customers | Customers (read/write) |
| calendar | Agreements (read/write) |
| timetracking | Time registration |
| employees | Team (read) |
| intelligence | AI tools |

---

## Tips for Effective Use

1. **Batch operations** - Use CreateTasksBulk instead of many CreateTask calls
2. **Cache user context** - Remember tenantId from GetUserContext
3. **Use AI tools** - They provide valuable insights from historical data
4. **Respect confirmations** - Bulk operations must be confirmed by user
5. **Use @entity references** - They make referencing specific objects easy

---

*Last Updated: 2026-01-17*
