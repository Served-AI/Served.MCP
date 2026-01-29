---
type: mcp-tool
name: Projects
version: 2026.1.2
domain: projects
tags: [mcp, projects, project-management, crud]
description: Project management tools for creating, updating, and organizing projects.
---

# Projects MCP Tools

Manage projects via MCP.

---

## GetProjects

List all projects for a workspace.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| customerId | int | No | - | Filter by customer |
| status | string | No | - | Filter by status |

### Response

```json
{
  "success": true,
  "count": 5,
  "projects": [
    {
      "id": 101,
      "name": "Mobile App",
      "code": "P-001",
      "status": "Active",
      "progress": 45,
      "customerId": 50,
      "customerName": "Acme Corp"
    }
  ]
}
```

### Example

```
GetProjects(tenantId: 1)
```

---

## GetProjectDetails

Get detailed project information.

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
    "code": "P-001",
    "description": "iOS and Android app development",
    "status": "Active",
    "progress": 45,
    "startDate": "2026-01-01",
    "endDate": "2026-06-30",
    "plannedHours": 500,
    "actualHours": 225,
    "budget": 100000,
    "customerId": 50,
    "customerName": "Acme Corp",
    "projectManagerId": 10,
    "projectManagerName": "John Doe"
  }
}
```

### Example

```
GetProjectDetails(tenantId: 1, projectId: 101)
```

---

## CreateProject

Create a new project.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| name | string | Yes | - | Project name |
| parentId | int | No | null | Parent project ID (for sub-projects) |
| customerId | int | No | null | Customer ID |
| description | string | No | null | Description |
| startDate | date | No | null | Start date |
| endDate | date | No | null | End date |
| budget | float | No | null | Budget amount |
| categoryId | int | No | null | Project category ID |
| isBillable | bool | No | true | Is billable |

### Example - Simple Project

```
CreateProject(
  tenantId: 1,
  name: "Website Redesign",
  description: "Redesign company website",
  startDate: "2026-02-01",
  endDate: "2026-04-30"
)
```

### Example - Sub-Project

```
CreateProject(
  tenantId: 1,
  name: "Phase 1 - Design",
  parentId: 101,
  startDate: "2026-02-01",
  endDate: "2026-02-28"
)
```

### Response

```json
{
  "success": true,
  "message": "Project created successfully",
  "projectId": 102
}
```

---

## UpdateProject

Update an existing project.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| projectId | int | Yes | - | Project ID |
| name | string | No | - | New name |
| parentId | int | No | - | New parent project ID |
| description | string | No | - | New description |
| status | string | No | - | New status |
| startDate | date | No | - | New start date |
| endDate | date | No | - | New end date |
| budget | float | No | - | New budget |

### Example - Update Status

```
UpdateProject(tenantId: 1, projectId: 101, status: "Completed")
```

### Example - Move to Sub-Project

```
UpdateProject(tenantId: 1, projectId: 102, parentId: 101)
```

### Response

```json
{
  "success": true,
  "message": "Project 101 updated successfully"
}
```

---

## DeleteProject

Delete a project.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| projectId | int | Yes | - | Project ID |

### Example

```
DeleteProject(tenantId: 1, projectId: 102)
```

### Warning

Deleting a project may affect linked tasks, time entries, and invoices.

---

## UpdateProjectsBulk

Update multiple projects at once. **Requires user confirmation.**

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| updatesJson | string | Yes | - | JSON array of updates |

### Update JSON Format

```json
[
  {
    "projectId": 101,
    "status": "Completed"
  },
  {
    "projectId": 102,
    "endDate": "2026-03-31"
  }
]
```

---

## ExecuteUpdateProjectsBulk

Execute bulk update after user confirmation.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| updatesJson | string | Yes | - | Same JSON as UpdateProjectsBulk |

---

## Workflows

### Create Project with Tasks

```
1. CreateProject(name: "New App") → projectId: 101
2. CreateTask(projectId: 101, name: "Design")
3. CreateTask(projectId: 101, name: "Development")
4. CreateTask(projectId: 101, name: "Testing")
```

### Project Hierarchy

```
1. CreateProject(name: "Enterprise App") → parentId: 100
2. CreateProject(name: "Phase 1", parentId: 100) → 101
3. CreateProject(name: "Phase 2", parentId: 100) → 102
4. CreateTask(projectId: 101, name: "Design")
5. CreateTask(projectId: 102, name: "Development")
```

### Bulk Status Update

```
1. UpdateProjectsBulk(tenantId, updatesJson) → Preview
2. User confirms: "Yes"
3. ExecuteUpdateProjectsBulk(tenantId, updatesJson)
```

---

## Errors

| Error | Cause | Solution |
|-------|-------|----------|
| Project not found | Invalid ID | Use GetProjects to find valid IDs |
| Circular reference | Invalid parent | Check project hierarchy |
| Customer not found | Invalid customer ID | Use GetCustomers to find ID |

---

## Hints

- Use `parentId` to create sub-projects
- `GetProjectDetails` includes budget and hours info
- Bulk operations require user confirmation
- Status values depend on workspace configuration

---

*Last Updated: 2026-01-17*
