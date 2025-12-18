# Employees Tool

Hent team medlemmer via MCP.

---

## GetEmployees

Hent medarbejdere/team medlemmer i et workspace.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| activeOnly | bool | Nej | Kun aktive medarbejdere (default: true) |

### Request - Kun aktive

```json
{
  "tool": "GetEmployees",
  "tenantId": 1
}
```

### Request - Alle (inkl. inaktive)

```json
{
  "tool": "GetEmployees",
  "tenantId": 1,
  "activeOnly": false
}
```

### Response

```json
{
  "success": true,
  "count": 5,
  "employees": [
    {
      "userId": 10,
      "email": "john@example.com",
      "fullName": "John Doe",
      "initials": "JD",
      "position": "Lead Developer",
      "employeeType": "Fuldtid",
      "accessLevel": "Admin",
      "isActive": true
    },
    {
      "userId": 11,
      "email": "jane@example.com",
      "fullName": "Jane Smith",
      "initials": "JS",
      "position": "Designer",
      "employeeType": "Fuldtid",
      "accessLevel": "Standard",
      "isActive": true
    },
    {
      "userId": 12,
      "email": "bob@example.com",
      "fullName": "Bob Johnson",
      "initials": "BJ",
      "position": "Developer",
      "employeeType": "Deltid",
      "accessLevel": "Standard",
      "isActive": true
    }
  ]
}
```

### Response Felter

| Felt | Type | Beskrivelse |
|------|------|-------------|
| userId | int | Bruger ID |
| email | string | Email adresse |
| fullName | string | Fuldt navn |
| initials | string | Initialer |
| position | string | Stilling |
| employeeType | string | Ansættelsestype |
| accessLevel | string | Adgangsniveau |
| isActive | bool | Aktiv status |

### Access Levels

| Level | Beskrivelse |
|-------|-------------|
| Admin | Fuld adgang til workspace |
| Standard | Normal brugeradgang |
| Limited | Begrænset adgang |
| ReadOnly | Kun læseadgang |

### Employee Types

Typiske værdier:
- Fuldtid
- Deltid
- Konsulent
- Praktikant

---

## Fejlhåndtering

### Ingen medarbejdere

```json
{
  "success": true,
  "count": 0,
  "employees": []
}
```

---

## Workflow Eksempler

### Find team til projekt

```
Bruger: "Hvem kan arbejde på det nye projekt?"

AI:
1. GetEmployees(tenantId: 1)
2. "I har 5 aktive team medlemmer:

   Udviklere:
   - John Doe (Lead Developer)
   - Bob Johnson (Developer)

   Design:
   - Jane Smith (Designer)

   Hvem vil du tilføje til projektet?"
```

### Tjek team kapacitet

```
Bruger: "Hvor mange udviklere har vi?"

AI:
1. GetEmployees(tenantId: 1)
2. [Filter på position]
3. "I har 3 udviklere:
   - 2 fuldtid
   - 1 deltid (Bob Johnson)

   Total kapacitet: ~2.5 FTE"
```

### Find administrator

```
Bruger: "Hvem er admin?"

AI:
1. GetEmployees(tenantId: 1)
2. [Filter på accessLevel: Admin]
3. "John Doe er den eneste administrator i workspace'et."
```
