# Unified File Format Specification

**Version:** 1.0.0

The Unified File Format provides a standardized way to document MCP tools, REST API endpoints, and skills across the Served platform. It supports three equivalent representations:

- **`unified.md`** - Human-readable markdown with YAML frontmatter
- **`unified.json`** - Machine-readable JSON
- **`unified.yaml`** - Machine-readable YAML

All three formats are semantically equivalent and can be converted between each other.

---

## Format Overview

### unified.md (Markdown)

```markdown
---
type: mcp-tool | api-endpoint | skill
name: ToolName
version: 1.0.0
domain: devops | tasks | projects | customers | calendar | finance
tags: [mcp, devops, ci-cd]
---

# Tool Name

Brief description.

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| param1 | string | Yes | - | Description |

## Response

\`\`\`json
{
  "success": true,
  "data": {}
}
\`\`\`

## Examples

### Basic Usage

\`\`\`
ToolName(param1: "value")
\`\`\`
```

### unified.json (JSON)

```json
{
  "$schema": "https://served.dk/schemas/unified-v1.json",
  "type": "mcp-tool",
  "name": "ToolName",
  "version": "1.0.0",
  "domain": "devops",
  "tags": ["mcp", "devops", "ci-cd"],
  "description": "Brief description.",
  "parameters": [
    {
      "name": "param1",
      "type": "string",
      "required": true,
      "description": "Description"
    }
  ],
  "response": {
    "type": "object",
    "properties": {
      "success": { "type": "boolean" },
      "data": { "type": "object" }
    }
  },
  "examples": [
    {
      "name": "Basic Usage",
      "request": { "param1": "value" }
    }
  ]
}
```

### unified.yaml (YAML)

```yaml
$schema: https://served.dk/schemas/unified-v1.json
type: mcp-tool
name: ToolName
version: 1.0.0
domain: devops
tags:
  - mcp
  - devops
  - ci-cd

description: Brief description.

parameters:
  - name: param1
    type: string
    required: true
    description: Description

response:
  type: object
  properties:
    success:
      type: boolean
    data:
      type: object

examples:
  - name: Basic Usage
    request:
      param1: value
```

---

## Document Types

### mcp-tool

Documentation for MCP Server tools.

**Required fields:**
- `type: mcp-tool`
- `name` - Tool function name (PascalCase)
- `domain` - Domain category
- `parameters` - List of parameters
- `description`

**Optional fields:**
- `version`
- `tags`
- `response`
- `examples`
- `workflows`
- `errors`
- `hints`

### api-endpoint

Documentation for REST API endpoints.

**Required fields:**
- `type: api-endpoint`
- `name` - Endpoint description
- `method` - HTTP method (GET, POST, PUT, DELETE, PATCH)
- `path` - URL path
- `domain` - API module

**Optional fields:**
- `version`
- `tags`
- `parameters` (path, query, body)
- `response`
- `examples`
- `authentication`
- `permissions`

### skill

Documentation for Claude skills.

**Required fields:**
- `type: skill`
- `name` - Skill name
- `description` - Brief description

**Optional fields:**
- `version`
- `tags`
- `commands`
- `workflows`
- `related_skills`

---

## Frontmatter Schema

```yaml
# Core fields (all types)
type: mcp-tool | api-endpoint | skill
name: string
version: string  # SemVer or CalVer
domain: string
tags: string[]
description: string

# Tool-specific
parameters:
  - name: string
    type: string | int | bool | object | array
    required: boolean
    default: any
    description: string
    enum: string[]  # For constrained values

# Response
response:
  type: string
  properties: object
  example: object

# Examples
examples:
  - name: string
    description: string
    request: object
    response: object

# Workflows
workflows:
  - name: string
    steps: string[]

# Errors
errors:
  - code: string
    message: string
    solution: string

# Hints (AI guidance)
hints:
  - context: string
    suggestion: string
```

---

## Domain Categories

| Domain | Description |
|--------|-------------|
| `context` | User/tenant context |
| `tasks` | Task management |
| `projects` | Project management |
| `customers` | Customer management |
| `calendar` | Agreements/meetings |
| `finance` | Invoicing/billing |
| `timetracking` | Time registration |
| `devops` | Git/CI/CD |
| `intelligence` | AI features |
| `files` | File operations |
| `canvas` | Canvas/whiteboard |
| `agents` | Agent coordination |

---

## Parameter Types

| Type | JSON | Description |
|------|------|-------------|
| `string` | `"string"` | Text value |
| `int` | `123` | Integer |
| `long` | `123456789` | Long integer |
| `bool` | `true/false` | Boolean |
| `float` | `1.5` | Decimal |
| `date` | `"2026-01-17"` | ISO date |
| `datetime` | `"2026-01-17T10:30:00Z"` | ISO datetime |
| `object` | `{}` | JSON object |
| `array` | `[]` | JSON array |

---

## File Naming Convention

```
tools/
├── mcp/
│   ├── tasks.unified.md       # MCP tool docs
│   ├── projects.unified.md
│   └── devops.unified.md
├── api/
│   ├── tasks.unified.md       # REST API docs
│   └── projects.unified.md
└── skills/
    ├── served.unified.md      # Skill docs
    └── devops.unified.md
```

Or use subdirectories with index files:

```
tools/
├── mcp/
│   ├── tasks/
│   │   ├── index.unified.md
│   │   └── schema.unified.json
│   └── devops/
│       ├── index.unified.md
│       └── schema.unified.json
```

---

## Conversion Between Formats

### MD → JSON

```javascript
const matter = require('gray-matter');
const md = fs.readFileSync('tool.unified.md', 'utf8');
const { data, content } = matter(md);
const json = { ...data, content };
```

### JSON → MD

```javascript
const json = require('./tool.unified.json');
const { content, ...frontmatter } = json;
const md = `---\n${yaml.dump(frontmatter)}---\n\n${content}`;
```

### CLI Conversion

```bash
served unified convert tool.unified.md --to json
served unified convert tool.unified.json --to yaml
served unified convert tool.unified.yaml --to md
```

---

## Validation

```bash
# Validate unified files
served unified validate tools/mcp/tasks.unified.md
served unified validate tools/mcp/*.unified.md

# Validate schema
served unified schema --check
```

---

## Example: Complete MCP Tool

### tasks.unified.md

```markdown
---
type: mcp-tool
name: GetTasks
version: 2026.1.2
domain: tasks
tags: [mcp, tasks, project-management]
---

# GetTasks

Get tasks for a project.

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| projectId | int | Yes | - | Project ID |

## Response

```json
{
  "success": true,
  "count": 8,
  "tasks": [
    {
      "id": 201,
      "name": "Design mockups",
      "progress": 75,
      "priority": "High"
    }
  ]
}
```

## Examples

### Get all tasks for project

```
GetTasks(tenantId: 1, projectId: 101)
```

## Hints

- Use `GetTaskDetails` for full task information
- Tasks can be filtered by status using query parameters
```

---

## Benefits

1. **Human-readable** - Markdown with frontmatter is easy to read/write
2. **Machine-parseable** - JSON/YAML for tooling and automation
3. **Consistent** - Same schema across all documentation
4. **Searchable** - Tags and domains enable efficient lookup
5. **Versionable** - Version field tracks changes
6. **Convertible** - Formats are interchangeable

---

## Related Documentation

- `/docs/reference/OBSIDIAN-TAGS-GUIDE.md` - Tag conventions
- `/docs/api/guides/API_ENDPOINTS.md` - API documentation
- `/.claude/skills/` - Skill documentation

---

*Last Updated: 2026-01-17*
