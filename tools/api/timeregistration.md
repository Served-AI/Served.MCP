# Time Registration Tool

Håndter tidsregistrering i Served platformen.

## Base URL

```
/api/registration/timeregistration
```

## Endpoints

### GetKeys - Hent registrering IDs

```http
POST /api/registration/timeregistration/GetKeys
```

**Request:**
```json
{
  "filter": {
    "userId": 10,
    "startDate": "2025-01-01",
    "endDate": "2025-01-31"
  },
  "sort": {
    "field": "date",
    "direction": "desc"
  }
}
```

**Response:**
```json
[401, 402, 403, 404, 405]
```

---

### Get - Hent registreringer

```http
POST /api/registration/timeregistration/Get
```

**Request:**
```json
{
  "filter": {
    "userId": 10,
    "projectId": 101
  }
}
```

**Response:**
```json
[
  {
    "id": 401,
    "version": 1,
    "employeeId": 10,
    "tenantId": 1,
    "projectId": 101,
    "taskId": 201,
    "date": "2025-01-20",
    "hours": 7.5,
    "minutes": 0,
    "billable": true,
    "billableHours": 7.5,
    "billableMinutes": 0,
    "factor": 100,
    "comment": "Udvikling af login-modul",
    "additionalComment": null,
    "approvalStatus": "Approved",
    "timeTrackingFormat": "Hours"
  }
]
```

---

### Group - Hent grupperet

```http
POST /api/registration/timeregistration/Group
```

**Request:**
```json
{
  "filter": {
    "userId": 10,
    "startDate": "2025-01-01",
    "endDate": "2025-01-31"
  },
  "groupBy": "date[W]",
  "sort": {
    "field": "date",
    "direction": "asc"
  }
}
```

Gruppér efter:
- `date[D]` - Dag
- `date[W]` - Uge
- `date[M]` - Måned
- `date[Y]` - År
- `projectId` - Projekt
- `taskId` - Task

---

### Save - Opret/opdater registrering

```http
POST /api/registration/timeregistration/Save
```

**Request (opret):**
```json
{
  "tenantId": 1,
  "taskId": 201,
  "date": "2025-01-21",
  "hours": 4.0,
  "minutes": 30,
  "billable": true,
  "billableHours": 4.0,
  "billableMinutes": 30,
  "factor": 100,
  "comment": "Frontend udvikling",
  "timeTrackingFormat": "Hours"
}
```

**Request (opdater):**
```json
{
  "id": 401,
  "tenantId": 1,
  "taskId": 201,
  "date": "2025-01-21",
  "hours": 5.0,
  "minutes": 0,
  "billable": true,
  "comment": "Frontend udvikling - udvidet"
}
```

**Response:**
```json
{
  "id": 406,
  "version": 1,
  "employeeId": 10,
  "taskId": 201,
  "date": "2025-01-21",
  "hours": 4.0,
  "minutes": 30
}
```

---

### Delete - Slet registrering

```http
DELETE /api/registration/timeregistration/Delete
```

**Request:**
```json
{
  "ids": [406]
}
```

---

### GetSuggestions - AI-forslag til tid

```http
GET /api/registration/timeregistration/suggestions?startDate={date}&endDate={date}
```

**Query params:**
- `startDate` - Fra dato
- `endDate` - Til dato
- `includeCalendar` - Inkluder kalenderevents (default: true)
- `minConfidence` - Minimum konfidensværdi (default: 0.5)

**Response:**
```json
{
  "suggestions": [
    {
      "taskId": 201,
      "date": "2025-01-21",
      "suggestedHours": 4.0,
      "description": "Design mockups",
      "reason": "Du arbejder typisk 4 timer på denne opgave om tirsdagen",
      "confidence": 0.85
    }
  ]
}
```

---

### GetPatterns - Tidsmønstre

```http
GET /api/registration/timeregistration/patterns?lookbackDays={days}
```

Analyserer brugerens arbejdsmønstre.

**Query params:**
- `lookbackDays` - Antal dage at analysere (default: 90)

---

### AcceptSuggestion - Accepter AI-forslag

```http
POST /api/registration/timeregistration/accept-suggestion
```

Opretter en tidsregistrering baseret på et AI-forslag.

**Request:**
```json
{
  "taskId": 201,
  "date": "2025-01-21",
  "suggestedHours": 4.0,
  "description": "Design mockups",
  "reason": "Du arbejder typisk 4 timer på denne opgave"
}
```

---

## Felter

| Felt | Type | Beskrivelse | Påkrævet |
|------|------|-------------|----------|
| id | int | Registrering ID | Nej (auto) |
| version | int | Optimistic locking | Ved update |
| tenantId | int | Organisation ID | Ja |
| userId | int | Bruger ID | Nej (auto) |
| employeeId | int | Medarbejder ID | Læs |
| projectId | int | Projekt ID | Læs |
| taskId | int | Task ID | Ja |
| date | date | Dato | Ja |
| hours | double | Timer | Ja |
| minutes | int | Minutter | Nej |
| billable | bool | Fakturerbar | Ja |
| billableHours | double | Fakturerbare timer | Nej |
| billableMinutes | int | Fakturerbare minutter | Nej |
| factor | double | Faktor (normalt 100) | Nej |
| comment | string | Kommentar | Nej |
| additionalComment | string | Ekstra kommentar | Nej |
| approvalStatus | enum | Godkendelsesstatus | Læs |
| timeTrackingFormat | enum | Format | Nej |
| startDate | datetime | Start (ved time-range) | Nej |
| endDate | datetime | Slut (ved time-range) | Nej |
| monthlyPeriod | string | Månedlig periode | Nej |
| stopWatchId | int | Stopwatch reference | Nej |

## ApprovalStatus

| Værdi | Beskrivelse |
|-------|-------------|
| `Pending` | Afventer godkendelse |
| `Approved` | Godkendt |
| `Rejected` | Afvist |

## TimeTrackingFormat

| Værdi | Beskrivelse |
|-------|-------------|
| `Hours` | Timer og minutter |
| `TimeRange` | Start- og sluttid |

## Eksempler

### cURL - Opret tidsregistrering

```bash
curl -X POST 'https://app.served.dk/api/registration/timeregistration/Save' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "tenantId": 1,
    "taskId": 201,
    "date": "2025-01-21",
    "hours": 7.5,
    "billable": true,
    "billableHours": 7.5,
    "factor": 100,
    "comment": "Backend API udvikling",
    "timeTrackingFormat": "Hours"
  }'
```

### cURL - Hent ugens registreringer

```bash
curl -X POST 'https://app.served.dk/api/registration/timeregistration/Get' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "filter": {
      "startDate": "2025-01-20",
      "endDate": "2025-01-26"
    }
  }'
```

### cURL - Hent grupperet per projekt

```bash
curl -X POST 'https://app.served.dk/api/registration/timeregistration/Group' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "filter": {
      "startDate": "2025-01-01",
      "endDate": "2025-01-31"
    },
    "groupBy": "projectId"
  }'
```

### cURL - Hent AI-forslag

```bash
curl -X GET 'https://app.served.dk/api/registration/timeregistration/suggestions?startDate=2025-01-21&endDate=2025-01-25' \
  -H 'Authorization: Bearer <TOKEN>'
```
