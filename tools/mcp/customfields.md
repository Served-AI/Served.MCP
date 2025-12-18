# Custom Fields Tools

Håndter brugerdefinerede felter via MCP.

Custom fields gør det muligt at tilføje ekstra data til projekter, opgaver, kunder og andre entiteter i systemet.

---

## Oversigt

Custom fields er organiseret i tre niveauer:

1. **Sections** - Grupper af felter (f.eks. "Projektinfo", "Teknisk data")
2. **Definitions** - Feltdefinitioner (navn, type, validering)
3. **Values** - Faktiske værdier for hver entitet

---

## GetCustomFieldDefinitions

Hent alle tilgængelige custom field definitioner for en domæntype.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| domainType | string | Nej | Filtrér på domæntype |
| sectionId | int | Nej | Filtrér på sektion |

### Domain Types

| Værdi | Beskrivelse |
|-------|-------------|
| Project | Projekter |
| Task | Opgaver |
| Customer | Kunder |
| Agreement | Aftaler |
| Invoice | Fakturaer |
| Employee | Medarbejdere |

### Request - Alle definitioner

```json
{
  "tool": "GetCustomFieldDefinitions",
  "tenantId": 1
}
```

### Request - Kun projekt-felter

```json
{
  "tool": "GetCustomFieldDefinitions",
  "tenantId": 1,
  "domainType": "Project"
}
```

### Response

```json
[
  {
    "id": 1,
    "stringId": "project_category",
    "sectionId": 1,
    "label": "Projektkategori",
    "dataType": "Dropdown",
    "domainType": "Project",
    "isRequired": true,
    "isReadOnly": false,
    "configuration": "{\"options\": [\"Intern\", \"Kunde\", \"R&D\"]}",
    "placeholder": "Vælg kategori",
    "description": "Angiv projektets hovedkategori"
  },
  {
    "id": 2,
    "stringId": "estimated_budget",
    "sectionId": 1,
    "label": "Estimeret budget",
    "dataType": "Number",
    "domainType": "Project",
    "isRequired": false,
    "configuration": "{\"min\": 0, \"currency\": \"DKK\"}",
    "placeholder": "Indtast beløb"
  },
  {
    "id": 3,
    "stringId": "complexity_level",
    "sectionId": 2,
    "label": "Kompleksitetsniveau",
    "dataType": "Dropdown",
    "domainType": "Task",
    "isRequired": false,
    "configuration": "{\"options\": [\"Lav\", \"Medium\", \"Høj\", \"Kritisk\"]}"
  }
]
```

### Data Types

| DataType | Beskrivelse | Eksempel værdi |
|----------|-------------|----------------|
| Text | Fritekst | "En beskrivelse" |
| Number | Numerisk værdi | "12345" |
| Date | Dato | "2025-03-15" |
| DateTime | Dato og tid | "2025-03-15T14:30:00" |
| Dropdown | Valgmuligheder | "Option1" |
| Checkbox | Boolean | "true" eller "false" |
| MultiSelect | Flere valg | "Option1,Option2" |
| User | Bruger reference | "42" (userId) |
| Link | URL | "https://example.com" |

---

## GetEntityCustomFields

Hent custom field værdier for en specifik entitet.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| domainType | string | Ja | Entitetstype |
| entityId | int | Ja | Entitets ID |

### Request

```json
{
  "tool": "GetEntityCustomFields",
  "tenantId": 1,
  "domainType": "Project",
  "entityId": 101
}
```

### Response

```json
{
  "tenantId": 1,
  "domainType": "Project",
  "entityId": 101,
  "fields": [
    {
      "definitionId": 1,
      "stringId": "project_category",
      "label": "Projektkategori",
      "dataType": "Dropdown",
      "isRequired": true,
      "isReadOnly": false,
      "configuration": "{\"options\": [\"Intern\", \"Kunde\", \"R&D\"]}",
      "value": "Kunde",
      "valueId": 501
    },
    {
      "definitionId": 2,
      "stringId": "estimated_budget",
      "label": "Estimeret budget",
      "dataType": "Number",
      "isRequired": false,
      "value": "150000",
      "valueId": 502
    },
    {
      "definitionId": 5,
      "stringId": "risk_assessment",
      "label": "Risikovurdering",
      "dataType": "Text",
      "isRequired": false,
      "value": null,
      "valueId": null
    }
  ]
}
```

### Note

Felter uden værdi har `value: null` og `valueId: null`.

---

## SetCustomFieldValue

Sæt en custom field værdi for en entitet.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| domainType | string | Ja | Entitetstype |
| entityId | int | Ja | Entitets ID |
| definitionId | int | Ja | Field definition ID |
| value | string | Nej | Ny værdi (null for at slette) |

### Request - Sæt værdi

```json
{
  "tool": "SetCustomFieldValue",
  "tenantId": 1,
  "domainType": "Project",
  "entityId": 101,
  "definitionId": 1,
  "value": "Intern"
}
```

### Request - Slet værdi

```json
{
  "tool": "SetCustomFieldValue",
  "tenantId": 1,
  "domainType": "Project",
  "entityId": 101,
  "definitionId": 2,
  "value": null
}
```

### Response

```
Custom field værdi opdateret.
```

---

## BulkSetCustomFieldValues

Sæt flere custom field værdier på én gang.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| domainType | string | Ja | Entitetstype |
| entityId | int | Ja | Entitets ID |
| values | array | Ja | Array af felt-værdier |

### Values Format

Hver værdi kan identificeres med enten `definitionId` eller `stringId`:

```json
{
  "fieldIdentifier": "project_category",
  "value": "Kunde"
}
```

eller

```json
{
  "fieldIdentifier": "1",
  "value": "Kunde"
}
```

### Request

```json
{
  "tool": "BulkSetCustomFieldValues",
  "tenantId": 1,
  "domainType": "Project",
  "entityId": 101,
  "values": [
    { "fieldIdentifier": "project_category", "value": "Kunde" },
    { "fieldIdentifier": "estimated_budget", "value": "150000" },
    { "fieldIdentifier": "risk_assessment", "value": "Medium risiko pga. ny teknologi" }
  ]
}
```

### Response

```
3 custom field værdier opdateret.
```

---

## Workflow Eksempler

### Opret projekt med custom fields

```
Bruger: "Opret et nyt kundeprojekt med budget 200.000 kr"

AI:
1. GetUserContext() → tenantId: 1
2. GetCustomFieldDefinitions(tenantId: 1, domainType: "Project")
   → Finder project_category (id: 1) og estimated_budget (id: 2)
3. CreateProject(tenantId: 1, name: "Nyt kundeprojekt") → projectId: 105
4. BulkSetCustomFieldValues(
     tenantId: 1,
     domainType: "Project",
     entityId: 105,
     values: [
       { "fieldIdentifier": "project_category", "value": "Kunde" },
       { "fieldIdentifier": "estimated_budget", "value": "200000" }
     ]
   )

"Projekt 'Nyt kundeprojekt' oprettet med:
- Kategori: Kunde
- Budget: 200.000 kr"
```

### Læs og vis custom fields

```
Bruger: "Vis alle detaljer om projekt 101"

AI:
1. GetProjectDetails(tenantId: 1, projectId: 101)
2. GetEntityCustomFields(tenantId: 1, domainType: "Project", entityId: 101)

"Projekt: Website Redesign (101)

Standard felter:
- Progress: 45%
- Periode: 2025-01-01 til 2025-06-30

Custom felter:
- Projektkategori: Kunde
- Estimeret budget: 150.000 kr
- Risikovurdering: Medium"
```

### Opdater enkelt custom field

```
Bruger: "Sæt budget til 175.000 på projekt 101"

AI:
1. GetCustomFieldDefinitions(tenantId: 1, domainType: "Project")
   → Finder estimated_budget (stringId: estimated_budget)
2. SetCustomFieldValue(
     tenantId: 1,
     domainType: "Project",
     entityId: 101,
     definitionId: 2,
     value: "175000"
   )

"Budget opdateret til 175.000 kr"
```

### Tilføj custom fields til opgave

```
Bruger: "Sæt kompleksitet til Høj på opgave 201"

AI:
1. GetCustomFieldDefinitions(tenantId: 1, domainType: "Task")
   → Finder complexity_level
2. SetCustomFieldValue(
     tenantId: 1,
     domainType: "Task",
     entityId: 201,
     definitionId: 3,
     value: "Høj"
   )

"Kompleksitetsniveau sat til Høj"
```

---

## Fejlhåndtering

### Field definition ikke fundet

```json
{
  "success": false,
  "error": "Custom field definition not found"
}
```

### Ugyldig værdi for dropdown

```json
{
  "success": false,
  "error": "Value 'Invalid' is not valid for dropdown field 'project_category'. Valid options: Intern, Kunde, R&D"
}
```

### Påkrævet felt mangler

```json
{
  "success": false,
  "error": "Required field 'project_category' is missing"
}
```

### Ingen custom fields defineret

```json
{
  "fields": []
}
```

---

## Best Practices

### 1. Hent definitioner først

Før du sætter værdier, hent altid definitionerne for at kende:
- Tilgængelige felter
- Data typer og validering
- Dropdown muligheder

### 2. Brug stringId når muligt

StringId er mere stabil end definitionId og ændres ikke ved reimport.

### 3. Bulk operations

Brug `BulkSetCustomFieldValues` frem for flere `SetCustomFieldValue` kald når du skal sætte flere felter.

### 4. Dropdown validering

Check at værdien er en gyldig option før du sender den:

```json
// Configuration: {"options": ["Intern", "Kunde", "R&D"]}
// Gyldige værdier: "Intern", "Kunde", "R&D"
```

### 5. Dato formater

Brug altid ISO 8601 format:
- Date: `"2025-03-15"`
- DateTime: `"2025-03-15T14:30:00"`
