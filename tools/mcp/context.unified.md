---
type: mcp-tool
name: Context
version: 2026.1.2
domain: context
tags: [mcp, context, user, tenant, workspace]
description: Context tools for getting user profile, workspace settings, and project information.
---

# Context MCP Tools

Get user profile, workspace settings, and project context. **Call these first.**

---

## GetUserContext

Get user profile and available workspaces. **Call this first to get tenantId.**

### Parameters

None.

### Response

```json
{
  "success": true,
  "user": {
    "id": 123,
    "email": "user@example.com",
    "name": "John Doe"
  },
  "workspaces": [
    {
      "tenantId": 1,
      "name": "My Company",
      "slug": "my-company",
      "role": "Admin"
    }
  ],
  "hint": "Use tenantId from workspace for subsequent calls."
}
```

### Example

```
GetUserContext()
```

---

## GetTenantContext

Get detailed workspace information including settings, categories, and custom fields.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |

### Response

```json
{
  "success": true,
  "tenant": {
    "id": 1,
    "name": "My Company",
    "slug": "my-company",
    "settings": {
      "currency": "DKK",
      "timezone": "Europe/Copenhagen",
      "language": "da"
    }
  },
  "projectCategories": [
    { "id": 1, "name": "Development" },
    { "id": 2, "name": "Design" }
  ],
  "taskStatuses": [
    { "id": 1, "name": "Not Started" },
    { "id": 2, "name": "In Progress" },
    { "id": 3, "name": "Completed" }
  ],
  "customFields": [
    { "id": 1, "name": "Department", "type": "dropdown" }
  ]
}
```

### Example

```
GetTenantContext(tenantId: 1)
```

---

## GetProjectContext

Get project with tasks, team, and recent activity.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| projectId | int | Yes | - | Project ID |

### Response

```json
{
  "success": true,
  "project": {
    "id": 101,
    "name": "Mobile App",
    "description": "iOS and Android app development",
    "status": "Active",
    "progress": 45,
    "startDate": "2026-01-01",
    "endDate": "2026-06-30"
  },
  "tasks": {
    "total": 24,
    "completed": 8,
    "inProgress": 6,
    "notStarted": 10
  },
  "team": [
    { "id": 1, "name": "John Doe", "role": "Lead" }
  ],
  "recentActivity": [
    { "type": "task_updated", "description": "Task completed", "timestamp": "2026-01-17T10:30:00Z" }
  ]
}
```

### Example

```
GetProjectContext(tenantId: 1, projectId: 101)
```

---

## Workflows

### Initial Session Setup

```
1. GetUserContext() → Get tenantId from workspaces
2. GetTenantContext(tenantId) → Get workspace settings
3. GetProjects(tenantId) → List projects
4. GetProjectContext(tenantId, projectId) → Get project details
```

### Switch Workspace

```
1. GetUserContext() → List all workspaces
2. Select new tenantId
3. GetTenantContext(newTenantId) → Load new workspace
```

---

## Hints

- Always call `GetUserContext` first to get `tenantId`
- `GetTenantContext` provides custom field definitions
- `GetProjectContext` is useful for quick project overview
- Workspace slug can be used for URL construction

---

*Last Updated: 2026-01-17*
