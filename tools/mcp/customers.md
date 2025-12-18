# Customers Tools

Håndter kunder via MCP.

---

## GetCustomers

Hent kunder for et workspace.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |

### Request

```json
{
  "tool": "GetCustomers",
  "tenantId": 1
}
```

### Response

```
Her er 3 kunder:

@customer[301] {
  name: "Acme Corporation"
  type: "Erhverv"
  email: "kontakt@acme.dk"
  phone: "+4512345678"
}

@customer[302] {
  name: "John Doe"
  type: "Privat"
  email: "john@example.com"
  phone: "+4587654321"
}

@customer[303] {
  name: "Novo Nordisk A/S"
  type: "Erhverv"
  email: "info@novonordisk.com"
  phone: "Ingen telefon"
}
```

### @customer Reference

Brug `@customer[id]` til at referere til kunder i efterfølgende kald.

---

## CreateCustomer

Opret en ny kunde.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| firstName | string | Ja | Fornavn eller firmanavn |
| lastName | string | Nej | Efternavn |
| email | string | Nej | Email adresse |
| phone | string | Nej | Telefonnummer |

### Request - Virksomhed

```json
{
  "tool": "CreateCustomer",
  "tenantId": 1,
  "firstName": "Tech Solutions",
  "lastName": "ApS",
  "email": "kontakt@techsolutions.dk",
  "phone": "+4533221100"
}
```

### Request - Privat person

```json
{
  "tool": "CreateCustomer",
  "tenantId": 1,
  "firstName": "Anders",
  "lastName": "Andersen",
  "email": "anders@gmail.com",
  "phone": "+4511223344"
}
```

### Response

```
Kunde 'Tech Solutions ApS' oprettet succesfuldt med ID: 304
```

### Note

Kundetype (Person/Erhverv) sættes automatisk baseret på data. For at specificere CVR nummer eller andre erhvervsfelter, brug REST API.

---

## UpdateCustomer

Opdater en eksisterende kunde.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| customerId | int | Ja | Kunde ID |
| firstName | string | Nej | Nyt fornavn/firmanavn |
| lastName | string | Nej | Nyt efternavn |
| email | string | Nej | Ny email |
| phone | string | Nej | Nyt telefonnummer |

### Request

```json
{
  "tool": "UpdateCustomer",
  "tenantId": 1,
  "customerId": 301,
  "email": "ny-email@acme.dk",
  "phone": "+4599887766"
}
```

### Response

```
Kunde 'Acme Corporation' opdateret succesfuldt.
```

---

## Fejlhåndtering

### Kunde ikke fundet

```
Kunde med ID 999 blev ikke fundet.
```

### Ingen kunder

```
Ingen kunder fundet.
```

---

## Workflow Eksempler

### Opret kunde og tilknyt til projekt

```
1. CreateCustomer(tenantId: 1, firstName: "Ny Kunde") → customerId: 305
2. CreateProject(tenantId: 1, name: "Projekt for Ny Kunde")
3. [Via REST API: Tilknyt customerId til projekt]
```

### Find og opdater kunde

```
1. GetCustomers(tenantId: 1) → Find @customer[301]
2. UpdateCustomer(tenantId: 1, customerId: 301, email: "ny@email.dk")
```

### Søg efter kunde

```
1. GetCustomers(tenantId: 1)
2. AI: Filtrer listen baseret på brugerens søgekriterier
3. AI: "Fandt 2 kunder der matcher 'Novo': @customer[303], @customer[310]"
```
