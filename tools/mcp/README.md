# Served MCP Tools

MCP (Model Context Protocol) tools til AI assistenter som Claude.

## Oversigt

Served eksponerer følgende MCP tools kategorier:

| Kategori | Beskrivelse |
|----------|-------------|
| [Context](#context) | Bruger og workspace kontekst |
| [Projects](#projects) | Projekt CRUD operationer |
| [Tasks](#tasks) | Opgave CRUD og bulk operationer |
| [Customers](#customers) | Kunde CRUD operationer |
| [Agreements](#agreements) | Aftaler/bookinger |
| [Time Tracking](#time-tracking) | Tidsregistrering og AI-forslag |
| [Intelligence](#project-intelligence) | AI-powered analyse tools |
| [Employees](#employees) | Team medlemmer |

## Authentication

MCP tools kræver OAuth authentication. Token skal indeholde:
- `userId` - Bruger ID
- `tenantId` - Workspace/organisation ID
- `scope` - Tilladte scopes

## MCP Server Endpoint

```
https://app.served.dk/mcp
```

---

## Context

### GetUserContext

Hent brugerens profil og alle tilgængelige workspaces.

**Kald denne først** for at forstå hvem du arbejder for og hvilket workspace der skal bruges.

```json
{
  "tool": "GetUserContext"
}
```

**Response:**
```json
{
  "success": true,
  "user": {
    "id": 10,
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "fullName": "John Doe"
  },
  "workspaceCount": 2,
  "workspaces": [
    {
      "tenantId": 1,
      "tenantName": "Acme Corporation",
      "tenantSlug": "acme",
      "position": "Developer",
      "accessLevel": "Admin",
      "isAdministrator": true
    }
  ],
  "primaryWorkspace": { ... },
  "hint": "User has access to 2 workspaces. Use tenantId parameter to specify which one."
}
```

---

## Projects

### GetProjects

Hent alle projekter for et workspace.

**Parametre:**
| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |

```json
{
  "tool": "GetProjects",
  "tenantId": 1
}
```

**Response:**
```
Her er dine 5 projekter:

@project[101] {
  name: "Website Redesign"
  projectNo: "PRJ-2025-001"
  progress: 45%
  status: "I gang"
  dates: "2025-01-01 til 2025-06-30"
  manager: "John Doe"
}
...
```

---

### CreateProject

Opret et nyt projekt.

**Parametre:**
| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| name | string | Ja | Projektnavn |
| description | string | Nej | Projektbeskrivelse |
| startDate | string | Nej | Start (YYYY-MM-DD) |
| endDate | string | Nej | Slut (YYYY-MM-DD) |
| projectNo | string | Nej | Projektnummer |

```json
{
  "tool": "CreateProject",
  "tenantId": 1,
  "name": "Nyt Projekt",
  "description": "Projektbeskrivelse",
  "startDate": "2025-02-01",
  "endDate": "2025-05-31"
}
```

---

### UpdateProject

Opdater et eksisterende projekt.

**Parametre:**
| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| projectId | int | Ja | Projekt ID |
| name | string | Nej | Nyt navn |
| description | string | Nej | Ny beskrivelse |
| startDate | string | Nej | Ny start |
| endDate | string | Nej | Ny slut |

---

## Tasks

### GetTasks

Hent opgaver for et projekt.

**Parametre:**
| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| projectId | int | Ja | Projekt ID |

```json
{
  "tool": "GetTasks",
  "tenantId": 1,
  "projectId": 101
}
```

**Response:**
```
Her er 8 opgaver:

@task[201] {
  name: "Design mockups"
  taskNo: "T-001"
  progress: 75%
  priority: "Høj"
  dates: "2025-01-15 til 2025-01-31"
}
...
```

---

### CreateTask

Opret en ny opgave.

**Parametre:**
| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| projectId | int | Ja | Projekt ID |
| name | string | Ja | Opgavenavn |
| description | string | Nej | Beskrivelse |
| startDate | string | Nej | Start (YYYY-MM-DD) |
| endDate | string | Nej | Slut (YYYY-MM-DD) |
| priority | int | Nej | 1=Kritisk, 2=Høj, 3=Normal, 4=Lav |
| isBillable | bool | Nej | Fakturerbar (default: true) |

---

### UpdateTask

Opdater en eksisterende opgave.

**Parametre:**
| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| taskId | int | Ja | Opgave ID |
| name | string | Nej | Nyt navn |
| description | string | Nej | Ny beskrivelse |
| progress | int | Nej | Fremskridt (0-100) |
| priority | int | Nej | Ny prioritet |
| startDate | string | Nej | Ny start |
| endDate | string | Nej | Ny slut |

---

### CreateTasksBulk

Opret flere opgaver på én gang. Returnerer bekræftelsesanmodning.

**Parametre:**
| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| tasksJson | string | Ja | JSON array af opgaver |

```json
{
  "tool": "CreateTasksBulk",
  "tenantId": 1,
  "tasksJson": "[{\"name\": \"Task 1\", \"projectId\": 101}, {\"name\": \"Task 2\", \"projectId\": 101}]"
}
```

---

### ExecuteCreateTasksBulk

Udfør bulk oprettelse efter brugerbekræftelse.

---

## Customers

### GetCustomers

Hent kunder for et workspace.

**Parametre:**
| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |

---

### CreateCustomer

Opret en ny kunde.

**Parametre:**
| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| firstName | string | Ja | Fornavn/firmanavn |
| lastName | string | Nej | Efternavn |
| email | string | Nej | Email |
| phone | string | Nej | Telefon |

---

### UpdateCustomer

Opdater en eksisterende kunde.

---

## Agreements

### GetAgreements

Hent aftaler/bookinger for et workspace.

**Parametre:**
| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |

---

### CreateAgreement

Opret en ny aftale/booking.

**Parametre:**
| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| title | string | Ja | Aftaletitel |
| startDate | string | Ja | Start (YYYY-MM-DDTHH:mm) |
| endDate | string | Ja | Slut (YYYY-MM-DDTHH:mm) |
| customerId | int | Nej | Kunde ID |
| task | string | Nej | Beskrivelse/opgave |

---

## Time Tracking

### SuggestTimeEntries

AI-genererede forslag til tidsregistrering baseret på mønstre og kalender.

**Parametre:**
| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| startDate | string | Ja | Fra dato (YYYY-MM-DD) |
| endDate | string | Ja | Til dato (YYYY-MM-DD) |
| includeCalendar | bool | Nej | Inkluder kalender (default: true) |
| minConfidence | double | Nej | Min. sikkerhed 0-1 (default: 0.5) |

```json
{
  "tool": "SuggestTimeEntries",
  "tenantId": 1,
  "startDate": "2025-01-20",
  "endDate": "2025-01-24"
}
```

**Response:**
```json
{
  "Type": "time_entry_suggestions",
  "Count": 5,
  "TotalHours": 35.5,
  "PatternCount": 3,
  "CalendarEventCount": 2,
  "Summary": "Analyseret 90 dages historik...",
  "Suggestions": [
    {
      "Dato": "2025-01-20",
      "ProjektId": 101,
      "ProjektNavn": "Website Redesign",
      "OpgaveId": 201,
      "OpgaveNavn": "Frontend udvikling",
      "ForeslåedeTimer": 7.5,
      "Sikkerhed": "85%",
      "Kilde": "HistoricalPattern",
      "Begrundelse": "Du arbejder typisk 7.5 timer på denne opgave om mandagen"
    }
  ]
}
```

---

### AnalyzeTimePatterns

Analyser brugerens tidsmønstre.

**Parametre:**
| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| lookbackDays | int | Nej | Dage at analysere (default: 90) |

---

## Project Intelligence

### SuggestTaskDecomposition

AI-powered forslag til opgaveopdeling baseret på lignende projekter.

**Parametre:**
| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| description | string | Ja | Beskrivelse af opgave/feature |
| projectType | string | Ja | Projekttype eller keywords |

```json
{
  "tool": "SuggestTaskDecomposition",
  "tenantId": 1,
  "description": "Implementer brugerlogin med OAuth",
  "projectType": "web development"
}
```

---

### EstimateEffort

AI-powered estimat baseret på historiske data.

**Parametre:**
| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| description | string | Ja | Opgavebeskrivelse |
| featureList | string | Ja | Komma-separeret liste af features |
| teamSize | int | Nej | Teamstørrelse (default: 1) |
| riskTolerance | string | Nej | conservative/moderate/aggressive |

---

### AnalyzeProjectHealth

Analysér projektsundhed med score, risici og anbefalinger.

**Parametre:**
| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| projectId | int | Ja | Projekt ID |
| includeRecommendations | bool | Nej | Inkluder anbefalinger (default: true) |

```json
{
  "tool": "AnalyzeProjectHealth",
  "tenantId": 1,
  "projectId": 101
}
```

**Response:**
```json
{
  "ProjectId": 101,
  "ProjectName": "Website Redesign",
  "OverallHealthScore": 72,
  "Status": "I Risiko",
  "AlertLevel": "Gul",
  "Metrics": {
    "TotalTasks": 15,
    "CompletedTasks": 8,
    "OverdueTasks": 2,
    "TaskCompletionPercentage": 53.3,
    "ProjectProgress": 45,
    "DaysRemaining": 45,
    "WeeklyVelocity": 1.5
  },
  "Risks": [
    {
      "Description": "2 opgaver er overskredet deadline",
      "Severity": "Medium",
      "Impact": "Kan forsinke hele projektet",
      "MitigationSuggestion": "Prioriter og omfordel ressourcer"
    }
  ],
  "Recommendations": [...],
  "Forecast": {
    "OnTimeProbability": 65,
    "ExpectedCompletionDate": "2025-07-15",
    "Notes": "Projektet kan blive 15 dage forsinket"
  }
}
```

---

### FindSimilarProjects

Find lignende projekter baseret på beskrivelse og mønstre.

**Parametre:**
| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| description | string | Ja | Søgebeskrivelse |
| count | int | Nej | Max resultater (default: 5) |
| includePatterns | bool | Nej | Inkluder mønsteranalyse (default: true) |

---

## Employees

### GetEmployees

Hent team medlemmer i et workspace.

**Parametre:**
| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| activeOnly | bool | Nej | Kun aktive (default: true) |

```json
{
  "tool": "GetEmployees",
  "tenantId": 1
}
```

**Response:**
```json
{
  "success": true,
  "count": 5,
  "employees": [
    {
      "userId": 10,
      "email": "john@example.com",
      "fullName": "John Doe",
      "initials": "JD",
      "position": "Developer",
      "employeeType": "Fuldtid",
      "accessLevel": "Admin",
      "isActive": true
    }
  ]
}
```
