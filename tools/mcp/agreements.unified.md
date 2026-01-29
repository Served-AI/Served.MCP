---
type: mcp-tool
name: Agreements
version: 2026.1.2
domain: calendar
tags: [mcp, agreements, bookings, calendar, crud]
description: Agreement and booking management tools for creating and managing appointments.
---

# Agreements MCP Tools

Manage agreements and bookings via MCP.

---

## GetAgreements

Get agreements for a workspace.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |

### Response

```json
{
  "success": true,
  "count": 4,
  "agreements": [
    {
      "id": 401,
      "name": "Customer Meeting - Acme",
      "startDate": "2026-01-20",
      "endDate": "2026-01-20",
      "task": "Project plan review"
    }
  ]
}
```

### Example

```
GetAgreements(tenantId: 1)
```

---

## GetAgreementDetails

Get detailed agreement information.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| agreementId | int | Yes | - | Agreement ID |

### Response

```json
{
  "success": true,
  "agreement": {
    "id": 405,
    "name": "Status Meeting",
    "startDate": "2026-02-03T10:00",
    "endDate": "2026-02-03T11:30",
    "customerId": 301,
    "customerName": "Acme Corp",
    "task": "Weekly status meeting"
  }
}
```

---

## CreateAgreement

Create a new agreement/booking.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| title | string | Yes | - | Agreement title |
| startDate | string | Yes | - | Start date/time |
| endDate | string | Yes | - | End date/time |
| customerId | int | No | null | Customer ID |
| task | string | No | null | Description/task |

### Date Formats

| Format | Example | Use Case |
|--------|---------|----------|
| `YYYY-MM-DD` | 2026-02-01 | All-day event |
| `YYYY-MM-DDTHH:mm` | 2026-02-03T10:00 | Specific time |

### Example - All-Day Event

```
CreateAgreement(
  tenantId: 1,
  title: "Project Day",
  startDate: "2026-02-01",
  endDate: "2026-02-01",
  task: "Focused work on Project X"
)
```

### Example - Meeting with Time

```
CreateAgreement(
  tenantId: 1,
  title: "Status Meeting",
  startDate: "2026-02-03T10:00",
  endDate: "2026-02-03T11:30",
  customerId: 301,
  task: "Weekly status meeting with Acme"
)
```

### Example - Multi-Day Event

```
CreateAgreement(
  tenantId: 1,
  title: "Conference",
  startDate: "2026-03-10",
  endDate: "2026-03-12",
  task: "Tech conference in Copenhagen"
)
```

### Response

```json
{
  "success": true,
  "message": "Agreement 'Status Meeting' created successfully",
  "agreementId": 405
}
```

---

## UpdateAgreement

Update an existing agreement.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| agreementId | int | Yes | - | Agreement ID |
| title | string | No | - | New title |
| startDate | string | No | - | New start date |
| endDate | string | No | - | New end date |
| customerId | int | No | - | New customer ID |
| task | string | No | - | New task description |

### Example

```
UpdateAgreement(agreementId: 405, title: "Updated Meeting")
```

---

## DeleteAgreement

Delete an agreement.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| agreementId | int | Yes | - | Agreement ID |

### Example

```
DeleteAgreement(agreementId: 405)
```

---

## Workflows

### Book Customer Meeting

```
1. GetCustomers(tenantId: 1) -> Find @customer[301] "Acme"
2. CreateAgreement(
     tenantId: 1,
     title: "Meeting with Acme",
     startDate: "2026-02-05T14:00",
     endDate: "2026-02-05T15:00",
     customerId: 301,
     task: "Review Q1 deliverables"
   )
```

### Schedule Project Work

```
1. GetProjects(tenantId: 1) -> Find @project[101]
2. CreateAgreement(
     tenantId: 1,
     title: "Focus Time - Website Redesign",
     startDate: "2026-02-06T09:00",
     endDate: "2026-02-06T16:00",
     task: "Focused frontend work"
   )
```

### Create Meeting Series

```
1. CreateAgreement(title: "Status Week 6", startDate: "2026-02-03T10:00", ...)
2. CreateAgreement(title: "Status Week 7", startDate: "2026-02-10T10:00", ...)
3. CreateAgreement(title: "Status Week 8", startDate: "2026-02-17T10:00", ...)
4. CreateAgreement(title: "Status Week 9", startDate: "2026-02-24T10:00", ...)
```

---

## Errors

| Error | Cause | Solution |
|-------|-------|----------|
| Invalid date format | Wrong date string | Use ISO format YYYY-MM-DD or YYYY-MM-DDTHH:mm |
| Agreement not found | Invalid ID | Use GetAgreements to find valid IDs |
| Customer not found | Invalid customer ID | Use GetCustomers to find ID |

---

## Hints

- Use ISO 8601 date format for all dates
- Link agreements to customers for better organization
- Multi-day events use different start and end dates
- Time-specific meetings use T notation (e.g., T10:00)

---

*Last Updated: 2026-01-17*
