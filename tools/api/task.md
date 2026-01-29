# Task Tool

Håndter opgaver (tasks) i Served platformen.

## Base URL

```
/api/project_management/task
```

## Endpoints

### GetKeys - Hent task IDs

```http
POST /api/project_management/task/GetKeys
```

**Scope:** `Task.Read`

**Request:**
```json
{
  "filter": {
    "projectId": 101,
    "isActive": true
  },
  "sort": {
    "field": "order",
    "direction": "asc"
  },
  "skip": 0,
  "take": 100
}
```

**Response:**
```json
[201, 202, 203, 204, 205]
```

---

### GetGrouping - Hent tasks grupperet

```http
POST /api/project_management/task/GetGrouping
```

**Scope:** `Task.Read`

**Request:**
```json
{
  "groupBy": "taskStateId",
  "filter": {
    "projectId": 101
  }
}
```

---

### Gantt - Hent Gantt data

```http
POST /api/project_management/task/Gantt
```

**Scope:** `Task.Read`

Henter tasks formateret til Gantt-visning med dependencies og ressourcer.

**Request:**
```json
{
  "filter": {
    "projectId": 101
  },
  "includeResources": true,
  "includeDependencies": true
}
```

---

### Detailed - Hent detaljeret task

```http
POST /api/project_management/task/Detailed?taskId={id}
```

**Scope:** `Task.Read`

**Response:**
```json
{
  "id": 201,
  "version": 3,
  "projectId": 101,
  "name": "Design mockups",
  "description": "Opret wireframes og mockups til alle hovedsider",
  "taskNo": "T-001",
  "taskStateId": 2,
  "taskTypeId": 1,
  "taskCategoryId": 1,
  "priority": 1,
  "progress": 75,
  "startDate": "2025-01-15",
  "endDate": "2025-01-31",
  "plannedStart": "2025-01-15",
  "plannedFinish": "2025-01-31",
  "plannedEffort": 40,
  "plannedDuration": 12,
  "plannedCost": 20000,
  "actualStart": "2025-01-16",
  "actualFinish": null,
  "actualEffort": 30,
  "actualDuration": 10,
  "actualCost": 15000,
  "budgetHours": 40,
  "budgetAmount": 20000,
  "isBillable": true,
  "isActive": true,
  "parentId": null,
  "subTasks": [205, 206],
  "resources": [10, 11],
  "tags": "design,ui",
  "color": "#3498db"
}
```

---

### Create - Opret task

```http
POST /api/project_management/task/Create
```

**Scope:** `Task.Write`

**Request:**
```json
{
  "tenantId": 1,
  "projectId": 101,
  "name": "Ny opgave",
  "description": "Opgavebeskrivelse",
  "taskStateId": 1,
  "taskTypeId": 1,
  "priority": 2,
  "startDate": "2025-02-01",
  "endDate": "2025-02-15",
  "budgetHours": 20,
  "budgetAmount": 10000,
  "isBillable": true,
  "isActive": true,
  "parentTaskId": null
}
```

**Response:**
```json
207
```

---

### Update - Opdater task

```http
POST /api/project_management/task/Update
```

**Scope:** `Task.Write`

**Request:**
```json
{
  "id": 201,
  "version": 3,
  "tenantId": 1,
  "projectId": 101,
  "name": "Design mockups - Opdateret",
  "progress": 100,
  "taskStateId": 3
}
```

---

### Patch - Hurtig opdatering

```http
POST /api/project_management/task/Patch
```

**Scope:** `Task.Write`

Til hurtig opdatering af enkelte felter.

**Request:**
```json
{
  "id": 201,
  "version": 3,
  "progress": 80,
  "taskStateId": 2
}
```

---

### PatchRange - Batch opdatering

```http
POST /api/project_management/task/PatchRange
```

**Scope:** `Task.Write`

**Request:**
```json
{
  "tenantId": 1,
  "items": [
    { "id": 201, "version": 3, "progress": 100 },
    { "id": 202, "version": 2, "progress": 50 },
    { "id": 203, "version": 1, "taskStateId": 2 }
  ]
}
```

---

### Delete - Slet tasks

```http
DELETE /api/project_management/task/Delete
```

**Scope:** `Task.Write`

**Request:**
```json
{
  "items": [
    { "id": 207, "version": 1 }
  ]
}
```

---

## Felter

| Felt | Type | Beskrivelse | Påkrævet |
|------|------|-------------|----------|
| id | int | Task ID | Nej (auto) |
| version | int | Optimistic locking | Ved update |
| tenantId | int | Organisation ID | Ja |
| projectId | int | Projekt ID | Ja |
| name | string | Opgavenavn (max 255) | Ja |
| fullName | string | Fuldt navn | Nej |
| description | string | Beskrivelse (max 4000) | Nej |
| taskNo | string | Opgavenummer | Nej |
| taskStateId | int | Status ID | Nej |
| taskTypeId | int | Type ID | Nej |
| taskCategoryId | int | Kategori ID | Nej |
| priority | int | Prioritet (1=høj) | Nej |
| progress | int | Fremskridt (0-100) | Nej |
| startDate | date | Startdato | Nej |
| endDate | date | Slutdato | Nej |
| plannedStart | date | Planlagt start | Nej |
| plannedFinish | date | Planlagt slut | Nej |
| plannedEffort | double | Planlagt indsats (timer) | Nej |
| plannedDuration | double | Planlagt varighed (dage) | Nej |
| plannedCost | double | Planlagt omkostning | Nej |
| actualStart | date | Faktisk start | Læs |
| actualFinish | date | Faktisk slut | Læs |
| actualEffort | double | Faktisk indsats | Læs |
| actualCost | double | Faktisk omkostning | Læs |
| budgetHours | double | Budget timer | Nej |
| budgetAmount | double | Budget beløb | Nej |
| isBillable | bool | Fakturerbar | Nej |
| isActive | bool | Aktiv | Nej |
| isParent | bool | Er forælder | Læs |
| parentTaskId | int | Forælder task ID | Nej |
| assignedToId | int | Tildelt bruger | Nej |
| defaultHourlyRateId | int | Standard timepris ID | Nej |
| order | int | Sorteringsrækkefølge | Nej |
| tags | string | Tags | Nej |
| color | string | Farve (hex) | Nej |
| wbs | string | WBS nummer | Læs |

## Task States (typisk)

| ID | Navn |
|----|------|
| 1 | Ikke startet |
| 2 | I gang |
| 3 | Afsluttet |
| 4 | On hold |

## Eksempler

### cURL - Opret task

```bash
curl -X POST 'https://app.served.dk/api/project_management/task/Create' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "tenantId": 1,
    "projectId": 101,
    "name": "Implementer login",
    "description": "Implementer brugerlogin med JWT",
    "taskStateId": 1,
    "priority": 1,
    "startDate": "2025-02-01",
    "endDate": "2025-02-07",
    "budgetHours": 16,
    "isBillable": true,
    "isActive": true
  }'
```

### cURL - Opdater progress

```bash
curl -X POST 'https://app.served.dk/api/project_management/task/Patch' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "id": 201,
    "version": 3,
    "progress": 100,
    "taskStateId": 3
  }'
```

### cURL - Hent Gantt data

```bash
curl -X POST 'https://app.served.dk/api/project_management/task/Gantt' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "filter": { "projectId": 101 }
  }'
```
