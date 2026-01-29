# Served MCP Tools

MCP (Model Context Protocol) tools til AI assistenter.

## MCP Server

```
https://app.served.dk/mcp
```

## Tools Oversigt

| Dokument | Tools | Beskrivelse |
|----------|-------|-------------|
| [context.md](context.md) | GetUserContext | Bruger og workspace kontekst |
| [projects.md](projects.md) | GetProjects, GetProjectDetails, CreateProject, UpdateProject, DeleteProject, UpdateProjectsBulk | Projekt CRUD med hierarki |
| [tasks.md](tasks.md) | GetTasks, GetTaskDetails, CreateTask, UpdateTask, DeleteTask, Bulk | Opgave CRUD med hierarki og bulk |
| [customers.md](customers.md) | GetCustomers, CreateCustomer, UpdateCustomer | Kunde CRUD |
| [agreements.md](agreements.md) | GetAgreements, CreateAgreement | Aftaler/bookinger |
| [customfields.md](customfields.md) | GetCustomFieldDefinitions, GetEntityCustomFields, SetCustomFieldValue, BulkSetCustomFieldValues | Brugerdefinerede felter |
| [timetracking.md](timetracking.md) | SuggestTimeEntries, AnalyzeTimePatterns | AI tidsregistrering |
| [intelligence.md](intelligence.md) | AnalyzeProjectHealth, EstimateEffort, m.fl. | AI analyse tools |
| [employees.md](employees.md) | GetEmployees | Team medlemmer |

## Quick Reference

### Context
| Tool | Beskrivelse |
|------|-------------|
| `GetUserContext` | **Kald først** - Hent bruger og workspaces |

### Projects
| Tool | Beskrivelse |
|------|-------------|
| `GetProjects` | List projekter (med optional selection mode) |
| `GetProjectDetails` | Hent detaljeret projektinfo |
| `CreateProject` | Opret projekt (parentId for underprojekter) |
| `UpdateProject` | Opdater projekt (parentId for flytning) |
| `DeleteProject` | Slet projekt |
| `UpdateProjectsBulk` | Bulk opdater (kræver bekræftelse) |

### Tasks
| Tool | Beskrivelse |
|------|-------------|
| `GetTasks` | Hent opgaver for projekt |
| `GetTaskDetails` | Hent detaljeret opgaveinfo |
| `CreateTask` | Opret opgave (parentTaskId for underopgaver) |
| `UpdateTask` | Opdater opgave (parentTaskId for flytning) |
| `DeleteTask` | Slet opgave |
| `CreateTasksBulk` | Bulk opret (kræver bekræftelse) |
| `ExecuteCreateTasksBulk` | Udfør bulk |
| `UpdateTasksBulk` | Bulk opdater (kræver bekræftelse) |
| `ExecuteUpdateTasksBulk` | Udfør bulk opdatering |

### Customers
| Tool | Beskrivelse |
|------|-------------|
| `GetCustomers` | List kunder |
| `CreateCustomer` | Opret kunde |
| `UpdateCustomer` | Opdater kunde |

### Agreements
| Tool | Beskrivelse |
|------|-------------|
| `GetAgreements` | List aftaler |
| `CreateAgreement` | Opret aftale |

### Custom Fields
| Tool | Beskrivelse |
|------|-------------|
| `GetCustomFieldDefinitions` | Hent feltdefinitioner per domæntype |
| `GetEntityCustomFields` | Hent værdier for entitet |
| `SetCustomFieldValue` | Sæt enkelt værdi |
| `BulkSetCustomFieldValues` | Sæt flere værdier |

### Time Tracking (AI)
| Tool | Beskrivelse |
|------|-------------|
| `SuggestTimeEntries` | AI tidsforslag |
| `AnalyzeTimePatterns` | Mønsteranalyse |

### Intelligence (AI)
| Tool | Beskrivelse |
|------|-------------|
| `AnalyzeProjectHealth` | Sundhedscheck med score |
| `SuggestTaskDecomposition` | Opgaveopdeling |
| `EstimateEffort` | AI estimering |
| `FindSimilarProjects` | Find lignende projekter |

### Employees
| Tool | Beskrivelse |
|------|-------------|
| `GetEmployees` | List team medlemmer |

## Authentication

OAuth med scopes:
- `projects` - Projekter
- `tasks` - Opgaver
- `customers` - Kunder
- `calendar` - Aftaler
- `timetracking` - Tidsregistrering
- `employees` - Team
- `intelligence` - AI tools
- `customfields` - Brugerdefinerede felter
