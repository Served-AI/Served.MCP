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

### Project

| Endpoint | Metode | Beskrivelse |
|----------|--------|-------------|
| `/api/project_management/project/GetKeys` | POST | Hent projekt IDs |
| `/api/project_management/project/GetGrouping` | POST | Hent grupperet |
| `/api/project_management/project/Detailed` | POST | Hent detaljeret |
| `/api/project_management/project/Create` | POST | Opret projekt |
| `/api/project_management/project/Update` | POST | Opdater projekt |
| `/api/project_management/project/UpdateMultiple` | PATCH | Batch opdater |
| `/api/project_management/project/Delete` | DELETE | Slet projekt |

→ Se [tools/api/project.md](tools/api/project.md) for detaljer

### Task

| Endpoint | Metode | Beskrivelse |
|----------|--------|-------------|
| `/api/project_management/task/GetKeys` | POST | Hent task IDs |
| `/api/project_management/task/GetGrouping` | POST | Hent grupperet |
| `/api/project_management/task/Gantt` | POST | Hent Gantt data |
| `/api/project_management/task/Detailed` | POST | Hent detaljeret |
| `/api/project_management/task/Create` | POST | Opret task |
| `/api/project_management/task/Update` | POST | Opdater task |
| `/api/project_management/task/Patch` | POST | Hurtig opdatering |
| `/api/project_management/task/Delete` | DELETE | Slet task |

→ Se [tools/api/task.md](tools/api/task.md) for detaljer

### Agreement

| Endpoint | Metode | Beskrivelse |
|----------|--------|-------------|
| `/api/calendar/agreement/Get` | GET | Hent aftale |
| `/api/calendar/agreement/GetKeys` | POST | Hent aftale IDs |
| `/api/calendar/agreement/Create` | POST | Opret aftale |
| `/api/calendar/agreement/Update` | POST | Opdater aftale |
| `/api/calendar/agreement/Delete` | DELETE | Slet aftaler |

→ Se [tools/api/agreement.md](tools/api/agreement.md) for detaljer

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
OAuth med scopes: `projects`, `tasks`, `customers`, `calendar`, `timetracking`, `employees`, `intelligence`, `customfields`
