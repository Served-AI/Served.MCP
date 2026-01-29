# Customer Tool

Håndter kunder i Served platformen.

## Base URL

```
/api/calendar/customer
```

## Endpoints

### Get - Hent kunde

```http
GET /api/calendar/customer/Get?id={id}
```

**Scope:** `Customer.Read`

**Response:**
```json
{
  "id": 123,
  "version": 1,
  "tenantId": 1,
  "locationId": 1,
  "firstName": "John",
  "lastName": "Doe",
  "email": "john@example.com",
  "phone": "+4512345678",
  "note": "VIP kunde",
  "type": "Person",
  "cvr": null,
  "cpr": null,
  "address": {
    "street": "Hovedgaden 1",
    "city": "København",
    "zipCode": "1000",
    "country": "Danmark"
  }
}
```

---

### GetKeys - Hent kunde IDs

```http
POST /api/calendar/customer/GetKeys
```

**Scope:** `Customer.Read`

**Request:**
```json
{
  "filter": {
    "type": "Company"
  },
  "sort": {
    "field": "firstName",
    "direction": "asc"
  },
  "skip": 0,
  "take": 50
}
```

**Response:**
```json
[123, 124, 125, 126]
```

---

### Create - Opret kunde

```http
POST /api/calendar/customer/Create
```

**Scope:** `Customer.Write`

**Request:**
```json
{
  "tenantId": 1,
  "locationId": 1,
  "firstName": "Acme",
  "lastName": "Corporation",
  "email": "kontakt@acme.dk",
  "phone": "+4587654321",
  "note": "Ny erhvervskunde",
  "type": "Company",
  "cvr": "12345678",
  "ean": "5790000000000",
  "att": "Regnskab",
  "address": {
    "street": "Industrivej 10",
    "city": "Aarhus",
    "zipCode": "8000",
    "country": "Danmark"
  }
}
```

**Response:**
```json
123
```

---

### Update - Opdater kunde

```http
POST /api/calendar/customer/Update
```

**Scope:** `Customer.Write`

**Request:**
```json
{
  "id": 123,
  "version": 1,
  "tenantId": 1,
  "locationId": 1,
  "firstName": "Acme",
  "lastName": "Corporation Updated",
  "email": "ny-email@acme.dk",
  "phone": "+4587654321",
  "type": "Company"
}
```

---

### Delete - Slet kunder

```http
DELETE /api/calendar/customer/Delete
```

**Scope:** `Customer.Write`

**Request:**
```json
[
  { "id": 123, "version": 1 },
  { "id": 124, "version": 2 }
]
```

---

### LookUp - Søg kunder

```http
POST /api/calendar/customer/LookUp
```

**Scope:** `Customer.Read`

**Request:**
```json
{
  "filter": {
    "search": "acme"
  },
  "skip": 0,
  "take": 20
}
```

**Response:**
```json
[
  {
    "id": 123,
    "firstName": "Acme",
    "lastName": "Corporation",
    "email": "kontakt@acme.dk",
    "type": "Company"
  }
]
```

---

### GetAgreementGrouping - Hent aftaler for kunde

```http
POST /api/calendar/customer/GetAgreementGrouping
```

**Scope:** `Customer.Read`

Henter aftaler grupperet for en eller flere kunder.

---

## Felter

| Felt | Type | Beskrivelse | Påkrævet |
|------|------|-------------|----------|
| id | int | Kunde ID | Nej (auto) |
| version | int | Optimistic locking version | Ved update |
| tenantId | int | Organisation ID | Ja |
| locationId | int | Lokation ID | Ja |
| firstName | string | Fornavn / Firmanavn | Ja |
| lastName | string | Efternavn | Nej |
| email | string | Email adresse | Nej |
| phone | string | Telefonnummer | Nej |
| note | string | Intern note (max 4000 tegn) | Nej |
| type | enum | `Person` eller `Company` | Ja |
| cvr | string | CVR nummer (virksomheder) | Nej |
| cpr | string | CPR nummer (personer) | Nej |
| ean | string | EAN nummer | Nej |
| att | string | Att. felt | Nej |
| logo | guid | Logo fil reference | Nej |
| addressId | int | Adresse ID | Nej |
| address | object | Adresse objekt | Nej |

## Customer Types

| Værdi | Beskrivelse |
|-------|-------------|
| `Person` | Privat person |
| `Company` | Virksomhed |

## Eksempler

### cURL - Opret virksomhedskunde

```bash
curl -X POST 'https://app.served.dk/api/calendar/customer/Create' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "tenantId": 1,
    "locationId": 1,
    "firstName": "Novo Nordisk",
    "lastName": "A/S",
    "email": "info@novonordisk.com",
    "phone": "+4544442222",
    "type": "Company",
    "cvr": "24256790"
  }'
```

### cURL - Søg kunder

```bash
curl -X POST 'https://app.served.dk/api/calendar/customer/LookUp' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "filter": { "search": "novo" },
    "take": 10
  }'
```
