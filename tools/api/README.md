# Served API Tools

Tool-dokumentation for Served API.

## Tools

| Tool | Beskrivelse | Endpoints |
|------|-------------|-----------|
| [customer.md](customer.md) | Kunder - opret, opdater, søg | 6 endpoints |
| [project.md](project.md) | Projekter - CRUD, budget, team | 7 endpoints |
| [task.md](task.md) | Tasks - CRUD, Gantt, progress | 8 endpoints |
| [agreement.md](agreement.md) | Aftaler/bookinger - kalender | 5 endpoints |
| [timeregistration.md](timeregistration.md) | Tidsregistrering - timer, AI-forslag | 7 endpoints |
| [finance.md](finance.md) | Fakturaer - CRUD, PDF, billing | 7 endpoints |

## Fælles Patterns

### Authentication

Alle endpoints kræver JWT token:

```bash
curl -X POST 'https://app.served.dk/api/...' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json'
```

### Request Filter

Standard filter struktur:

```json
{
  "filter": { "field": "value" },
  "sort": { "field": "name", "direction": "asc" },
  "skip": 0,
  "take": 50
}
```

### Delete Request

Standard delete struktur:

```json
[
  { "id": 123, "version": 1 },
  { "id": 124, "version": 2 }
]
```

### Optimistic Locking

Alle update operationer kræver `version` feltet for at sikre data-integritet.

## Miljøer

| Miljø | Base URL |
|-------|----------|
| Production | `https://app.served.dk` |
| Local Dev | `http://localhost:5010` |
