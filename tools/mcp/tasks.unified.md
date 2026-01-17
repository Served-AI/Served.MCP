---
type: mcp-tool
name: Tasks
version: 2026.1.2
domain: tasks
tags: [mcp, tasks, project-management, crud]
description: Task management tools for creating, updating, and organizing project tasks.
---

# Tasks MCP Tools

Manage project tasks via MCP.

---

## GetTasks

Get tasks for a project.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| projectId | int | Yes | - | Project ID |

### Response

```json
{
  "success": true,
  "count": 8,
  "tasks": [
    {
      "id": 201,
      "name": "Design mockups",
      "taskNo": "T-001",
      "progress": 75,
      "priority": "High",
      "startDate": "2026-01-15",
      "endDate": "2026-01-31"
    }
  ]
}
```

### Example

```
GetTasks(tenantId: 1, projectId: 101)
```

---

## GetTaskDetails

Get detailed information about a task.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| taskId | int | Yes | - | Task ID |

### Response

```json
{
  "success": true,
  "task": {
    "id": 201,
    "name": "Design mockups",
    "description": "Create wireframes and mockups for all pages",
    "status": 2,
    "progress": 75,
    "priority": "High",
    "startDate": "2026-01-15",
    "endDate": "2026-01-31",
    "plannedEffort": 40,
    "actualEffort": 30
  }
}
```

### Example

```
GetTaskDetails(tenantId: 1, taskId: 201)
```

---

## CreateTask

Create a new task.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| projectId | int | Yes | - | Project ID |
| name | string | Yes | - | Task name |
| parentTaskId | int | No | null | Parent task ID (for subtasks) |
| description | string | No | null | Description |
| startDate | date | No | null | Start date (YYYY-MM-DD) |
| endDate | date | No | null | End date (YYYY-MM-DD) |
| priority | int | No | 3 | Priority (1-5) |
| isBillable | bool | No | true | Is billable |

### Priority Values

| Value | Meaning |
|-------|---------|
| 1 | Critical |
| 2 | High |
| 3 | Normal (default) |
| 4 | Low |
| 5 | Very Low |

### Example - Simple Task

```
CreateTask(
  tenantId: 1,
  projectId: 101,
  name: "Implement login",
  description: "Implement user login with JWT",
  startDate: "2026-02-01",
  endDate: "2026-02-07",
  priority: 2
)
```

### Example - Subtask

```
CreateTask(
  tenantId: 1,
  projectId: 101,
  name: "Design login form",
  parentTaskId: 201,
  priority: 2
)
```

### Response

```json
{
  "success": true,
  "message": "Task created successfully",
  "taskId": 204
}
```

---

## UpdateTask

Update an existing task.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| taskId | int | Yes | - | Task ID |
| name | string | No | - | New name |
| parentTaskId | int | No | - | New parent task ID |
| description | string | No | - | New description |
| progress | int | No | - | Progress (0-100) |
| priority | int | No | - | New priority |
| startDate | date | No | - | New start date |
| endDate | date | No | - | New end date |

### Example - Update Progress

```
UpdateTask(tenantId: 1, taskId: 201, progress: 100)
```

### Example - Move to Subtask

```
UpdateTask(tenantId: 1, taskId: 205, parentTaskId: 201)
```

### Example - Move to Top-Level

```
UpdateTask(tenantId: 1, taskId: 205, parentTaskId: null)
```

### Response

```json
{
  "success": true,
  "message": "Task 201 updated successfully"
}
```

---

## DeleteTask

Delete a task.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| taskId | int | Yes | - | Task ID |

### Example

```
DeleteTask(tenantId: 1, taskId: 205)
```

### Response

```json
{
  "success": true,
  "message": "Task 205 deleted successfully"
}
```

### Warning

Deleting a task may affect linked subtasks and time entries.

---

## CreateTasksBulk

Create multiple tasks at once. **Requires user confirmation.**

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| tasksJson | string | Yes | - | JSON array of tasks |

### Task JSON Format

```json
[
  {
    "name": "Task 1",
    "projectId": 101,
    "description": "Description",
    "priority": 2,
    "parentTaskId": null,
    "startDate": "2026-02-01",
    "endDate": "2026-02-15"
  }
]
```

### Response (Confirmation Request)

```json
{
  "action": "create_tasks_bulk",
  "count": 3,
  "tasks": [...],
  "requiresConfirmation": true,
  "confirmationMessage": "Create 3 tasks?"
}
```

---

## ExecuteCreateTasksBulk

Execute bulk creation after user confirmation.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| tasksJson | string | Yes | - | Same JSON as CreateTasksBulk |

### Response

```json
{
  "success": true,
  "created": 3,
  "total": 3,
  "tasks": [
    { "id": 205, "name": "Setup project" },
    { "id": 206, "name": "Design database" },
    { "id": 207, "name": "Implement API" }
  ]
}
```

---

## UpdateTasksBulk

Update multiple tasks at once. **Requires user confirmation.**

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| updatesJson | string | Yes | - | JSON array of updates |

### Update JSON Format

```json
[
  {
    "taskId": 201,
    "progress": 100
  },
  {
    "taskId": 202,
    "progress": 50,
    "endDate": "2026-03-15"
  }
]
```

---

## ExecuteUpdateTasksBulk

Execute bulk update after user confirmation.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| updatesJson | string | Yes | - | Same JSON as UpdateTasksBulk |

---

## Workflows

### Hierarchical Task Structure

```
1. CreateTask(projectId: 101, name: "Login feature") → taskId: 210
2. CreateTask(projectId: 101, name: "Design UI", parentTaskId: 210)
3. CreateTask(projectId: 101, name: "Frontend", parentTaskId: 210)
4. CreateTask(projectId: 101, name: "Backend API", parentTaskId: 210)
```

### Bulk Task Creation

```
1. CreateTasksBulk(tenantId, tasksJson) → Preview
2. User confirms: "Yes"
3. ExecuteCreateTasksBulk(tenantId, tasksJson)
```

**Important:** Never call ExecuteCreateTasksBulk without user confirmation.

---

## Errors

| Error | Cause | Solution |
|-------|-------|----------|
| Task not found | Invalid ID | Use GetTasks to find valid IDs |
| No tasks in project | Empty project | Create tasks first |
| Circular reference | Invalid parent | Check task hierarchy |
| Project not found | Invalid project ID | Use GetProjects to find ID |

---

## Hints

- Use `GetTaskDetails` for full task information
- `parentTaskId: null` moves task to top-level
- Priority 1-5 (1=Critical, 5=Very Low)
- Bulk operations require user confirmation

---

*Last Updated: 2026-01-17*
