# Served MCP Server

**Version:** 2026.1.2

MCP (Model Context Protocol) Server for AI assistants to interact with the Served platform. Enables Claude, GPT, and other AI models to access workspaces, projects, tasks, customers, and more.

## Features

- **40+ MCP Tools** - Full CRUD operations on Served entities
- **AI Intelligence** - Project health analysis, task decomposition, effort estimation
- **DevOps Integration** - Git repos, PRs, CI/CD pipelines
- **SDK Tracing** - OpenTelemetry observability via Served.SDK
- **Analytics** - Tool usage metrics and performance tracking

## Quick Links

| Document | Description |
|----------|-------------|
| [UNIFIED-FORMAT.md](UNIFIED-FORMAT.md) | Unified file format specification |
| [tools/mcp/](tools/mcp/) | MCP tool documentation (unified format) |
| [tools/api/](tools/api/) | REST API tool documentation |
| [skills/](skills/) | Skills documentation |
| [schemas/](schemas/) | JSON schemas |

---

## Unified File Format

All MCP documentation uses the **Unified File Format** for consistency and machine-readability.

### Supported Formats

| Format | Extension | Use Case |
|--------|-----------|----------|
| Markdown | `.unified.md` | Human-readable with YAML frontmatter |
| JSON | `.unified.json` | Machine-readable, API integration |
| YAML | `.unified.yaml` | Machine-readable, configuration |

### Structure

```markdown
---
type: mcp-tool
name: ToolName
version: 2026.1.2
domain: tasks
tags: [mcp, tasks, crud]
description: Brief description.
---

# ToolName

## Parameters
| Name | Type | Required | Description |
|------|------|----------|-------------|

## Response
\`\`\`json
{ "success": true }
\`\`\`

## Examples
...
```

### Schema

JSON Schema: `schemas/unified-v1.json`

See [UNIFIED-FORMAT.md](UNIFIED-FORMAT.md) for full specification.

---

## MCP Tools Oversigt

MCP Server: `https://app.served.dk/mcp`

### Context

| Tool | Beskrivelse |
|------|-------------|
| `GetUserContext` | Hent bruger profil og workspaces. **Kald denne først.** |
| `GetTenantContext` | Hent detaljeret tenant info (indstillinger, kategorier). |
| `GetProjectContext` | Hent projekt med tasks, team og recent activity. |

→ Se [tools/mcp/context.unified.md](tools/mcp/context.unified.md) for detaljer

### Agent Plan (TodoWrite-style)

| Tool | Beskrivelse |
|------|-------------|
| `AgentPlanGet` | Hent nuværende plan/todos for aktiv agent session. |
| `AgentPlanAdd` | Tilføj nyt todo item til plan. |
| `AgentPlanUpdate` | Opdater todo status (pending/in_progress/completed/skipped). |

→ Se [tools/mcp/agentplan.unified.md](tools/mcp/agentplan.unified.md) for parametre

### Canvas

| Tool | Beskrivelse |
|------|-------------|
| `GetCanvasList` | List canvases i en workspace. |
| `GetCanvasDetail` | Hent canvas med alle nodes og edges. |
| `CreateCanvas` | Opret ny canvas. |
| `AddCanvasNode` | Tilføj node til canvas (text/file/link/group/entity). |
| `SaveContextToCanvas` | Gem aktuel agent context til canvas. |

→ Se [tools/mcp/canvas.unified.md](tools/mcp/canvas.unified.md) for parametre

### Projects

| Tool | Beskrivelse |
|------|-------------|
| `GetProjects` | List alle projekter for workspace |
| `GetProjectDetails` | Hent detaljeret projektinformation |
| `CreateProject` | Opret nyt projekt (inkl. underprojekter via parentId) |
| `UpdateProject` | Opdater eksisterende projekt (inkl. flyt via parentId) |
| `DeleteProject` | Slet projekt |
| `UpdateProjectsBulk` | Bulk opdater projekter (kræver bekræftelse) |
| `ExecuteUpdateProjectsBulk` | Udfør bulk opdatering af projekter |

→ Se [tools/mcp/projects.unified.md](tools/mcp/projects.unified.md) for parametre

### Tasks

| Tool | Beskrivelse |
|------|-------------|
| `GetTasks` | Hent opgaver for projekt |
| `GetTaskDetails` | Hent detaljeret opgaveinformation |
| `CreateTask` | Opret ny opgave (inkl. underopgaver via parentTaskId) |
| `UpdateTask` | Opdater opgave (inkl. flyt via parentTaskId) |
| `DeleteTask` | Slet opgave |
| `CreateTasksBulk` | Bulk opret (kræver bekræftelse) |
| `ExecuteCreateTasksBulk` | Udfør bulk efter bekræftelse |
| `UpdateTasksBulk` | Bulk opdater (kræver bekræftelse) |
| `ExecuteUpdateTasksBulk` | Udfør bulk opdatering efter bekræftelse |

→ Se [tools/mcp/tasks.unified.md](tools/mcp/tasks.unified.md) for parametre

### Customers

| Tool | Beskrivelse |
|------|-------------|
| `GetCustomers` | List kunder |
| `GetCustomerDetails` | Hent detaljeret kundeinformation |
| `CreateCustomer` | Opret kunde |
| `UpdateCustomer` | Opdater kunde |
| `DeleteCustomer` | Slet kunde |

→ Se [tools/mcp/customers.unified.md](tools/mcp/customers.unified.md) for parametre

### Agreements

| Tool | Beskrivelse |
|------|-------------|
| `GetAgreements` | List aftaler/bookinger |
| `GetAgreementDetails` | Hent detaljeret aftaleinformation |
| `CreateAgreement` | Opret aftale |
| `UpdateAgreement` | Opdater aftale |
| `DeleteAgreement` | Slet aftale |

→ Se [tools/mcp/agreements.unified.md](tools/mcp/agreements.unified.md) for parametre

### Custom Fields

| Tool | Beskrivelse |
|------|-------------|
| `GetCustomFieldDefinitions` | Hent feltdefinitioner for en domæntype |
| `GetEntityCustomFields` | Hent custom field værdier for en entitet |
| `SetCustomFieldValue` | Sæt en enkelt custom field værdi |
| `BulkSetCustomFieldValues` | Sæt flere custom field værdier på én gang |

→ Se [tools/mcp/customfields.unified.md](tools/mcp/customfields.unified.md) for parametre

### Time Tracking (AI)

| Tool | Beskrivelse |
|------|-------------|
| `SuggestTimeEntries` | AI-forslag til tidsregistrering |
| `AnalyzeTimePatterns` | Analyser brugerens tidsmønstre |

→ Se [tools/mcp/timetracking.unified.md](tools/mcp/timetracking.unified.md) for parametre

### Project Intelligence (AI)

| Tool | Beskrivelse |
|------|-------------|
| `AnalyzeProjectHealth` | Sundhedscheck med score, risici, anbefalinger |
| `SuggestTaskDecomposition` | Forslag til opgaveopdeling |
| `EstimateEffort` | AI-estimat baseret på historik |
| `FindSimilarProjects` | Find lignende projekter |

→ Se [tools/mcp/intelligence.unified.md](tools/mcp/intelligence.unified.md) for parametre

### Employees

| Tool | Beskrivelse |
|------|-------------|
| `GetEmployees` | List team medlemmer |

→ Se [tools/mcp/employees.unified.md](tools/mcp/employees.unified.md) for parametre

### DevOps - Git Repositories

| Tool | Beskrivelse |
|------|-------------|
| `GetDevOpsRepositories` | List forbundne Git repos (GitHub, GitLab, Azure DevOps) |
| `GetDevOpsRepository` | Hent detaljer for et repository |
| `ConnectRepository` | Forbind nyt Git repository med webhook setup |
| `UpdateRepository` | Opdater repository indstillinger |
| `DisconnectRepository` | Fjern repository forbindelse |

→ Se [tools/mcp/devops.unified.md](tools/mcp/devops.unified.md) for parametre

### DevOps - Pull Requests

| Tool | Beskrivelse |
|------|-------------|
| `GetPullRequests` | Hent PRs for workspace eller repository |
| `GetTaskPullRequests` | Hent PRs linket til en task |
| `GetAgentSessionPullRequests` | Hent PRs oprettet af CLI agent session |
| `LinkPullRequestToTask` | Link PR til Served task |

→ Se [tools/mcp/devops.unified.md](tools/mcp/devops.unified.md) for parametre

### DevOps - Pipeline/CI

| Tool | Beskrivelse |
|------|-------------|
| `GetPipelineRuns` | Hent pipeline runs for PR eller repository |
| `GetLatestPipelineRun` | Hent seneste CI status for PR |
| `GetPipelineJobs` | Hent jobs for en pipeline (GitLab) |
| `GetJobLog` | Hent log output fra et job |
| `RetryJob` | Retry et fejlet job |
| `CancelJob` | Annuller et kørende job |

→ Se [tools/mcp/devops.unified.md](tools/mcp/devops.unified.md) for parametre

### Files (Local Filesystem)

| Tool | Beskrivelse |
|------|-------------|
| `served_file_find` | Find files with smart filtering |
| `served_file_stats` | Get directory statistics |
| `served_file_duplicates` | Find duplicate files |
| `served_file_tree` | Display directory tree |
| `served_file_auth_status` | Check tooling auth config |
| `served_file_auth_allow` | Grant temporary path access |

→ Se [tools/mcp/files.unified.md](tools/mcp/files.unified.md) for parametre

---

## REST API Tools Oversigt

Base URL: `https://app.served.dk`

### Customer

| Endpoint | Metode | Beskrivelse |
|----------|--------|-------------|
| `/api/calendar/customer/Get` | GET | Hent kunde |
| `/api/calendar/customer/GetKeys` | POST | Hent kunde IDs |
| `/api/calendar/customer/Create` | POST | Opret kunde |
| `/api/calendar/customer/Update` | POST | Opdater kunde |
| `/api/calendar/customer/Delete` | DELETE | Slet kunder |
| `/api/calendar/customer/LookUp` | POST | Søg kunder |

→ Se [tools/api/customer.md](tools/api/customer.md) for detaljer

### Project (API V2)

| Endpoint | Metode | Beskrivelse |
|----------|--------|-------------|
| `/api/projects` | GET | List projekter med filtrering |
| `/api/projects/{id}` | GET | Hent projekt detaljer |
| `/api/projects` | POST | Opret projekt |
| `/api/projects/{id}` | PUT | Opdater projekt |
| `/api/projects/{id}` | PATCH | Delvis opdatering |
| `/api/projects/{id}` | DELETE | Slet projekt |
| `/api/projects/by-customer/{customerId}` | GET | Projekter for kunde |
| `/api/projects/grouping` | POST | Hent grupperet |
| `/api/projects/can-delete` | POST | Check sletbarhed |

→ Se [tools/api/project.md](tools/api/project.md) for detaljer

### Task (API V2)

| Endpoint | Metode | Beskrivelse |
|----------|--------|-------------|
| `/api/tasks` | GET | List tasks med filtrering |
| `/api/tasks/{id}` | GET | Hent task detaljer |
| `/api/tasks` | POST | Opret task |
| `/api/tasks/{id}` | PUT | Opdater task |
| `/api/tasks/{id}` | PATCH | Delvis opdatering |
| `/api/tasks/{id}` | DELETE | Slet task |
| `/api/tasks/{id}/status` | PATCH | Opdater status |
| `/api/tasks/by-project/{projectId}` | GET | Tasks for projekt |
| `/api/tasks/grouping` | POST | Hent grupperet |
| `/api/tasks/gantt` | POST | Hent Gantt data |

→ Se [tools/api/task.md](tools/api/task.md) for detaljer

### Agreement (API V2)

| Endpoint | Metode | Beskrivelse |
|----------|--------|-------------|
| `/api/agreements` | GET | List aftaler med filtrering |
| `/api/agreements/{id}` | GET | Hent aftale |
| `/api/agreements` | POST | Opret aftale |
| `/api/agreements/{id}` | PUT | Opdater aftale |
| `/api/agreements/{id}` | DELETE | Slet aftale |
| `/api/agreements/by-customer/{customerId}` | GET | Aftaler for kunde |
| `/api/agreements/by-date-range` | GET | Aftaler i datointerval |

→ Se [tools/api/agreement.md](tools/api/agreement.md) for detaljer

### Meeting (API V2)

| Endpoint | Metode | Beskrivelse |
|----------|--------|-------------|
| `/api/meetings` | GET | List møder med filtrering |
| `/api/meetings/{id}` | GET | Hent møde detaljer |
| `/api/meetings` | POST | Opret møde |
| `/api/meetings/{id}` | PUT | Opdater møde |
| `/api/meetings/{id}` | DELETE | Slet møde |
| `/api/meetings/by-claim/{claimId}` | GET | Møde via claim ID |
| `/api/meetings/{id}/participants` | GET/POST | Deltagere |
| `/api/meetings/{id}/recordings` | GET/POST | Optagelser |
| `/api/meetings/{id}/notes` | GET/POST | Mødenoter |
| `/api/meetings/{id}/action-items` | GET/POST | Handlingspunkter |

→ Se [tools/api/meeting.md](tools/api/meeting.md) for detaljer

### Time Registration

| Endpoint | Metode | Beskrivelse |
|----------|--------|-------------|
| `/api/registration/timeregistration/GetKeys` | POST | Hent IDs |
| `/api/registration/timeregistration/Get` | POST | Hent registreringer |
| `/api/registration/timeregistration/Group` | POST | Hent grupperet |
| `/api/registration/timeregistration/Save` | POST | Opret/opdater |
| `/api/registration/timeregistration/Delete` | DELETE | Slet |
| `/api/registration/timeregistration/suggestions` | GET | AI-forslag |
| `/api/registration/timeregistration/patterns` | GET | Tidsmønstre |

→ Se [tools/api/timeregistration.md](tools/api/timeregistration.md) for detaljer

### Finance (Invoice)

| Endpoint | Metode | Beskrivelse |
|----------|--------|-------------|
| `/api/finance/invoice/GetKeys` | POST | Hent faktura IDs |
| `/api/finance/invoice/GetRange` | POST | Hent fakturaer |
| `/api/finance/invoice/GetGrouping` | POST | Hent grupperet |
| `/api/finance/invoice/Create` | POST | Opret faktura |
| `/api/finance/invoice/Update` | POST | Opdater faktura |
| `/api/finance/invoice/UpdateMultiple` | PATCH | Batch opdater |
| `/api/finance/invoice/{id}/pdf` | GET | Download PDF |

→ Se [tools/api/finance.md](tools/api/finance.md) for detaljer

### Custom Fields

| Endpoint | Metode | Beskrivelse |
|----------|--------|-------------|
| `/api/customField/GetSections` | GET | Hent sektioner |
| `/api/customField/GetDefinitions` | GET | Hent feltdefinitioner |
| `/api/customField/GetEntityFields` | GET | Hent værdier for entitet |
| `/api/customField/SetValue` | POST | Sæt enkelt værdi |
| `/api/customField/BulkSetValues` | POST | Sæt flere værdier |

→ Se [tools/mcp/customfields.unified.md](tools/mcp/customfields.unified.md) for MCP integration

---

## Observability & Tracing

MCP Server uses Served.SDK for distributed tracing via OpenTelemetry.

### Configuration

```bash
# Enable tracing via environment variables
export SERVED_MCP_TRACING=true
export FORGE_API_KEY="your-forge-api-key"

# Or for OTLP collector
export OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4317"
```

### What Gets Traced

Every tool call captures:

| Attribute | Description |
|-----------|-------------|
| `mcp.tool.name` | Tool name (e.g., `GetTasks`) |
| `mcp.tool.success` | Whether the call succeeded |
| `mcp.session.id` | Session identifier |
| `mcp.agent.id` | Agent identifier |
| `mcp.conversation.turn` | Turn number in conversation |
| `mcp.result.size` | Size of result payload |
| Duration | Execution time in ms |

### Metrics

| Metric | Type | Labels |
|--------|------|--------|
| `mcp.tool.duration` | Histogram | tool_name, success |

### Disable Tracking

```bash
export SERVED_MCP_TRACKING=false
```

---

## Miljøer

| Miljø | URL |
|-------|-----|
| Production | `https://app.served.dk` |
| MCP Server | `https://app.served.dk/mcp` |
| Local Dev | `http://localhost:5010` |

## Authentication

**REST API:**
```
Authorization: Bearer <JWT_TOKEN>
```

**MCP:**
OAuth med scopes: `projects`, `tasks`, `customers`, `calendar`, `timetracking`, `employees`, `intelligence`, `customfields`, `devops`

---

## Changelog

### v2026.1.2 (2026-01-17)

- **Unified File Format** - All documentation converted to `.unified.md` format with YAML frontmatter
- **SDK Tracing** - Integrated with Served.SDK tracing infrastructure
- **OpenTelemetry** - Tool calls now emit spans and metrics
- **Forge Integration** - Native export to Forge observability platform
- **Analytics** - Enhanced tool usage tracking with session context
- **DevOps Enhancement** - Extended DevOps tools with pipeline jobs, logs, retry, and cancel
- **File Tools** - Added local filesystem tools with tooling-auth protection
- **JSON Schema** - Added `schemas/unified-v1.json` for format validation

### v2026.1.1

- Initial MCP server implementation
- 40+ tools for Served platform access
- DevOps integration (repos, PRs, pipelines)
- AI intelligence tools
