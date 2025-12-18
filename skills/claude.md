# Served API - Claude Skills

Dette dokument giver Claude viden om Served platformen og dens API.

## Oversigt

Served er en enterprise platform med følgende hovedmoduler:

| Modul | Beskrivelse |
|-------|-------------|
| Project Management | Projekter, tasks, ressourcer, Gantt |
| Time Registration | Tidsregistrering, timesheets, stopwatch |
| Calendar | Kalender, aftaler, bookings, kunder |
| Finance | Fakturaer, billing, valuta |
| Trading | Trading agents, porteføljer, strategier |
| Automation | Workflows, triggers, webhooks |
| Integration | GitHub, Microsoft, SaxoBank, Stripe |

## Miljøer

| Miljø | Base URL |
|-------|----------|
| Production | `https://app.served.dk` |
| Local Dev | `http://localhost:5010` |

## Authentication

### JWT Token

```bash
curl -X GET 'https://app.served.dk/api/endpoint' \
  -H 'Authorization: Bearer <JWT_TOKEN>' \
  -H 'Content-Type: application/json'
```

### API Key

```bash
curl -X GET 'https://app.served.dk/api/endpoint' \
  -H 'X-API-Key: <API_KEY>' \
  -H 'Content-Type: application/json'
```

---

## API Endpoints Reference

### Project Management (`/api/project_management`)

#### Projects

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| POST | `/project/GetKeys` | Hent projekt IDs med filter |
| POST | `/project/GetGrouping` | Hent projekter grupperet |
| POST | `/project/Detailed` | Hent detaljeret projekt |
| POST | `/project/Create` | Opret projekt |
| POST | `/project/Update` | Opdater projekt |
| PATCH | `/project/UpdateMultiple` | Batch opdater |
| DELETE | `/project/Delete` | Slet projekt |

**Eksempel - Hent projekter:**
```bash
curl -X POST 'https://app.served.dk/api/project_management/project/GetKeys' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "filter": { "status": "Active" },
    "sort": { "field": "CreatedAt", "direction": "desc" },
    "skip": 0,
    "take": 20
  }'
```

#### Tasks

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| POST | `/task/GetKeys` | Hent task IDs |
| POST | `/task/GetGrouping` | Hent tasks grupperet |
| POST | `/task/Gantt` | Hent Gantt data |
| POST | `/task/Detailed` | Hent detaljeret task |
| POST | `/task/Create` | Opret task |
| POST | `/task/Update` | Opdater task |
| PATCH | `/task/UpdateMultiple` | Batch opdater |
| DELETE | `/task/Delete` | Slet task |

#### Resources & Allocations

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| POST | `/resource/GetGrouping` | Hent ressourcer grupperet |
| POST | `/resource/AddResource` | Tilføj ressource |
| POST | `/allocation/Get` | Hent allokeringer |
| POST | `/allocation/Patch` | Opdater allokeringer |
| POST | `/gantt/ListResourceAvailability` | Ressource tilgængelighed |

---

### Time Registration (`/api/registration`)

#### Time Registration

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| POST | `/timeregistration/GetKeys` | Hent registrering IDs |
| POST | `/timeregistration/Get` | Hent registrering |
| POST | `/timeregistration/Group` | Hent grupperet |
| POST | `/timeregistration/Create` | Opret registrering |
| POST | `/timeregistration/Update` | Opdater registrering |
| POST | `/timeregistration/GetMonthlyReport` | Månedlig rapport |
| DELETE | `/timeregistration/Delete` | Slet registrering |

**Eksempel - Opret tidsregistrering:**
```bash
curl -X POST 'https://app.served.dk/api/registration/timeregistration/Create' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "projectId": 123,
    "taskId": 456,
    "date": "2025-01-15",
    "hours": 7.5,
    "description": "Udvikling af feature X"
  }'
```

#### Timesheets

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| POST | `/timesheet/Get` | Hent timesheet |
| POST | `/timesheet/Submit` | Indsend timesheet |
| POST | `/timesheet/Approve` | Godkend timesheet |
| POST | `/timesheet/Reject` | Afvis timesheet |

#### Stopwatch

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| POST | `/stopwatch/Start` | Start timer |
| POST | `/stopwatch/Stop` | Stop timer |
| POST | `/stopwatch/Pause` | Pause timer |
| GET | `/stopwatch/Active` | Hent aktive timere |

---

### Calendar (`/api/calendar`)

#### Calendars

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| GET | `/calendar/Get/{id}` | Hent kalender |
| POST | `/calendar/GetKeys` | Hent kalender IDs |
| POST | `/calendar/GetEvents` | Hent events |
| POST | `/calendar/Create` | Opret kalender |
| POST | `/calendar/Update` | Opdater kalender |
| DELETE | `/calendar/Delete` | Slet kalender |
| GET | `/calendar/ICS/{guid}` | Eksporter ICS |

#### Customers

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| GET | `/customer/Get/{id}` | Hent kunde |
| POST | `/customer/GetKeys` | Hent kunde IDs |
| POST | `/customer/Create` | Opret kunde |
| POST | `/customer/Update` | Opdater kunde |
| DELETE | `/customer/Delete` | Slet kunde |
| POST | `/customer/LookUp` | Søg kunde |

#### Contacts

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| GET | `/contact/Get/{id}` | Hent kontakt |
| POST | `/contact/GetKeys` | Hent kontakt IDs |
| POST | `/contact/Create` | Opret kontakt |
| POST | `/contact/Update` | Opdater kontakt |
| DELETE | `/contact/Delete` | Slet kontakt |
| GET | `/contact/Search` | Søg kontakter |

#### Agreements

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| GET | `/agreement/Get/{id}` | Hent aftale |
| POST | `/agreement/GetKeys` | Hent aftale IDs |
| POST | `/agreement/Create` | Opret aftale |
| POST | `/agreement/Update` | Opdater aftale |
| DELETE | `/agreement/Delete` | Slet aftale |

---

### Finance (`/api/finance`)

#### Invoices

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| POST | `/invoice/GetKeys` | Hent faktura IDs |
| POST | `/invoice/GetRange` | Hent fakturaer |
| POST | `/invoice/GetGrouping` | Hent grupperet |
| POST | `/invoice/Create` | Opret faktura |
| POST | `/invoice/Update` | Opdater faktura |
| PATCH | `/invoice/UpdateMultiple` | Batch opdater |
| DELETE | `/invoice/Delete` | Slet faktura |
| GET | `/invoice/{id}/pdf` | Download PDF |

**Eksempel - Hent fakturaer:**
```bash
curl -X POST 'https://app.served.dk/api/finance/invoice/GetKeys' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "filter": { "status": "Unpaid" },
    "skip": 0,
    "take": 50
  }'
```

#### Billing

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| GET | `/billing/unbilled-time-registrations` | Ufaktureret tid |
| GET | `/billing/stats` | Billing statistik |
| GET | `/billing/contracts` | List kontrakter |
| POST | `/billing/contracts` | Opret kontrakt |

#### Currency

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| GET | `/currency/Get` | Hent valuta |
| GET | `/currency/GetAll` | Alle valutaer |
| POST | `/currency/Enable` | Aktiver valuta |
| POST | `/currency/SetAsDefault` | Sæt standard |

---

### Trading (`/api/trading`)

#### Agents

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| GET | `/agents` | List agents |
| GET | `/agents/{id}` | Hent agent |
| POST | `/agents` | Opret agent |
| PUT | `/agents/{id}` | Opdater agent |
| DELETE | `/agents/{id}` | Slet agent |
| POST | `/agents/{id}/pause` | Pause agent |
| POST | `/agents/{id}/resume` | Genoptag agent |
| GET | `/agents/{id}/performance` | Performance metrics |

#### Portfolio

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| GET | `/portfolio` | Alle porteføljer |
| GET | `/portfolio/agent/{agentId}` | Agent portefølje |

#### Transactions

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| GET | `/transactions` | Alle transaktioner |
| GET | `/transactions/agent/{agentId}` | Agent transaktioner |
| GET | `/transactions/{id}` | Transaktionsdetaljer |

#### Strategies

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| GET | `/strategies` | List strategier |
| POST | `/strategies` | Opret strategi |
| PUT | `/strategies/{id}` | Opdater strategi |

#### Assets

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| GET | `/assets/search?query=X` | Søg assets |
| GET | `/assets/{symbol}/price` | Hent pris |
| GET | `/assets/{symbol}/history` | Historiske priser |

---

### Administration (`/api/administration`)

#### Users

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| POST | `/user/GetKeys` | Hent bruger IDs |
| POST | `/user/GetRange` | Hent brugere |
| POST | `/user/GetSearchRange` | Søg brugere |
| POST | `/user/Create` | Opret bruger |
| POST | `/user/Update` | Opdater bruger |
| DELETE | `/user/Delete` | Slet bruger |

#### API Keys

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| GET | `/apikey` | List API keys |
| GET | `/apikey/{id}` | Hent API key |
| POST | `/apikey` | Opret API key |
| PUT | `/apikey/{id}` | Opdater API key |
| DELETE | `/apikey/{id}` | Slet API key |
| POST | `/apikey/{id}/regenerate` | Regenerer key |

#### Permissions

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| GET | `/permission` | Alle permissions |
| POST | `/permission/GetForUser` | Bruger permissions |
| POST | `/permission/Assign` | Tildel permission |
| POST | `/permission/Revoke` | Fjern permission |

---

### Automation (`/api/automation`)

#### Workflows

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| POST | `/workflow/Get` | Hent workflow |
| POST | `/workflow/GetList` | List workflows |
| POST | `/workflow/Create` | Opret workflow |
| POST | `/workflow/Update` | Opdater workflow |
| POST | `/workflow/Delete` | Slet workflow |
| POST | `/workflow/ToggleActive` | Toggle aktiv |

#### Workflow Execution

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| POST | `/workflowexecution/Get` | Hent execution |
| POST | `/workflowexecution/GetList` | List executions |
| POST | `/workflowexecution/ExecuteWorkflow` | Kør workflow |

#### Triggers

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| POST | `/workflowtrigger/Get` | Hent trigger |
| POST | `/workflowtrigger/Create` | Opret trigger |
| POST | `/workflowtrigger/ExecuteManually` | Manuel kørsel |

---

### Reports (`/api/reports`)

#### Dashboard

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| GET | `/dashboard/kpis` | Hent KPIs |
| GET | `/dashboard/quick-stats` | Hurtig statistik |
| GET | `/dashboard/recent-activity` | Seneste aktivitet |
| GET | `/dashboard/my-tasks` | Mine tasks |
| GET | `/dashboard/team-performance` | Team performance |

#### Finance Reports

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| POST | `/financereport/Revenue` | Omsætningsrapport |
| POST | `/financereport/Expenses` | Udgiftsrapport |
| POST | `/financereport/Profitability` | Rentabilitetsrapport |

#### Time Reports

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| POST | `/timereport/Utilization` | Udnyttelsesrapport |
| POST | `/timereport/ByProject` | Tid per projekt |
| POST | `/timereport/ByEmployee` | Tid per medarbejder |

---

### Integration (`/api/integration`)

#### Connection Management

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| GET | `/administration/connection/SystemIntegrations` | System integrationer |
| GET | `/administration/connection/GetIntegrations` | Tenant integrationer |
| POST | `/administration/connection/InitiateConnection` | Start OAuth flow |
| POST | `/administration/connection/DisconnectIntegration` | Frakobl |

#### GitHub

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| GET | `/github/repositories` | List repositories |
| GET | `/github/repositories/{owner}/{repo}/pulls` | List PRs |
| GET | `/github/repositories/{owner}/{repo}/issues` | List issues |

#### Microsoft

| Method | Endpoint | Beskrivelse |
|--------|----------|-------------|
| GET | `/microsoft/sites` | SharePoint sites |
| GET | `/microsoft/drives` | OneDrive drives |
| GET | `/microsoft/files` | List filer |
| POST | `/microsoft/upload` | Upload fil |

---

## Response Patterns

### Success Response

```json
{
  "data": { ... },
  "success": true,
  "message": null
}
```

### Error Response

```json
{
  "data": null,
  "success": false,
  "message": "Error description",
  "errors": ["Validation error 1", "Validation error 2"]
}
```

### GetKeys Request Pattern

```json
{
  "filter": { "status": "Active" },
  "sort": { "field": "CreatedAt", "direction": "desc" },
  "skip": 0,
  "take": 20
}
```

### Paginated Response

```json
{
  "items": [...],
  "totalCount": 100,
  "pageIndex": 0,
  "pageSize": 20,
  "totalPages": 5
}
```

---

## SignalR Hubs

Real-time kommunikation:

| Hub | Endpoint | Events |
|-----|----------|--------|
| NotificationHub | `/notificationHub` | `NotificationReceived`, `NotificationRead` |
| TradingHub | `/tradingHub` | `TransactionExecuted`, `PortfolioUpdated` |
| BackgroundTasksHub | `/backgroundTasksHub` | `TaskStarted`, `TaskCompleted`, `Progress` |

---

## Translations

Opret nye oversættelser via API:

```bash
curl -X POST 'https://app.served.dk/api/translations/phrase/Create' \
  -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{
    "name": "module.feature.description",
    "languages": [
      {"languageId": 1, "value": "English text"},
      {"languageId": 2, "value": "Dansk tekst"}
    ]
  }'
```

**Language IDs:** 1 = English, 2 = Danish
