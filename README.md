# ServedMCP

AI skills og tools dokumentation for Served platformen.

## Quick Links

| Dokument | Beskrivelse |
|----------|-------------|
| [skills/claude.md](skills/claude.md) | REST API reference (500+ endpoints) |
| [skills/mcp.md](skills/mcp.md) | MCP tools guide og workflows |
| [tools/api/](tools/api/) | REST API tool dokumentation |
| [tools/mcp/](tools/mcp/) | MCP Server tools |

---

## MCP Tools Oversigt

MCP Server: `https://app.served.dk/mcp`

### Context

| Tool | Beskrivelse |
|------|-------------|
| `GetUserContext` | Hent bruger profil og workspaces. **Kald denne først.** |
| `GetTenantContext` | Hent detaljeret tenant info (indstillinger, kategorier). |

→ Se [tools/mcp/context.md](tools/mcp/context.md) for detaljer

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

→ Se [tools/mcp/projects.md](tools/mcp/projects.md) for parametre

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

→ Se [tools/mcp/tasks.md](tools/mcp/tasks.md) for parametre

### Customers

| Tool | Beskrivelse |
|------|-------------|
| `GetCustomers` | List kunder |
| `GetCustomerDetails` | Hent detaljeret kundeinformation |
| `CreateCustomer` | Opret kunde |
| `UpdateCustomer` | Opdater kunde |
| `DeleteCustomer` | Slet kunde |

→ Se [tools/mcp/customers.md](tools/mcp/customers.md) for parametre

### Agreements

| Tool | Beskrivelse |
|------|-------------|
| `GetAgreements` | List aftaler/bookinger |
| `GetAgreementDetails` | Hent detaljeret aftaleinformation |
| `CreateAgreement` | Opret aftale |
| `UpdateAgreement` | Opdater aftale |
| `DeleteAgreement` | Slet aftale |

→ Se [tools/mcp/agreements.md](tools/mcp/agreements.md) for parametre

### Custom Fields

| Tool | Beskrivelse |
|------|-------------|
| `GetCustomFieldDefinitions` | Hent feltdefinitioner for en domæntype |
| `GetEntityCustomFields` | Hent custom field værdier for en entitet |
| `SetCustomFieldValue` | Sæt en enkelt custom field værdi |
| `BulkSetCustomFieldValues` | Sæt flere custom field værdier på én gang |

→ Se [tools/mcp/customfields.md](tools/mcp/customfields.md) for parametre

### Time Tracking (AI)

| Tool | Beskrivelse |
|------|-------------|
| `SuggestTimeEntries` | AI-forslag til tidsregistrering |
| `AnalyzeTimePatterns` | Analyser brugerens tidsmønstre |

→ Se [tools/mcp/timetracking.md](tools/mcp/timetracking.md) for parametre

### Project Intelligence (AI)

| Tool | Beskrivelse |
|------|-------------|
| `AnalyzeProjectHealth` | Sundhedscheck med score, risici, anbefalinger |
| `SuggestTaskDecomposition` | Forslag til opgaveopdeling |
| `EstimateEffort` | AI-estimat baseret på historik |
| `FindSimilarProjects` | Find lignende projekter |

→ Se [tools/mcp/intelligence.md](tools/mcp/intelligence.md) for parametre

### Employees

| Tool | Beskrivelse |
|------|-------------|
| `GetEmployees` | List team medlemmer |

→ Se [tools/mcp/employees.md](tools/mcp/employees.md) for parametre

### DevOps - Git Repositories

| Tool | Beskrivelse |
|------|-------------|
| `GetDevOpsRepositories` | List forbundne Git repos (GitHub, GitLab, Azure DevOps) |
| `GetDevOpsRepository` | Hent detaljer for et repository |
| `ConnectRepository` | Forbind nyt Git repository med webhook setup |
| `UpdateRepository` | Opdater repository indstillinger |
| `DisconnectRepository` | Fjern repository forbindelse |

→ Se [tools/mcp/devops.md](tools/mcp/devops.md) for parametre

### DevOps - Pull Requests

| Tool | Beskrivelse |
|------|-------------|
| `GetPullRequests` | Hent PRs for workspace eller repository |
| `GetTaskPullRequests` | Hent PRs linket til en task |
| `GetAgentSessionPullRequests` | Hent PRs oprettet af CLI agent session |
| `LinkPullRequestToTask` | Link PR til Served task |

→ Se [tools/mcp/devops.md](tools/mcp/devops.md) for parametre

### DevOps - Pipeline/CI

| Tool | Beskrivelse |
|------|-------------|
| `GetPipelineRuns` | Hent pipeline runs for PR eller repository |
| `GetLatestPipelineRun` | Hent seneste CI status for PR |

→ Se [tools/mcp/devops.md](tools/mcp/devops.md) for parametre

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

→ Se [tools/mcp/customfields.md](tools/mcp/customfields.md) for MCP integration

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
