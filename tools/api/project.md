# Project Tool

Håndter projekter i Served platformen.

## Base URL

```
/api/project_management/project
```

## Endpoints

### GetKeys - Hent projekt IDs

```http
POST /api/project_management/project/GetKeys
```

**Scope:** `Project.Read`
**Permission:** `Projects.View`

**Request:**
```json
{
  "filter": {
    "isActive": true,
    "projectStatusId": 1
  },
  "sort": {
    "field": "startDate",
    "direction": "desc"
  },
  "skip": 0,
  "take": 50
}
```

**Response:**
```json
[101, 102, 103, 104]
```

---

### GetGrouping - Hent projekter grupperet

```http
POST /api/project_management/project/GetGrouping
```

**Scope:** `Project.Read`
**Permission:** `Projects.View`

Henter projekter med gruppering og aggregering.

**Request:**
```json
{
  "groupBy": "projectStatusId",
  "filter": {
    "isActive": true
  },
  "includeStats": true
}
```

---

### Detailed - Hent detaljeret projekt

```http
POST /api/project_management/project/Detailed?projectId={id}
```

**Scope:** `Project.Read`
**Permission:** `Projects.View`

**Response:**
```json
{
  "id": 101,
  "version": 5,
  "name": "Website Redesign",
  "fullName": "Kunde A - Website Redesign 2025",
  "description": "Komplet redesign af kundens website",
  "projectNo": "PRJ-2025-001",
  "customerId": 123,
  "projectTypeId": 1,
  "projectStatusId": 2,
  "projectStageId": 1,
  "projectCategogyId": 3,
  "startDate": "2025-01-01",
  "endDate": "2025-06-30",
  "isActive": true,
  "progress": 35,
  "projectBudgetHours": 500,
  "projectBudgetAmount": 250000,
  "regHours": 175,
  "regAmount": 87500,
  "projectManagerId": 10,
  "memberIds": [10, 11, 12, 13]
}
```

---

### Create - Opret projekt

```http
POST /api/project_management/project/Create
```

**Scope:** `Project.Write`
**Permission:** `Projects.Create`

**Request:**
```json
{
  "tenantId": 1,
  "name": "Nyt Projekt",
  "fullName": "Kunde B - Nyt Projekt Q1 2025",
  "description": "Projektbeskrivelse her",
  "projectNo": "PRJ-2025-002",
  "customerId": 124,
  "projectTypeId": 1,
  "projectStatusId": 1,
  "projectStageId": 1,
  "startDate": "2025-02-01",
  "endDate": "2025-04-30",
  "isActive": true,
  "projectBudgetHours": 200,
  "projectBudgetAmount": 100000,
  "projectManagerId": 10,
  "memberIds": [10, 11]
}
```

**Response:**
```json
102
```

---

### Update - Opdater projekt

```http
POST /api/project_management/project/Update
```

**Scope:** `Project.Write`
**Permission:** `Projects.Edit`

**Request:**
```json
{
  "id": 101,
  "version": 5,
  "name": "Website Redesign - Fase 2",
  "endDate": "2025-08-31",
  "projectBudgetHours": 700,
  "progress": 50
}
```

---

### UpdateMultiple - Batch opdater projekter

```http
PATCH /api/project_management/project/UpdateMultiple
```

**Scope:** `Project.Write`
**Permission:** `Projects.Edit`

**Request:**
```json
{
  "tenantId": 1,
  "items": [
    {
      "id": 101,
      "version": 5,
      "projectStatusId": 3
    },
    {
      "id": 102,
      "version": 2,
      "projectStatusId": 3
    }
  ]
}
```

---

### Delete - Slet projekt

```http
DELETE /api/project_management/project/Delete
```

**Scope:** `Project.Write`
**Permission:** `Projects.Delete`

**Request:**
```json
{
  "ids": [103],
  "permanent": false
}
```

---

### Chat - AI chat om projekt

```http
POST /api/project_management/project/Chat
```

**Scope:** `Project.Write`
**Permission:** `Projects.Edit`

Chat med AI om projektet for at få indsigt eller forslag.

**Request:**
```json
{
  "projectId": 101,
  "message": "Hvad er status på dette projekt?"
}
```

---

## Felter

| Felt | Type | Beskrivelse | Påkrævet |
|------|------|-------------|----------|
| id | int | Projekt ID | Nej (auto) |
| version | int | Optimistic locking | Ved update |
| tenantId | int | Organisation ID | Ja |
| name | string | Projektnavn (max 255) | Ja |
| fullName | string | Fuldt navn (max 4000) | Nej |
| description | string | Beskrivelse | Nej |
| projectNo | string | Projektnummer | Nej |
| customerId | int | Kunde ID | Nej |
| projectTypeId | int | Projekttype ID | Ja |
| projectStatusId | int | Status ID | Ja |
| projectStageId | int | Stage ID | Ja |
| projectCategogyId | int | Kategori ID | Nej |
| startDate | date | Startdato | Ja |
| endDate | date | Slutdato | Ja |
| isActive | bool | Aktiv status | Nej |
| progress | double | Fremskridt (0-100) | Nej |
| projectBudgetHours | double | Budget timer | Nej |
| projectBudgetAmount | double | Budget beløb | Nej |
| contractAmount | double | Kontraktbeløb | Nej |
| regHours | double | Registrerede timer | Læs |
| regAmount | double | Registreret beløb | Læs |
| billedHours | double | Fakturerede timer | Læs |
| billedAmount | double | Faktureret beløb | Læs |
| projectManagerId | int | Projektleder ID | Nej |
| approvalManagerId | int | Godkender ID | Nej |
| projectPartnerId | int | Partner ID | Nej |
| parentId | int | Forælder projekt ID | Nej |
| memberIds | int[] | Team medlemmer | Nej |
| tags | string | Tags (komma-separeret) | Nej |
| color | string | Farve (hex) | Nej |
| timeRegistrationModel | enum | Tidsregistreringsmodel | Nej |

## Eksempler

### cURL - Opret projekt

```bash
curl -X POST 'https://app.served.dk/api/project_management/project/Create' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "tenantId": 1,
    "name": "App Udvikling",
    "projectTypeId": 1,
    "projectStatusId": 1,
    "projectStageId": 1,
    "startDate": "2025-01-15",
    "endDate": "2025-05-15",
    "isActive": true,
    "projectBudgetHours": 400,
    "projectManagerId": 10
  }'
```

### cURL - Hent aktive projekter

```bash
curl -X POST 'https://app.served.dk/api/project_management/project/GetKeys' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "filter": { "isActive": true },
    "sort": { "field": "name", "direction": "asc" },
    "take": 100
  }'
```
