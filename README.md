# ServedMCP

AI skills og tools dokumentation for Served platformen.

## Struktur

```
ServedMCP/
├── skills/                    # AI-specifikke instruktioner
│   ├── claude.md              # Claude API reference
│   └── mcp.md                 # MCP tools guide
├── tools/
│   ├── api/                   # REST API tools
│   │   ├── customer.md
│   │   ├── project.md
│   │   ├── task.md
│   │   ├── agreement.md
│   │   ├── timeregistration.md
│   │   └── finance.md
│   └── mcp/                   # MCP Server tools
│       └── README.md
└── README.md
```

## Skills

Skills er instruktionsfiler der giver AI'er kontekst og viden om Served.

| Fil | Beskrivelse |
|-----|-------------|
| [skills/claude.md](skills/claude.md) | Komplet Served API reference (500+ endpoints) |
| [skills/mcp.md](skills/mcp.md) | MCP tools guide med workflows og best practices |

## Tools

### REST API Tools

Dokumentation for direkte API kald:

| Tool | Beskrivelse |
|------|-------------|
| [customer.md](tools/api/customer.md) | Kunder - CRUD, søgning |
| [project.md](tools/api/project.md) | Projekter - CRUD, budget, team |
| [task.md](tools/api/task.md) | Opgaver - CRUD, Gantt, progress |
| [agreement.md](tools/api/agreement.md) | Aftaler/bookinger |
| [timeregistration.md](tools/api/timeregistration.md) | Tidsregistrering, AI-forslag |
| [finance.md](tools/api/finance.md) | Fakturaer, billing, PDF |

### MCP Tools

Model Context Protocol tools til AI assistenter:

| Dokument | Beskrivelse |
|----------|-------------|
| [tools/mcp/README.md](tools/mcp/README.md) | Komplet MCP tools reference |

MCP Tools kategorier:
- **Context** - GetUserContext
- **Projects** - GetProjects, CreateProject, UpdateProject
- **Tasks** - GetTasks, CreateTask, UpdateTask, Bulk operations
- **Customers** - GetCustomers, CreateCustomer, UpdateCustomer
- **Agreements** - GetAgreements, CreateAgreement
- **Time Tracking** - SuggestTimeEntries, AnalyzeTimePatterns
- **Intelligence** - AnalyzeProjectHealth, SuggestTaskDecomposition, EstimateEffort, FindSimilarProjects
- **Employees** - GetEmployees

## Brug

### Claude Code

Referer i CLAUDE.md:

```markdown
Se også: /path/to/ServedMCP/skills/claude.md
Se også: /path/to/ServedMCP/skills/mcp.md
```

### Claude.ai MCP Integration

```json
{
  "mcpServers": {
    "served": {
      "url": "https://app.served.dk/mcp",
      "auth": {
        "type": "oauth",
        "clientId": "claude-mcp"
      }
    }
  }
}
```

## API Miljøer

| Miljø | URL |
|-------|-----|
| Production | `https://app.served.dk` |
| MCP Server | `https://app.served.dk/mcp` |
| Local Dev | `http://localhost:5010` |
