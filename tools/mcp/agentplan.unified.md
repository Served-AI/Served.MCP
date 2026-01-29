---
type: mcp-tool
name: AgentPlan
version: 2026.1.2
domain: agents
tags: [mcp, agents, todowrite, plan, tracking, ai]
description: TodoWrite-style plan tracking for AI agents. Enables agents to track their work and progress.
---

# AgentPlan MCP Tools

TodoWrite-style plan tracking for AI agents.

---

## AgentPlanGet

Get current plan/todos for the active agent session.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| agentId | string | No | current | Agent ID (default: current session) |

### Response

```json
{
  "success": true,
  "agentId": "claude-session-123",
  "taskId": 1234,
  "todos": [
    {
      "index": 0,
      "content": "Research existing patterns",
      "status": "completed"
    },
    {
      "index": 1,
      "content": "Implement service layer",
      "status": "in_progress"
    },
    {
      "index": 2,
      "content": "Add unit tests",
      "status": "pending"
    }
  ],
  "progress": {
    "completed": 1,
    "total": 3,
    "percentage": 33
  }
}
```

### Example

```
AgentPlanGet()
```

---

## AgentPlanAdd

Add a new todo item to the plan.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| content | string | Yes | - | Todo description |
| status | string | No | pending | Initial status |

### Status Values

| Status | Description |
|--------|-------------|
| `pending` | Not started |
| `in_progress` | Currently working |
| `completed` | Done |
| `skipped` | Skipped |

### Response

```json
{
  "success": true,
  "todo": {
    "index": 3,
    "content": "Implement login endpoint",
    "status": "pending"
  },
  "totalTodos": 4
}
```

### Example

```
AgentPlanAdd(content: "Implement login endpoint")
```

---

## AgentPlanUpdate

Update status on an existing todo item.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| index | int | Yes | - | Todo index |
| status | string | Yes | - | New status |

### Response

```json
{
  "success": true,
  "todo": {
    "index": 1,
    "content": "Implement service layer",
    "status": "completed"
  },
  "progress": {
    "completed": 2,
    "total": 3,
    "percentage": 67
  }
}
```

### Example

```
AgentPlanUpdate(index: 1, status: "completed")
```

---

## Workflows

### Typical Agent Workflow

```
1. AgentPlanAdd("Research existing patterns")
2. AgentPlanAdd("Implement service layer")
3. AgentPlanAdd("Add unit tests")
4. AgentPlanUpdate(0, "in_progress")
5. ... work ...
6. AgentPlanUpdate(0, "completed")
7. AgentPlanUpdate(1, "in_progress")
8. ... work ...
9. AgentPlanGet() -> Check progress
```

### AI Agent Session

```
AI: [Start session for task #1234]
AI: [AgentPlanAdd("Analyze codebase")]
AI: [AgentPlanAdd("Implement feature")]
AI: [AgentPlanAdd("Write tests")]
AI: [AgentPlanUpdate(0, "in_progress")]

AI: "Analyzing codebase..."
    [Perform analysis]

AI: [AgentPlanUpdate(0, "completed")]
AI: [AgentPlanUpdate(1, "in_progress")]

AI: "Analysis complete. Implementing feature..."
```

---

## CLI Equivalent

| MCP Tool | CLI Command |
|----------|-------------|
| AgentPlanGet | `served atlas plan` |
| AgentPlanAdd | `served atlas plan add "item"` |
| AgentPlanUpdate | `served atlas plan status 0 completed` |

---

## Hints

- Use AgentPlanGet to check progress during long tasks
- Mark todos as `in_progress` before starting work
- Use `skipped` for items that become irrelevant
- Plan todos sync with linked Served tasks

---

*Last Updated: 2026-01-17*
