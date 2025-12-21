# Tasks Tools

Håndter opgaver via MCP.

---

## GetTasks

Hent opgaver for et projekt.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| projectId | int | Ja | Projekt ID |

### Request

```json
{
  "tool": "GetTasks",
  "tenantId": 1,
  "projectId": 101
}
```

### Response

```
Her er 8 opgaver:

@task[201] {
  name: "Design mockups"
  taskNo: "T-001"
  progress: 75%
  priority: "Høj"
  dates: "2025-01-15 til 2025-01-31"
}

@task[202] {
  name: "Frontend udvikling"
  taskNo: "T-002"
  progress: 30%
  priority: "Normal"
  dates: "2025-02-01 til 2025-02-28"
}

@task[203] {
  name: "Backend API"
  taskNo: "T-003"
  progress: 0%
  priority: "Normal"
  dates: "Ikke planlagt"
}
```

### @task Reference

Brug `@task[id]` til at referere til opgaver i efterfølgende kald.

---

## GetTaskDetails

Hent detaljeret information om en opgave.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| taskId | int | Ja | Opgave ID |

### Request

```json
{
  "tool": "GetTaskDetails",
  "tenantId": 1,
  "taskId": 201
}
```

### Response

```
@task-details[201] {
  name: "Design mockups"
  description: "Opret wireframes og mockups for alle sider"
  status: 2 (Status ID)
  progress: 75%
  priority: "Høj"
  dates: "2025-01-15 til 2025-01-31"
  plannedEffort: 40
  actualEffort: 30
}
```

---

## CreateTask

Opret en ny opgave.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| projectId | int | Ja | Projekt ID |
| name | string | Ja | Opgavenavn |
| parentTaskId | int | Nej | Parent opgave ID (for underopgaver) |
| description | string | Nej | Beskrivelse |
| startDate | string | Nej | Start dato (YYYY-MM-DD) |
| endDate | string | Nej | Slut dato (YYYY-MM-DD) |
| priority | int | Nej | Prioritet (default: 3) |
| isBillable | bool | Nej | Fakturerbar (default: true) |

### Priority Værdier

| Værdi | Betydning |
|-------|-----------|
| 1 | Kritisk |
| 2 | Høj |
| 3 | Normal (default) |
| 4 | Lav |
| 5 | Meget lav |

### Request - Simpel opgave

```json
{
  "tool": "CreateTask",
  "tenantId": 1,
  "projectId": 101,
  "name": "Implementer login",
  "description": "Implementer brugerlogin med JWT",
  "startDate": "2025-02-01",
  "endDate": "2025-02-07",
  "priority": 2,
  "isBillable": true
}
```

### Request - Underopgave

```json
{
  "tool": "CreateTask",
  "tenantId": 1,
  "projectId": 101,
  "name": "Design login form",
  "parentTaskId": 201,
  "description": "Opret UI design for login formularen",
  "priority": 2
}
```

### Response

```
Opgave oprettet succesfuldt med ID: 204
```

---

## UpdateTask

Opdater en eksisterende opgave.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| taskId | int | Ja | Opgave ID |
| name | string | Nej | Nyt navn |
| parentTaskId | int | Nej | Nyt parent opgave ID |
| description | string | Nej | Ny beskrivelse |
| progress | int | Nej | Fremskridt (0-100) |
| priority | int | Nej | Ny prioritet |
| startDate | string | Nej | Ny start dato |
| endDate | string | Nej | Ny slut dato |

### Request - Opdater progress

```json
{
  "tool": "UpdateTask",
  "tenantId": 1,
  "taskId": 201,
  "progress": 100
}
```

### Request - Opdater flere felter

```json
{
  "tool": "UpdateTask",
  "tenantId": 1,
  "taskId": 202,
  "name": "Frontend udvikling - Fase 1",
  "progress": 50,
  "priority": 2,
  "endDate": "2025-03-15"
}
```

### Request - Flyt til underopgave

```json
{
  "tool": "UpdateTask",
  "tenantId": 1,
  "taskId": 205,
  "parentTaskId": 201
}
```

### Request - Flyt til top-level

```json
{
  "tool": "UpdateTask",
  "tenantId": 1,
  "taskId": 205,
  "parentTaskId": null
}
```

### Response

```
Opgave 201 opdateret succesfuldt.
```

---

## DeleteTask

Slet en opgave.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| taskId | int | Ja | Opgave ID |

### Request

```json
{
  "tool": "DeleteTask",
  "tenantId": 1,
  "taskId": 205
}
```

### Response

```
Opgave med ID 205 slettet succesfuldt.
```

### Advarsel

Sletning af en opgave kan påvirke tilknyttede underopgaver og tidsregistreringer.

---

## CreateTasksBulk

Opret flere opgaver på én gang. **Kræver brugerbekræftelse**.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| tasksJson | string | Ja | JSON array af opgaver |

### Task JSON Format

```json
[
  {
    "name": "Opgave 1",
    "projectId": 101,
    "description": "Beskrivelse",
    "priority": 2,
    "parentTaskId": null,
    "startDate": "2025-02-01",
    "endDate": "2025-02-15",
    "isBillable": true
  },
  {
    "name": "Underopgave 1.1",
    "projectId": 101,
    "description": "En underopgave",
    "parentTaskId": 201,
    "priority": 3
  }
]
```

### Request

```json
{
  "tool": "CreateTasksBulk",
  "tenantId": 1,
  "tasksJson": "[{\"name\": \"Setup projekt\", \"projectId\": 101}, {\"name\": \"Design database\", \"projectId\": 101}, {\"name\": \"Implementer API\", \"projectId\": 101}]"
}
```

### Response (Bekræftelsesanmodning)

```json
{
  "Action": "create_tasks_bulk",
  "Count": 3,
  "Tasks": [
    { "Name": "Setup projekt", "ProjectId": 101 },
    { "Name": "Design database", "ProjectId": 101 },
    { "Name": "Implementer API", "ProjectId": 101 }
  ],
  "RequiresConfirmation": true,
  "ConfirmationMessage": "Vil du oprette 3 opgaver?"
}
```

---

## ExecuteCreateTasksBulk

Udfør bulk oprettelse efter brugerbekræftelse.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| tasksJson | string | Ja | Samme JSON som CreateTasksBulk |

### Request

```json
{
  "tool": "ExecuteCreateTasksBulk",
  "tenantId": 1,
  "tasksJson": "[{\"name\": \"Setup projekt\", \"projectId\": 101}, {\"name\": \"Design database\", \"projectId\": 101}, {\"name\": \"Implementer API\", \"projectId\": 101}]"
}
```

### Response

```
Oprettet 3 ud af 3 opgaver.

✓ 'Setup projekt' (ID: 205)
✓ 'Design database' (ID: 206)
✓ 'Implementer API' (ID: 207)
```

### Response med fejl

```
Oprettet 2 ud af 3 opgaver.

✓ 'Setup projekt' (ID: 205)
✓ 'Design database' (ID: 206)

Fejl:
✗ 'Implementer API': Projekt ikke fundet
```

---



## UpdateTasksBulk

Opdater flere opgaver på én gang. **Kræver brugerbekræftelse**.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| updatesJson | string | Ja | JSON array af opdateringer |

### Update JSON Format

```json
[
  {
    "taskId": 201,
    "name": "Nyt navn",
    "progress": 100,
    "priority": 1,
    "parentTaskId": null
  },
  {
    "taskId": 202,
    "progress": 50,
    "endDate": "2025-03-15"
  }
]
```

### Request

```json
{
  "tool": "UpdateTasksBulk",
  "tenantId": 1,
  "updatesJson": "[{\"taskId\": 201, \"progress\": 100}, {\"taskId\": 202, \"progress\": 50}]"
}
```

### Response (Bekræftelsesanmodning)

```json
{
  "Action": "update_tasks_bulk",
  "Count": 2,
  "Updates": [
    { "TaskId": 201, "Progress": 100 },
    { "TaskId": 202, "Progress": 50 }
  ],
  "RequiresConfirmation": true,
  "ConfirmationMessage": "Vil du opdatere 2 opgaver?"
}
```

---

## ExecuteUpdateTasksBulk

Udfør bulk opdatering efter brugerbekræftelse.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| updatesJson | string | Ja | Samme JSON som UpdateTasksBulk |

### Request

```json
{
  "tool": "ExecuteUpdateTasksBulk",
  "tenantId": 1,
  "updatesJson": "[{\"taskId\": 201, \"progress\": 100}, {\"taskId\": 202, \"progress\": 50}]"
}
```

### Response

```
Opdateret 2 ud af 2 opgaver.

✓ 'Design mockups' (ID: 201)
✓ 'Frontend udvikling' (ID: 202)
```

---

## Workflow: Bulk Task Creation

```
1. AI: CreateTasksBulk(tenantId, tasksJson)
2. AI: "Jeg vil oprette 3 opgaver: [liste]. Skal jeg fortsætte?"
3. Bruger: "Ja"
4. AI: ExecuteCreateTasksBulk(tenantId, tasksJson)
5. AI: "Færdig! Oprettet 3 opgaver."
```

**Vigtigt:** Kald ALDRIG ExecuteCreateTasksBulk uden først at vise brugeren hvad der oprettes.

---

## Workflow: Hierarkisk opgavestruktur

```
Bruger: "Opret en opgave 'Login feature' med underopgaver for design, frontend og backend"

AI:
1. CreateTask(projectId: 101, name: "Login feature") → taskId: 210
2. CreateTask(projectId: 101, name: "Design login UI", parentTaskId: 210)
3. CreateTask(projectId: 101, name: "Frontend implementering", parentTaskId: 210)
4. CreateTask(projectId: 101, name: "Backend API", parentTaskId: 210)

"Oprettet 'Login feature' med 3 underopgaver:
- Design login UI
- Frontend implementering
- Backend API"
```

---

## Workflow: Flyt opgave

```
Bruger: "Flyt opgave 205 under opgave 201"

AI:
1. UpdateTask(taskId: 205, parentTaskId: 201)
2. "Opgave 205 er nu en underopgave af opgave 201"
```

---

## Fejlhåndtering

### Opgave ikke fundet

```
Opgave med ID 999 blev ikke fundet.
```

### Ingen opgaver i projekt

```
Ingen opgaver fundet for dette projekt.
```

### Cirkulær reference

```
Kan ikke flytte opgave - ville skabe cirkulær reference.
```

### Projekt ikke fundet (bulk)

```
Oprettet 2 ud af 3 opgaver.

✓ 'Opgave 1' (ID: 205)
✓ 'Opgave 2' (ID: 206)

Fejl:
✗ 'Opgave 3': Projekt med ID 999 ikke fundet
```
