---
type: skill
name: ServedAPI
version: 2026.1.3
domain: integrations
tags: [skill, api, rest, authentication, served-platform]
description: Claude skill for Served platform API - authentication, endpoints, and integration patterns.
---

# Served API - Claude Skills

Knowledge about the Served platform and its REST API.

---

## Platform Overview

| Module | Description |
|--------|-------------|
| Project Management | Projects, tasks, resources, Gantt |
| Time Registration | Time tracking, timesheets, stopwatch |
| Calendar | Calendar, appointments, bookings, customers |
| Finance | Invoices, billing, currency |
| Trading | Trading agents, portfolios, strategies |
| Companies / Sales | Company database, CVR lookup, auto-sync |
| Automation | Workflows, triggers, webhooks |
| Integration | GitHub, Microsoft, SaxoBank, Stripe |

---

## Environments

| Environment | Base URL |
|-------------|----------|
| Production | `https://app.served.dk` |
| Local Dev | `http://localhost:5010` |

---

## Authentication

JWT-based authentication with browser/session tracking.

### Flow: Register -> Login -> Bootstrap -> API calls

#### Step 1: Register Browser (REQUIRED)

```bash
BROWSER_JWT=$(curl -s -X GET \
  'https://app.served.dk/api/account/Register?visitorId=550e8400-e29b-41d4-a716-446655440000' \
  | jq -r '.token')
```

#### Step 2: Login (requires browser JWT)

```bash
USER_JWT=$(curl -s -X POST 'https://app.served.dk/api/account/Login' \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer ${BROWSER_JWT}" \
  -d '{"email": "user@example.com", "password": "...", "saveSession": true}' \
  | jq -r '.token')
```

#### Step 3: Bootstrap

```bash
USER_DATA=$(curl -s -X GET 'https://app.served.dk/api/bootstrap/user' \
  -H "Authorization: Bearer ${USER_JWT}")
TENANT_SLUG=$(echo "$USER_DATA" | jq -r '.tenants[0].slug')
```

#### Step 4: API Calls (requires BOTH tenant headers)

```bash
curl -X POST 'https://app.served.dk/api/endpoint' \
  -H "Authorization: Bearer ${USER_JWT}" \
  -H "Served-Tenant: ${TENANT_SLUG}" \
  -H "Served-Tenant: ${TENANT_SLUG}" \
  -H 'Content-Type: application/json'
```

### API Key Authentication

```bash
curl -X GET 'https://app.served.dk/api/endpoint' \
  -H 'X-API-Key: <API_KEY>'
```

---

## Response Format

Default response format is **JSON**. Use headers to request different formats.

### Headers

| Header | Example | Description |
|--------|---------|-------------|
| `Accept` | `application/xml` | Standard content negotiation |
| `X-Response-Format` | `xml` | Custom format header |
| `X-Request-Format` | `json` | Request body format |

### Supported Formats

| Format | MIME Type | Value |
|--------|-----------|-------|
| JSON (default) | `application/json` | `json` |
| XML | `application/xml` | `xml` |
| YAML | `application/x-yaml` | `yaml` |
| MessagePack | `application/msgpack` | `msgpack` |

### Example

```bash
# Get XML response
curl -X GET 'https://app.served.dk/api/bootstrap/user' \
  -H "Authorization: Bearer ${USER_JWT}" \
  -H 'Accept: application/xml'

# Or using custom header
curl -X GET 'https://app.served.dk/api/bootstrap/user' \
  -H "Authorization: Bearer ${USER_JWT}" \
  -H 'X-Response-Format: yaml'
```

---

## API Modules

### Project Management (`/api/project_management`)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/project/GetKeys` | POST | Get project IDs with filter |
| `/project/Create` | POST | Create project |
| `/project/Update` | POST | Update project |
| `/task/GetKeys` | POST | Get task IDs |
| `/task/Create` | POST | Create task |
| `/task/Update` | POST | Update task |

### Time Registration (`/api/registration`)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/timeregistration/GetKeys` | POST | Get time entry IDs |
| `/timeregistration/Create` | POST | Create time entry |
| `/timesheet/Submit` | POST | Submit timesheet |
| `/stopwatch/Start` | POST | Start timer |

### Calendar (`/api/calendar`)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/customer/GetKeys` | POST | Get customer IDs |
| `/customer/Create` | POST | Create customer |
| `/agreement/Create` | POST | Create agreement |
| `/contact/Search` | GET | Search contacts |

### Finance (`/api/finance`)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/invoice/GetKeys` | POST | Get invoice IDs |
| `/invoice/Create` | POST | Create invoice |
| `/invoice/{id}/pdf` | GET | Download PDF |
| `/billing/stats` | GET | Billing statistics |

### Trading (`/api/trading`)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/agents` | GET/POST | List/create agents |
| `/agents/{id}/performance` | GET | Performance metrics |
| `/portfolio` | GET | All portfolios |
| `/transactions` | GET | All transactions |

### Companies (`/api/companies`)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/companies/global-search` | GET | Search companies |
| `/companies/cvr/{cvrNumber}` | GET | CVR lookup |
| `/companies/import` | POST | Import to customer list |

---

## Response Patterns

### Success

```json
{
  "data": { ... },
  "success": true,
  "message": null
}
```

### Error

```json
{
  "success": false,
  "message": "Error description",
  "errors": ["Validation error 1"]
}
```

### GetKeys Request

```json
{
  "filter": { "status": "Active" },
  "sort": { "field": "CreatedAt", "direction": "desc" },
  "skip": 0,
  "take": 20
}
```

---

## SignalR Hubs

| Hub | Endpoint | Events |
|-----|----------|--------|
| NotificationHub | `/notificationHub` | `NotificationReceived`, `NotificationRead` |
| TradingHub | `/tradingHub` | `TransactionExecuted`, `PortfolioUpdated` |
| BackgroundTasksHub | `/backgroundTasksHub` | `TaskStarted`, `TaskCompleted` |

---

## Hints

- Always register browser first before login
- Use tenant SLUG (not ID) in Served-Tenant headers
- Include BOTH Served-Tenant headers (required by backend)
- Use API Key for programmatic access without login flow
- Bootstrap endpoints provide user/tenant context

---

*Last Updated: 2026-01-17*
