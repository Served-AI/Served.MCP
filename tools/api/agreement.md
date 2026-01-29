# Agreement Tool

Håndter aftaler/bookinger i Served platformen.

## Base URL

```
/api/calendar/agreement
```

## Endpoints

### Get - Hent aftale

```http
GET /api/calendar/agreement/Get?id={id}
```

**Response:**
```json
{
  "id": 301,
  "version": 2,
  "tenantId": 1,
  "locationId": 1,
  "title": "Møde med kunde",
  "task": "Gennemgang af projektplan og budget",
  "startDate": "2025-01-20T10:00:00",
  "endDate": "2025-01-20T11:30:00",
  "tags": "møde,kunde,projektplan",
  "customerId": 123,
  "calendarId": 1,
  "serviceId": null,
  "addressId": null,
  "users": [10, 11],
  "backgroundColor": "#3498db",
  "createdById": 10,
  "createdDate": "2025-01-15T09:00:00"
}
```

---

### GetKeys - Hent aftale IDs

```http
POST /api/calendar/agreement/GetKeys
```

**Request:**
```json
{
  "startDate": "2025-01-01",
  "endDate": "2025-01-31",
  "userIds": [10, 11],
  "locationId": 1
}
```

**Response:**
```json
[301, 302, 303, 304, 305]
```

---

### Create - Opret aftale

```http
POST /api/calendar/agreement/Create
```

**Request:**
```json
{
  "tenantId": 1,
  "locationId": 1,
  "title": "Projektmøde",
  "task": "Status på udvikling og næste skridt",
  "startDate": "2025-01-25T14:00:00",
  "endDate": "2025-01-25T15:00:00",
  "tags": "møde,status",
  "customerId": 123,
  "calendarId": 1,
  "users": [10, 11, 12]
}
```

**Response:**
```json
306
```

---

### Update - Opdater aftale

```http
POST /api/calendar/agreement/Update
```

**Request:**
```json
{
  "id": 301,
  "version": 2,
  "tenantId": 1,
  "locationId": 1,
  "title": "Møde med kunde - Udskudt",
  "task": "Gennemgang af projektplan og budget",
  "startDate": "2025-01-21T10:00:00",
  "endDate": "2025-01-21T11:30:00",
  "users": [10, 11]
}
```

---

### Delete - Slet aftaler

```http
DELETE /api/calendar/agreement/Delete
```

**Request:**
```json
[
  { "id": 306, "version": 1 }
]
```

---

## Felter

| Felt | Type | Beskrivelse | Påkrævet |
|------|------|-------------|----------|
| id | int | Aftale ID | Nej (auto) |
| version | int | Optimistic locking | Ved update |
| tenantId | int | Organisation ID | Ja |
| locationId | int | Lokation ID | Nej |
| title | string | Titel (max 255) | Ja |
| task | string | Beskrivelse (max 4000) | Nej |
| startDate | datetime | Starttidspunkt | Ja |
| endDate | datetime | Sluttidspunkt | Ja |
| tags | string | Tags (max 1000) | Nej |
| customerId | int | Kunde ID | Nej |
| calendarId | int | Kalender ID | Nej |
| serviceId | int | Service/ydelse ID | Nej |
| addressId | int | Adresse ID | Nej |
| users | int[] | Deltagere (bruger IDs) | Nej |
| backgroundColor | string | Baggrundsfarve (hex) | Læs |
| createdById | int | Oprettet af | Læs |
| createdDate | datetime | Oprettelsesdato | Læs |
| updatedById | int | Opdateret af | Læs |
| updatedDate | datetime | Opdateringsdato | Læs |

## Filtrering

### AgreementFilter felter

| Felt | Type | Beskrivelse |
|------|------|-------------|
| startDate | date | Fra dato |
| endDate | date | Til dato |
| userIds | int[] | Filtrer på brugere |
| locationId | int | Filtrer på lokation |
| customerId | int | Filtrer på kunde |
| calendarId | int | Filtrer på kalender |

## Eksempler

### cURL - Opret aftale

```bash
curl -X POST 'https://app.served.dk/api/calendar/agreement/Create' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "tenantId": 1,
    "locationId": 1,
    "title": "Kundemøde - Novo Nordisk",
    "task": "Gennemgang af Q1 leverancer",
    "startDate": "2025-02-01T09:00:00",
    "endDate": "2025-02-01T10:30:00",
    "customerId": 123,
    "users": [10, 11]
  }'
```

### cURL - Hent aftaler for uge

```bash
curl -X POST 'https://app.served.dk/api/calendar/agreement/GetKeys' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "startDate": "2025-01-20",
    "endDate": "2025-01-26",
    "userIds": [10]
  }'
```

### cURL - Flyt aftale

```bash
curl -X POST 'https://app.served.dk/api/calendar/agreement/Update' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "id": 301,
    "version": 2,
    "tenantId": 1,
    "title": "Kundemøde - Novo Nordisk",
    "startDate": "2025-02-03T09:00:00",
    "endDate": "2025-02-03T10:30:00"
  }'
```
