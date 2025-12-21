# Projects Tools

Håndter projekter via MCP.

---

## GetProjects

Hent alle projekter for et workspace.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID fra GetUserContext |

### Request - Standard

```json
{
  "tool": "GetProjects",
  "tenantId": 1
}
```

### Response - Standard

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

@project[102] {
  name: "Mobile App"
  projectNo: "PRJ-2025-002"
  progress: 20%
  status: "I gang"
  dates: "2025-02-01 til 2025-08-31"
  manager: "Jane Smith"
}
```



### @project Reference

Brug `@project[id]` til at referere til projekter i efterfølgende kald.

---

## GetProjectDetails

Hent detaljeret information om et projekt.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| projectId | int | Ja | Projekt ID |

### Request

```json
{
  "tool": "GetProjectDetails",
  "tenantId": 1,
  "projectId": 101
}
```

### Response

```
@project-details[101] {
  name: "Website Redesign"
  projectNo: "PRJ-2025-001"
  description: "Komplet redesign af virksomhedens website"
  status: "I gang"
  progress: 45%
  dates: "2025-01-01 til 2025-06-30"
  manager: "John Doe"
  budgetHours: 500
}
```

---

## CreateProject

Opret et nyt projekt.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| name | string | Ja | Projektnavn |
| parentId | int | Nej | Parent projekt ID (for underprojekter) |
| projectNo | string | Nej | Projektnummer (auto-genereret hvis udeladt) |
| description | string | Nej | Projektbeskrivelse |
| startDate | string | Nej | Start dato (YYYY-MM-DD) |
| endDate | string | Nej | Slut dato (YYYY-MM-DD) |

### Request - Simpelt projekt

```json
{
  "tool": "CreateProject",
  "tenantId": 1,
  "name": "Nyt Projekt",
  "description": "Beskrivelse af projektet",
  "startDate": "2025-02-01",
  "endDate": "2025-05-31",
  "projectNo": "PRJ-2025-003"
}
```

### Request - Underprojekt

```json
{
  "tool": "CreateProject",
  "tenantId": 1,
  "name": "Fase 2: Design",
  "parentId": 101,
  "description": "Designfase for hovedprojekt",
  "startDate": "2025-03-01",
  "endDate": "2025-04-15"
}
```

### Response

```
Projekt 'Nyt Projekt' oprettet succesfuldt med ID: 103 og projektnummer: PRJ-2025-003
```

### Defaults

- `startDate`: Dags dato hvis ikke angivet
- `endDate`: 3 måneder frem hvis ikke angivet
- `projectNo`: Auto-genereret hvis ikke angivet
- `parentId`: null (top-level projekt)

---

## UpdateProject

Opdater et eksisterende projekt.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| projectId | int | Ja | Projekt ID |
| name | string | Nej | Nyt navn |
| parentId | int | Nej | Nyt parent projekt ID |
| description | string | Nej | Ny beskrivelse |
| startDate | string | Nej | Ny start dato |
| endDate | string | Nej | Ny slut dato |

### Request - Opdater flere felter

```json
{
  "tool": "UpdateProject",
  "tenantId": 1,
  "projectId": 101,
  "name": "Website Redesign - Fase 2",
  "endDate": "2025-08-31"
}
```

### Request - Flyt til underprojekt

```json
{
  "tool": "UpdateProject",
  "tenantId": 1,
  "projectId": 103,
  "parentId": 101
}
```

### Request - Flyt til top-level

```json
{
  "tool": "UpdateProject",
  "tenantId": 1,
  "projectId": 103,
  "parentId": null
}
```

### Response

```
Projekt 'Website Redesign - Fase 2' opdateret succesfuldt.
```

### Note

Kun angivne felter opdateres. Udeladte felter forbliver uændrede.

---

## DeleteProject

Slet et projekt.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| projectId | int | Ja | Projekt ID |

### Request

```json
{
  "tool": "DeleteProject",
  "tenantId": 1,
  "projectId": 103
}
```

### Response

```
Projekt med ID 103 slettet succesfuldt.
```

### Advarsel

Sletning af et projekt kan påvirke tilknyttede opgaver og underprojekter.

---


## UpdateProjectsBulk

Opdater flere projekter på én gang. **Kræver brugerbekræftelse**.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| updatesJson | string | Ja | JSON array af opdateringer |

### Update JSON Format

```json
[
  {
    "projectId": 101,
    "name": "Nyt navn",
    "description": "Ny beskrivelse",
    "startDate": "2025-02-01",
    "endDate": "2025-06-30"
  },
  {
    "projectId": 102,
    "endDate": "2025-09-30"
  }
]
```

### Request

```json
{
  "tool": "UpdateProjectsBulk",
  "tenantId": 1,
  "updatesJson": "[{\"projectId\": 101, \"name\": \"Updated\"}, {\"projectId\": 102, \"endDate\": \"2025-09-30\"}]"
}
```

### Response (Bekræftelsesanmodning)

```json
{
  "Action": "update_projects_bulk",
  "Count": 2,
  "Updates": [
    { "ProjectId": 101, "Name": "Updated" },
    { "ProjectId": 102, "EndDate": "2025-09-30" }
  ],
  "RequiresConfirmation": true,
  "ConfirmationMessage": "Vil du opdatere 2 projekter?"
}
```

---

## ExecuteUpdateProjectsBulk

Udfør bulk opdatering af projekter efter bekræftelse.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| updatesJson | string | Ja | Samme JSON som UpdateProjectsBulk |

### Request

```json
{
  "tool": "ExecuteUpdateProjectsBulk",
  "tenantId": 1,
  "updatesJson": "[{\"projectId\": 101, \"name\": \"Updated\"}, {\"projectId\": 102, \"endDate\": \"2025-09-30\"}]"
}
```

### Response

```
Opdateret 2 ud af 2 projekter.

✓ 'Updated' (ID: 101)
✓ 'Website Redesign' (ID: 102)
```

---

### Workflow Eksempler

### Opret projekt med underprojekter

```
1. GetUserContext → tenantId: 1
2. CreateProject(tenantId: 1, name: "Hovedprojekt") → projectId: 101
3. CreateProject(tenantId: 1, name: "Fase 1", parentId: 101) → projectId: 102
4. CreateProject(tenantId: 1, name: "Fase 2", parentId: 101) → projectId: 103
```

### Find og opdater projekt

```
1. GetProjects(tenantId: 1) → Find @project[101]
2. UpdateProject(tenantId: 1, projectId: 101, endDate: "2025-09-30")
```

### Flyt projekt til nyt parent

```
1. GetProjects(tenantId: 1)
2. UpdateProject(tenantId: 1, projectId: 103, parentId: 101)
3. "Projekt 103 er nu et underprojekt af projekt 101"
```

### Hent projektdetaljer

```
Bruger: "Giv mig detaljer om Website projektet"

AI:
1. GetProjects(tenantId: 1)
2. [Bruger vælger projekt 101]
3. GetProjectDetails(tenantId: 1, projectId: 101)
4. "Website Redesign:
   - Progress: 45%
   - Budget: 500 timer
   - Periode: 2025-01-01 til 2025-06-30
   - Projektleder: John Doe"
```

