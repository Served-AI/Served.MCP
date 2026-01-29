# Finance Tool

Håndter fakturaer og økonomi i Served platformen.

## Base URL

```
/api/finance/invoice
```

## Endpoints

### GetKeys - Hent faktura IDs

```http
POST /api/finance/invoice/GetKeys
```

**Permission:** `Invoices.View`

**Request:**
```json
{
  "filter": {
    "invoiceStatus": "Unpaid",
    "customerId": 123
  },
  "sort": {
    "field": "invoiceDate",
    "direction": "desc"
  },
  "skip": 0,
  "take": 50
}
```

**Response:**
```json
["guid-1", "guid-2", "guid-3"]
```

---

### GetRange - Hent fakturaer

```http
POST /api/finance/invoice/GetRange
```

**Permission:** `Invoices.View`

**Request:**
```json
["guid-1", "guid-2", "guid-3"]
```

**Response:**
```json
[
  {
    "id": "a1b2c3d4-...",
    "invoiceId": 501,
    "version": 1,
    "invoiceNo": "INV-2025-001",
    "invoiceDate": "2025-01-15",
    "dueDate": "2025-02-14",
    "customerId": 123,
    "projectId": 101,
    "startDate": "2025-01-01",
    "endDate": "2025-01-31",
    "netAmount": 50000,
    "amount": 62500,
    "currencyId": 1,
    "currencyRate": 1.0,
    "invoiceStatus": "Sent",
    "invoiceType": "Standard",
    "header": "Faktura for januar 2025",
    "message": "Betaling inden 30 dage",
    "invoiceLines": [
      {
        "description": "Konsulentarbejde",
        "quantity": 50,
        "unitPrice": 1000,
        "amount": 50000,
        "vatRate": 25
      }
    ]
  }
]
```

---

### GetGrouping - Hent fakturaer grupperet

```http
POST /api/finance/invoice/GetGrouping
```

**Permission:** `Invoices.View`

**Request:**
```json
{
  "groupBy": "invoiceStatus",
  "filter": {
    "startDate": "2025-01-01",
    "endDate": "2025-12-31"
  }
}
```

---

### Create - Opret faktura

```http
POST /api/finance/invoice/Create
```

**Permission:** `Invoices.Create`

**Request:**
```json
{
  "customerId": 123,
  "projectId": 101,
  "locationId": 1,
  "invoiceDate": "2025-01-20",
  "dueDate": "2025-02-19",
  "startDate": "2025-01-01",
  "endDate": "2025-01-31",
  "header": "Faktura for konsulentarbejde",
  "message": "Betales inden forfaldsdato",
  "internalNote": "Intern note til regnskab",
  "currencyId": 1,
  "currencyRate": 1.0,
  "defaultDiscount": 0,
  "invoiceStatus": "Draft",
  "invoiceType": "Standard",
  "invoiceLines": [
    {
      "description": "Udvikling - uge 3",
      "quantity": 37.5,
      "unitPrice": 1200,
      "amount": 45000,
      "vatRate": 25
    },
    {
      "description": "Design arbejde",
      "quantity": 10,
      "unitPrice": 1000,
      "amount": 10000,
      "vatRate": 25
    }
  ]
}
```

**Response:**
```json
502
```

---

### Update - Opdater faktura

```http
POST /api/finance/invoice/Update
```

**Permission:** `Invoices.Edit`

**Request:**
```json
{
  "id": "a1b2c3d4-...",
  "version": 1,
  "invoiceStatus": "Sent",
  "dueDate": "2025-02-28",
  "message": "Opdateret betalingsbetingelser"
}
```

---

### UpdateMultiple - Batch opdater

```http
PATCH /api/finance/invoice/UpdateMultiple
```

**Permission:** `Invoices.Edit`

**Request:**
```json
{
  "tenantId": 1,
  "items": [
    { "id": "guid-1", "version": 1, "invoiceStatus": "Paid" },
    { "id": "guid-2", "version": 2, "invoiceStatus": "Paid" }
  ]
}
```

---

### Delete - Slet faktura

```http
DELETE /api/finance/invoice/Delete
```

**Permission:** `Invoices.Delete`

**Request:**
```json
{
  "ids": ["guid-3"]
}
```

---

### GetPdf - Download PDF

```http
GET /api/finance/invoice/{id}/pdf
```

**Permission:** `Invoices.View`

**Response:** PDF fil

---

## Felter

| Felt | Type | Beskrivelse | Påkrævet |
|------|------|-------------|----------|
| id | guid | Faktura GUID (ClaimId) | Nej (auto) |
| invoiceId | int | Intern faktura ID | Læs |
| version | int | Optimistic locking | Ved update |
| invoiceNo | string | Fakturanummer | Nej |
| invoiceNoExternal | string | Eksternt nummer | Nej |
| invoiceDate | date | Fakturadato | Ja |
| dueDate | date | Forfaldsdato | Nej |
| startDate | date | Periode start | Ja |
| endDate | date | Periode slut | Ja |
| customerId | int | Kunde ID | Nej |
| projectId | int | Projekt ID | Nej |
| locationId | int | Lokation ID | Nej |
| currencyId | int | Valuta ID | Nej |
| currencyRate | double | Valutakurs | Nej |
| netAmount | double | Nettobeløb | Læs |
| amount | double | Totalbeløb | Læs |
| defaultDiscount | double | Standard rabat | Nej |
| roundingFraction | double | Afrunding | Nej |
| header | string | Overskrift | Nej |
| message | string | Besked til kunde | Nej |
| internalNote | string | Intern note | Nej |
| purchaseNo | string | Indkøbsnummer | Nej |
| invoiceStatus | enum | Status | Ja |
| invoiceType | enum | Type | Ja |
| invoiceLines | array | Fakturalinjer | Nej |
| isActive | bool | Aktiv | Nej |
| createdDate | datetime | Oprettet | Læs |

## InvoiceStatus

| Værdi | Beskrivelse |
|-------|-------------|
| `Draft` | Kladde |
| `Sent` | Sendt |
| `Paid` | Betalt |
| `Overdue` | Forfalden |
| `Cancelled` | Annulleret |
| `Credited` | Krediteret |

## InvoiceType

| Værdi | Beskrivelse |
|-------|-------------|
| `Standard` | Standard faktura |
| `Credit` | Kreditnota |
| `Proforma` | Proforma faktura |

## Invoice Line

| Felt | Type | Beskrivelse |
|------|------|-------------|
| description | string | Beskrivelse |
| quantity | double | Antal |
| unitPrice | double | Enhedspris |
| amount | double | Beløb |
| vatRate | double | Momssats |
| discount | double | Rabat |

## Eksempler

### cURL - Opret faktura

```bash
curl -X POST 'https://app.served.dk/api/finance/invoice/Create' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "customerId": 123,
    "projectId": 101,
    "invoiceDate": "2025-01-20",
    "dueDate": "2025-02-19",
    "startDate": "2025-01-01",
    "endDate": "2025-01-31",
    "header": "Konsulentarbejde januar 2025",
    "invoiceStatus": "Draft",
    "invoiceType": "Standard",
    "invoiceLines": [
      {
        "description": "Udvikling",
        "quantity": 40,
        "unitPrice": 1200,
        "amount": 48000,
        "vatRate": 25
      }
    ]
  }'
```

### cURL - Hent ubetalte fakturaer

```bash
curl -X POST 'https://app.served.dk/api/finance/invoice/GetKeys' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "filter": { "invoiceStatus": "Sent" },
    "sort": { "field": "dueDate", "direction": "asc" }
  }'
```

### cURL - Marker som betalt

```bash
curl -X PATCH 'https://app.served.dk/api/finance/invoice/UpdateMultiple' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "tenantId": 1,
    "items": [
      { "id": "a1b2c3d4-...", "version": 1, "invoiceStatus": "Paid" }
    ]
  }'
```

### cURL - Download PDF

```bash
curl -X GET 'https://app.served.dk/api/finance/invoice/a1b2c3d4-.../pdf' \
  -H 'Authorization: Bearer <TOKEN>' \
  -o 'faktura.pdf'
```

---

## Relaterede Endpoints

### Billing Summary

```
GET /api/finance/billing-summary/unbilled-time
GET /api/finance/billing-summary/invoice-status
GET /api/finance/billing-summary/revenue-chart
```

### Invoice Generation

```
POST /api/finance/invoice-generation/preview
POST /api/finance/invoice-generation/generate
GET /api/finance/invoice-generation/unbilled-time
POST /api/finance/invoice-generation/validate
```

### Currency

```
GET /api/finance/currency/GetAll
POST /api/finance/currency/Enable
POST /api/finance/currency/SetAsDefault
```
