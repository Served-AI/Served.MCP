---
type: mcp-tool
name: CustomFields
version: 2026.1.2
domain: context
tags: [mcp, customfields, metadata, configuration]
description: Custom field management for adding extra data to projects, tasks, customers, and other entities.
---

# CustomFields MCP Tools

Manage custom fields via MCP. Add extra data to entities.

---

## Overview

Custom fields are organized in three levels:

1. **Sections** - Groups of fields (e.g., "Project Info", "Technical Data")
2. **Definitions** - Field definitions (name, type, validation)
3. **Values** - Actual values for each entity

---

## GetCustomFieldDefinitions

Get all custom field definitions for a domain type.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| domainType | string | No | all | Filter by domain type |
| sectionId | int | No | null | Filter by section |

### Domain Types

| Value | Description |
|-------|-------------|
| Project | Projects |
| Task | Tasks |
| Customer | Customers |
| Agreement | Agreements |
| Invoice | Invoices |
| Employee | Employees |

### Response

```json
{
  "success": true,
  "definitions": [
    {
      "id": 1,
      "stringId": "project_category",
      "sectionId": 1,
      "label": "Project Category",
      "dataType": "Dropdown",
      "domainType": "Project",
      "isRequired": true,
      "isReadOnly": false,
      "configuration": "{\"options\": [\"Internal\", \"Customer\", \"R&D\"]}",
      "placeholder": "Select category"
    },
    {
      "id": 2,
      "stringId": "estimated_budget",
      "label": "Estimated Budget",
      "dataType": "Number",
      "domainType": "Project",
      "configuration": "{\"min\": 0, \"currency\": \"DKK\"}"
    }
  ]
}
```

### Example

```
GetCustomFieldDefinitions(tenantId: 1, domainType: "Project")
```

---

## Data Types

| DataType | Description | Example Value |
|----------|-------------|---------------|
| Text | Free text | "A description" |
| Number | Numeric value | "12345" |
| Date | Date | "2026-03-15" |
| DateTime | Date and time | "2026-03-15T14:30:00" |
| Dropdown | Selection | "Option1" |
| Checkbox | Boolean | "true" or "false" |
| MultiSelect | Multiple choices | "Option1,Option2" |
| User | User reference | "42" (userId) |
| Link | URL | "https://example.com" |

---

## GetEntityCustomFields

Get custom field values for a specific entity.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| domainType | string | Yes | - | Entity type |
| entityId | int | Yes | - | Entity ID |

### Response

```json
{
  "success": true,
  "tenantId": 1,
  "domainType": "Project",
  "entityId": 101,
  "fields": [
    {
      "definitionId": 1,
      "stringId": "project_category",
      "label": "Project Category",
      "dataType": "Dropdown",
      "isRequired": true,
      "value": "Customer",
      "valueId": 501
    },
    {
      "definitionId": 2,
      "stringId": "estimated_budget",
      "label": "Estimated Budget",
      "dataType": "Number",
      "value": "150000",
      "valueId": 502
    }
  ]
}
```

### Example

```
GetEntityCustomFields(tenantId: 1, domainType: "Project", entityId: 101)
```

---

## SetCustomFieldValue

Set a custom field value for an entity.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| domainType | string | Yes | - | Entity type |
| entityId | int | Yes | - | Entity ID |
| definitionId | int | Yes | - | Field definition ID |
| value | string | No | null | New value (null to delete) |

### Example - Set Value

```
SetCustomFieldValue(
  tenantId: 1,
  domainType: "Project",
  entityId: 101,
  definitionId: 1,
  value: "Internal"
)
```

### Example - Delete Value

```
SetCustomFieldValue(
  tenantId: 1,
  domainType: "Project",
  entityId: 101,
  definitionId: 2,
  value: null
)
```

---

## BulkSetCustomFieldValues

Set multiple custom field values at once.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| domainType | string | Yes | - | Entity type |
| entityId | int | Yes | - | Entity ID |
| values | array | Yes | - | Array of field-values |

### Values Format

```json
[
  { "fieldIdentifier": "project_category", "value": "Customer" },
  { "fieldIdentifier": "estimated_budget", "value": "150000" }
]
```

Field can be identified by `stringId` or `definitionId` as string.

### Example

```
BulkSetCustomFieldValues(
  tenantId: 1,
  domainType: "Project",
  entityId: 101,
  values: [
    { "fieldIdentifier": "project_category", "value": "Customer" },
    { "fieldIdentifier": "estimated_budget", "value": "150000" }
  ]
)
```

---

## Workflows

### Create Project with Custom Fields

```
1. GetUserContext() -> tenantId: 1
2. GetCustomFieldDefinitions(tenantId: 1, domainType: "Project")
   -> Find project_category (id: 1) and estimated_budget (id: 2)
3. CreateProject(tenantId: 1, name: "New Customer Project") -> projectId: 105
4. BulkSetCustomFieldValues(
     tenantId: 1,
     domainType: "Project",
     entityId: 105,
     values: [
       { "fieldIdentifier": "project_category", "value": "Customer" },
       { "fieldIdentifier": "estimated_budget", "value": "200000" }
     ]
   )
```

### Read and Display Custom Fields

```
1. GetProjectDetails(tenantId: 1, projectId: 101)
2. GetEntityCustomFields(tenantId: 1, domainType: "Project", entityId: 101)

"Project: Website Redesign (101)

Standard fields:
- Progress: 45%
- Period: 2026-01-01 to 2026-06-30

Custom fields:
- Project Category: Customer
- Estimated Budget: 150,000 kr
- Risk Assessment: Medium"
```

---

## Errors

| Error | Cause | Solution |
|-------|-------|----------|
| Field definition not found | Invalid ID | Use GetCustomFieldDefinitions |
| Invalid dropdown value | Value not in options | Check configuration for valid options |
| Required field missing | Required field empty | Provide required values |

---

## Best Practices

1. **Get definitions first** - Before setting values, get definitions to know types and options
2. **Use stringId** - More stable than definitionId across environments
3. **Bulk operations** - Use BulkSetCustomFieldValues for multiple fields
4. **Dropdown validation** - Check that value is a valid option before sending
5. **Date formats** - Always use ISO 8601: `2026-03-15` or `2026-03-15T14:30:00`

---

## Hints

- stringId is more stable than definitionId
- Bulk operations are more efficient for multiple fields
- Fields without value have `value: null` and `valueId: null`
- Use configuration to see dropdown options

---

*Last Updated: 2026-01-17*
