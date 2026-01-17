---
type: mcp-tool
name: Canvas
version: 2026.1.2
domain: canvas
tags: [mcp, canvas, visualization, obsidian, nodes, edges]
description: Obsidian-style infinite canvas integration for visual organization of information.
---

# Canvas MCP Tools

Obsidian-style infinite canvas integration. Organize information visually.

---

## GetCanvasList

Get list of canvases in a workspace.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |

### Response

```json
{
  "success": true,
  "total": 2,
  "canvases": [
    {
      "id": 42,
      "name": "Project Architecture",
      "description": "System overview",
      "nodeCount": 15,
      "edgeCount": 8,
      "createdAt": "2026-01-15T10:00:00Z",
      "updatedAt": "2026-01-20T14:30:00Z"
    }
  ]
}
```

### Example

```
GetCanvasList(tenantId: 1)
```

---

## GetCanvasDetail

Get full canvas with all nodes and edges.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| canvasId | int | Yes | - | Canvas ID |

### Response

```json
{
  "success": true,
  "canvas": {
    "id": 42,
    "name": "Project Architecture",
    "description": "System overview",
    "storage": "Database"
  },
  "nodes": [
    {
      "id": "a1b2c3d4",
      "type": "text",
      "content": "Authentication flow overview...",
      "x": 100,
      "y": 200,
      "width": 300,
      "height": 150
    },
    {
      "id": "e5f6g7h8",
      "type": "entity",
      "entityType": "Task",
      "entityId": 1234,
      "content": "Task #1234: Implement auth",
      "x": 500,
      "y": 200,
      "width": 250,
      "height": 100
    }
  ],
  "edges": [
    {
      "id": "edge-1",
      "fromNode": "a1b2c3d4",
      "toNode": "e5f6g7h8",
      "label": "implements"
    }
  ]
}
```

---

## Node Types

| Type | Description |
|------|-------------|
| `text` | Free-text note |
| `file` | File reference |
| `link` | URL link |
| `group` | Group container |
| `entity` | Served entity (Task, Project, etc.) |

---

## CreateCanvas

Create a new canvas.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| name | string | Yes | - | Canvas name |
| description | string | No | null | Description |

### Response

```json
{
  "success": true,
  "canvas": {
    "id": 44,
    "name": "Sprint 42 Planning",
    "description": "Q1 sprint planning board",
    "createdAt": "2026-01-20T15:00:00Z"
  }
}
```

### Example

```
CreateCanvas(
  tenantId: 1,
  name: "Sprint 42 Planning",
  description: "Q1 sprint planning board"
)
```

---

## AddCanvasNode

Add node to canvas.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| canvasId | int | Yes | - | Canvas ID |
| type | string | Yes | - | Node type |
| content | string | Yes | - | Node content |
| entityId | int | No | null | Entity ID (for entity type) |
| x | int | No | auto | X position |
| y | int | No | auto | Y position |
| width | int | No | auto | Width |
| height | int | No | auto | Height |

### Example - Text Node

```
AddCanvasNode(
  canvasId: 42,
  type: "text",
  content: "Implementation notes for the auth system..."
)
```

### Example - Entity Node

```
AddCanvasNode(
  canvasId: 42,
  type: "entity",
  content: "Task",
  entityId: 1234
)
```

### Example - Link Node

```
AddCanvasNode(
  canvasId: 42,
  type: "link",
  content: "https://docs.example.com/auth"
)
```

### Response

```json
{
  "success": true,
  "node": {
    "id": "new-node-id",
    "type": "text",
    "content": "Implementation notes...",
    "x": 700,
    "y": 300,
    "width": 300,
    "height": 150
  }
}
```

---

## SaveContextToCanvas

Save current agent context to canvas. Creates nodes for todos, active files, and git state.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| canvasId | int | Yes | - | Canvas ID |
| includeFiles | bool | No | true | Include active files |
| includeTodos | bool | No | true | Include todos |
| includeGitState | bool | No | true | Include git state |

### Response

```json
{
  "success": true,
  "nodesCreated": 5,
  "nodes": [
    {"id": "todo-group", "type": "group", "content": "Agent Todos"},
    {"id": "todo-1", "type": "text", "content": "Research patterns"},
    {"id": "todo-2", "type": "text", "content": "Implement service"},
    {"id": "files-group", "type": "group", "content": "Active Files"},
    {"id": "git-node", "type": "text", "content": "Branch: feature/auth (2 uncommitted)"}
  ]
}
```

---

## Workflows

### Create Canvas for Sprint

```
1. CreateCanvas(tenantId, "Sprint 42")
2. AddCanvasNode(canvasId, "group", "Backlog")
3. AddCanvasNode(canvasId, "entity", entityId: task1)
4. AddCanvasNode(canvasId, "entity", entityId: task2)
5. AddCanvasNode(canvasId, "text", "Sprint goals: ...")
```

### Agent Context Sync

```
1. Agent works on tasks via AgentPlan tools
2. At milestone: SaveContextToCanvas(canvasId)
3. User can review progress visually in Canvas
```

---

## CLI Equivalent

| MCP Tool | CLI Command |
|----------|-------------|
| GetCanvasList | `served canvas list 1` |
| GetCanvasDetail | `served canvas get 42` |
| CreateCanvas | `served canvas create 1 "Name"` |
| AddCanvasNode | `served canvas add-node 42 text` |
| SaveContextToCanvas | `served atlas sync` |

---

## Export

Canvases can be exported to Obsidian JSON Canvas format:

```bash
served canvas export 42 --output sprint.canvas
```

Format compatible with Obsidian Canvas plugin.

---

## Hints

- Use groups to organize related nodes
- Link Served entities for task/project visibility
- SaveContextToCanvas captures agent work state
- Canvas format is Obsidian-compatible

---

*Last Updated: 2026-01-17*
