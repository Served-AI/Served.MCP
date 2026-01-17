---
type: mcp-tool
name: Customers
version: 2026.1.2
domain: customers
tags: [mcp, customers, crm, crud]
description: Customer management tools for creating, updating, and organizing customers.
---

# Customers MCP Tools

Manage customers via MCP.

---

## GetCustomers

Get customers for a workspace.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |

### Response

```json
{
  "success": true,
  "count": 3,
  "customers": [
    {
      "id": 301,
      "name": "Acme Corporation",
      "type": "Business",
      "email": "contact@acme.dk",
      "phone": "+4512345678"
    },
    {
      "id": 302,
      "name": "John Doe",
      "type": "Private",
      "email": "john@example.com",
      "phone": "+4587654321"
    }
  ]
}
```

### Example

```
GetCustomers(tenantId: 1)
```

---

## GetCustomerDetails

Get detailed customer information.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| customerId | int | Yes | - | Customer ID |

### Response

```json
{
  "success": true,
  "customer": {
    "id": 301,
    "firstName": "Acme",
    "lastName": "Corporation",
    "email": "contact@acme.dk",
    "phone": "+4599887766",
    "type": "Business",
    "address": "Main Street 1",
    "city": "Copenhagen",
    "zipCode": "1000",
    "country": "Denmark"
  }
}
```

---

## CreateCustomer

Create a new customer.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| firstName | string | Yes | - | First name or company name |
| lastName | string | No | null | Last name |
| email | string | No | null | Email address |
| phone | string | No | null | Phone number |

### Example - Business

```
CreateCustomer(
  tenantId: 1,
  firstName: "Tech Solutions",
  lastName: "ApS",
  email: "contact@techsolutions.dk",
  phone: "+4533221100"
)
```

### Example - Private Person

```
CreateCustomer(
  tenantId: 1,
  firstName: "Anders",
  lastName: "Andersen",
  email: "anders@gmail.com",
  phone: "+4511223344"
)
```

### Response

```json
{
  "success": true,
  "message": "Customer 'Tech Solutions ApS' created successfully",
  "customerId": 304
}
```

---

## UpdateCustomer

Update an existing customer.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| customerId | int | Yes | - | Customer ID |
| firstName | string | No | - | New first name/company |
| lastName | string | No | - | New last name |
| email | string | No | - | New email |
| phone | string | No | - | New phone number |

### Example

```
UpdateCustomer(
  tenantId: 1,
  customerId: 301,
  email: "new-email@acme.dk",
  phone: "+4599887766"
)
```

---

## DeleteCustomer

Delete a customer.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| customerId | int | Yes | - | Customer ID |

### Example

```
DeleteCustomer(customerId: 301)
```

---

## Workflows

### Create Customer and Link to Project

```
1. CreateCustomer(tenantId: 1, firstName: "New Customer") -> customerId: 305
2. CreateProject(tenantId: 1, name: "Project for New Customer", customerId: 305)
```

### Find and Update Customer

```
1. GetCustomers(tenantId: 1) -> Find @customer[301]
2. UpdateCustomer(tenantId: 1, customerId: 301, email: "new@email.dk")
```

### Search for Customer

```
1. GetCustomers(tenantId: 1)
2. AI: Filter list based on user search criteria
3. AI: "Found 2 customers matching 'Novo': @customer[303], @customer[310]"
```

---

## Errors

| Error | Cause | Solution |
|-------|-------|----------|
| Customer not found | Invalid ID | Use GetCustomers to find valid IDs |
| No customers found | Empty workspace | Create customers first |

---

## Hints

- Customer type (Person/Business) is set automatically based on data
- Use REST API for advanced fields like CVR number
- Link customers to projects for better organization
- Use @customer[id] reference format for clarity

---

*Last Updated: 2026-01-17*
