# Context Tool

Hent brugerens kontekst og tilgængelige workspaces.

## GetUserContext

**Kald denne FØRST** for at identificere brugeren og finde det korrekte workspace.

### Parametre

Ingen parametre.

### Request

```json
{
  "tool": "GetUserContext"
}
```

### Response

```json
{
  "success": true,
  "user": {
    "id": 10,
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "fullName": "John Doe"
  },
  "workspaceCount": 2,
  "workspaces": [
    {
      "tenantId": 1,
      "tenantName": "Acme Corporation",
      "tenantSlug": "acme",
      "logo": "guid-...",
      "initials": "AC",
      "position": "Developer",
      "accessLevel": "Admin",
      "isAdministrator": true
    },
    {
      "tenantId": 2,
      "tenantName": "Side Project",
      "tenantSlug": "sideproject",
      "logo": null,
      "initials": "SP",
      "position": "Owner",
      "accessLevel": "Admin",
      "isAdministrator": true
    }
  ],
  "primaryWorkspace": {
    "tenantId": 1,
    "tenantName": "Acme Corporation"
  },
  "hint": "User has access to 2 workspaces. Use tenantId parameter to specify which one."
}
```

### Response Felter

| Felt | Type | Beskrivelse |
|------|------|-------------|
| success | bool | Om kaldet lykkedes |
| user.id | int | Bruger ID |
| user.email | string | Email |
| user.firstName | string | Fornavn |
| user.lastName | string | Efternavn |
| user.fullName | string | Fuldt navn |
| workspaceCount | int | Antal workspaces |
| workspaces | array | Liste af workspaces |
| workspaces[].tenantId | int | **Brug denne i efterfølgende kald** |
| workspaces[].tenantName | string | Workspace navn |
| workspaces[].tenantSlug | string | URL slug |
| workspaces[].position | string | Brugerens stilling |
| workspaces[].accessLevel | string | Adgangsniveau |
| workspaces[].isAdministrator | bool | Er administrator |
| primaryWorkspace | object | Primært workspace |
| hint | string | Hjælpetekst |

### Fejl

```json
{
  "success": false,
  "error": "User not authenticated"
}
```

### Workflow

```
1. GetUserContext
2. Hvis flere workspaces: Spørg brugeren hvilket de vil arbejde med
3. Gem tenantId til efterfølgende kald
4. Brug tenantId i alle andre MCP tools
```

### Eksempel

**AI Assistant flow:**
```
Bruger: "Vis mine projekter"

AI: [Kalder GetUserContext]
AI: "Du har adgang til 2 workspaces:
     1. Acme Corporation (tenantId: 1)
     2. Side Project (tenantId: 2)
     Hvilket workspace vil du se projekter fra?"

Bruger: "Acme"

AI: [Kalder GetProjects(tenantId: 1)]
```
