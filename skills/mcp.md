# Served MCP Skills

Dette dokument giver AI assistenter (som Claude) viden om hvordan man bruger Served MCP tools effektivt.

## Oversigt

Served MCP Server eksponerer tools til at arbejde med:
- **Projekter** - Opret, læs, opdater projekter
- **Opgaver** - Håndter tasks med bulk operationer
- **Kunder** - Kundehåndtering
- **Aftaler** - Kalender og bookinger
- **Tidsregistrering** - AI-drevet tidsforslag
- **Projekt Intelligence** - AI analyse tools

## Quick Start

### 1. Start altid med GetUserContext

```
Kald GetUserContext FØRST for at:
- Identificere brugeren
- Finde tilgængelige workspaces
- Få det korrekte tenantId
```

### 2. Brug tenantId i alle efterfølgende kald

Alle MCP tools kræver `tenantId` parameter. Brug værdien fra GetUserContext.

### 3. Arbejd med @entity[id] references

Tools returnerer data i formatet:
```
@project[101] { name: "...", progress: 45% }
@task[201] { name: "...", priority: "Høj" }
@customer[301] { name: "...", email: "..." }
```

Brug disse IDs i efterfølgende operationer.

---

## Workflow Eksempler

### Eksempel 1: Opret projekt med opgaver

```
1. GetUserContext → Få tenantId
2. CreateProject(tenantId, "Nyt Projekt", ...) → Få projectId
3. CreateTask(tenantId, projectId, "Opgave 1", ...)
4. CreateTask(tenantId, projectId, "Opgave 2", ...)
```

### Eksempel 2: Analyser projektstatus

```
1. GetUserContext → Få tenantId
2. GetProjects(tenantId) → Find relevante projekter
3. AnalyzeProjectHealth(tenantId, projectId) → Få sundhedsrapport
4. GetTasks(tenantId, projectId) → Se opgavestatus
```

### Eksempel 3: Hjælp med tidsregistrering

```
1. GetUserContext → Få tenantId
2. SuggestTimeEntries(tenantId, startDate, endDate) → Få AI-forslag
3. Præsenter forslag for brugeren
4. (Brugeren accepterer/afviser i UI)
```

### Eksempel 4: Planlæg nyt projekt

```
1. GetUserContext → Få tenantId
2. FindSimilarProjects(tenantId, "beskrivelse") → Lær fra historik
3. SuggestTaskDecomposition(tenantId, "feature", "type") → Få opgaveforslag
4. EstimateEffort(tenantId, "beskrivelse", "features") → Få estimat
5. CreateProject(...) og CreateTasksBulk(...) → Opret alt
```

---

## Best Practices

### Altid verificer workspace

```
✅ Korrekt:
1. GetUserContext
2. "Du har adgang til workspace 'Acme Corp' (tenantId: 1). Skal jeg fortsætte med dette?"
3. [Efter bekræftelse] GetProjects(1)

❌ Forkert:
1. GetProjects(1)  // Antager tenantId uden at verificere
```

### Bulk operationer kræver bekræftelse

```
✅ Korrekt:
1. CreateTasksBulk returnerer bekræftelsesanmodning
2. Vis opgaveliste til brugeren
3. [Efter bekræftelse] ExecuteCreateTasksBulk

❌ Forkert:
1. ExecuteCreateTasksBulk uden CreateTasksBulk først
```

### Brug AI-tools til indsigt

```
✅ God praksis:
- Brug AnalyzeProjectHealth til at identificere problemer
- Brug SuggestTaskDecomposition ved nye features
- Brug EstimateEffort før du lover deadlines
- Brug FindSimilarProjects til at lære fra historik
```

---

## Tool Reference

### Data Tools (CRUD)

| Tool | Handling | Parametre |
|------|----------|-----------|
| GetUserContext | Hent bruger og workspaces | - |
| GetProjects | List projekter | tenantId |
| CreateProject | Opret projekt | tenantId, name, ... |
| UpdateProject | Opdater projekt | tenantId, projectId, ... |
| GetTasks | List opgaver | tenantId, projectId |
| CreateTask | Opret opgave | tenantId, projectId, name, ... |
| UpdateTask | Opdater opgave | tenantId, taskId, ... |
| CreateTasksBulk | Bulk opret (med bekræftelse) | tenantId, tasksJson |
| ExecuteCreateTasksBulk | Udfør bulk | tenantId, tasksJson |
| GetCustomers | List kunder | tenantId |
| CreateCustomer | Opret kunde | tenantId, firstName, ... |
| UpdateCustomer | Opdater kunde | tenantId, customerId, ... |
| GetAgreements | List aftaler | tenantId |
| CreateAgreement | Opret aftale | tenantId, title, startDate, endDate |
| GetEmployees | List team | tenantId, activeOnly |

### AI Tools (Intelligence)

| Tool | Formål | Output |
|------|--------|--------|
| SuggestTimeEntries | AI tidsforslag | Forslag med sikkerhedsgrad |
| AnalyzeTimePatterns | Mønsteranalyse | Brugerens arbejdsmønstre |
| SuggestTaskDecomposition | Opgaveopdeling | Forslag baseret på historik |
| EstimateEffort | Estimering | Timer/dage baseret på data |
| AnalyzeProjectHealth | Sundhedscheck | Score, risici, anbefalinger |
| FindSimilarProjects | Søg projekter | Lignende projekter med mønstre |

---

## Prioritet Reference

### Task Priority

| Værdi | Betydning |
|-------|-----------|
| 1 | Kritisk |
| 2 | Høj |
| 3 | Normal (default) |
| 4 | Lav |
| 5 | Meget lav |

### Health Score

| Score | Status | Alert |
|-------|--------|-------|
| 80-100 | Sundt | Grøn |
| 60-79 | I Risiko | Gul |
| 0-59 | Kritisk | Rød |

### Confidence Levels (Time Suggestions)

| Score | Niveau |
|-------|--------|
| >= 0.8 | Høj sikkerhed |
| 0.6-0.79 | Medium sikkerhed |
| < 0.6 | Lav sikkerhed |

---

## Fejlhåndtering

### Almindelige fejl

| Fejl | Årsag | Løsning |
|------|-------|---------|
| "Not authenticated" | Manglende token | Bruger skal logge ind igen |
| "No access to this workspace" | Forkert tenantId | Brug GetUserContext først |
| "Project not found" | Ugyldigt projektId | Verificer med GetProjects |
| "Invalid date format" | Forkert datoformat | Brug YYYY-MM-DD |

### Response format

**Success:**
```json
{
  "success": true,
  "data": { ... }
}
```

**Error:**
```json
{
  "success": false,
  "error": "Beskrivelse af fejl"
}
```

---

## Integration med Claude.ai

### MCP Server Konfiguration

```json
{
  "mcpServers": {
    "served": {
      "url": "https://app.served.dk/mcp",
      "auth": {
        "type": "oauth",
        "clientId": "claude-mcp",
        "scopes": ["projects", "tasks", "customers", "calendar", "timetracking"]
      }
    }
  }
}
```

### Tilgængelige Scopes

| Scope | Adgang |
|-------|--------|
| projects | Projekter (læs/skriv) |
| tasks | Opgaver (læs/skriv) |
| customers | Kunder (læs/skriv) |
| calendar | Aftaler (læs/skriv) |
| timetracking | Tidsregistrering |
| employees | Team (læs) |
| intelligence | AI tools |

---

## Tips til effektiv brug

1. **Batch operationer**: Brug CreateTasksBulk i stedet for mange enkelte CreateTask kald
2. **Cache brugerkontext**: Husk tenantId fra GetUserContext
3. **Udnyt AI tools**: De giver værdifuld indsigt baseret på historiske data
4. **Respekter bekræftelser**: Bulk operationer skal bekræftes af brugeren
5. **Brug @entity references**: De gør det nemt at referere til specifikke objekter
