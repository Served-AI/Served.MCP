# Agreements Tools

Håndter aftaler og bookinger via MCP.

---

## GetAgreements

Hent aftaler for et workspace.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |

### Request

```json
{
  "tool": "GetAgreements",
  "tenantId": 1
}
```

### Response

```
Her er 4 aftaler:

@agreement[401] {
  name: "Kundemøde - Acme"
  dates: "2025-01-20 til 2025-01-20"
  task: "Gennemgang af projektplan"
}

@agreement[402] {
  name: "Sprint Planning"
  dates: "2025-01-22 til 2025-01-22"
  task: "Planlægning af næste sprint"
}

@agreement[403] {
  name: "Workshop"
  dates: "2025-01-25 til 2025-01-25"
  task: "Design workshop med kunden"
}

@agreement[404] {
  name: "Frokostmøde"
  dates: "2025-01-27 til 2025-01-27"
  task: "Ingen opgave"
}
```

### @agreement Reference

Brug `@agreement[id]` til at referere til aftaler.

---

## CreateAgreement

Opret en ny aftale/booking.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| title | string | Ja | Aftaletitel |
| startDate | string | Ja | Start tidspunkt |
| endDate | string | Ja | Slut tidspunkt |
| customerId | int | Nej | Kunde ID |
| task | string | Nej | Beskrivelse/opgave |

### Dato Format

Accepterer:
- `YYYY-MM-DD` (hele dagen)
- `YYYY-MM-DDTHH:mm` (specifikt tidspunkt)

### Request - Heldags aftale

```json
{
  "tool": "CreateAgreement",
  "tenantId": 1,
  "title": "Projektdag",
  "startDate": "2025-02-01",
  "endDate": "2025-02-01",
  "task": "Fokuseret arbejde på projekt X"
}
```

### Request - Møde med tidspunkt

```json
{
  "tool": "CreateAgreement",
  "tenantId": 1,
  "title": "Statusmøde",
  "startDate": "2025-02-03T10:00",
  "endDate": "2025-02-03T11:30",
  "customerId": 301,
  "task": "Ugentligt statusmøde med Acme"
}
```

### Request - Flerdags event

```json
{
  "tool": "CreateAgreement",
  "tenantId": 1,
  "title": "Konference",
  "startDate": "2025-03-10",
  "endDate": "2025-03-12",
  "task": "Tech konference i København"
}
```

### Response

```
Aftale 'Statusmøde' oprettet succesfuldt med ID: 405
```

---

## Fejlhåndtering

### Ugyldigt datoformat

```json
{
  "success": false,
  "error": "Invalid date format"
}
```

### Ingen aftaler

```
Ingen aftaler fundet.
```

---

## Workflow Eksempler

### Book møde med kunde

```
1. GetCustomers(tenantId: 1) → Find @customer[301] "Acme"
2. CreateAgreement(
     tenantId: 1,
     title: "Møde med Acme",
     startDate: "2025-02-05T14:00",
     endDate: "2025-02-05T15:00",
     customerId: 301,
     task: "Gennemgang af Q1 leverancer"
   )
```

### Planlæg projektarbejde

```
1. GetProjects(tenantId: 1) → Find @project[101]
2. CreateAgreement(
     tenantId: 1,
     title: "Fokustid - Website Redesign",
     startDate: "2025-02-06T09:00",
     endDate: "2025-02-06T16:00",
     task: "Fokuseret arbejde på frontend"
   )
```

### Opret serie af møder

```
AI: "Jeg opretter 4 ugentlige statusmøder:"

1. CreateAgreement(title: "Statusmøde uge 6", startDate: "2025-02-03T10:00", ...)
2. CreateAgreement(title: "Statusmøde uge 7", startDate: "2025-02-10T10:00", ...)
3. CreateAgreement(title: "Statusmøde uge 8", startDate: "2025-02-17T10:00", ...)
4. CreateAgreement(title: "Statusmøde uge 9", startDate: "2025-02-24T10:00", ...)
```
