---
type: mcp-tool
name: Employees
version: 2026.1.2
domain: context
tags: [mcp, employees, team, workspace]
description: Get team members and employee information for a workspace.
---

# Employees MCP Tools

Get team members via MCP.

---

## GetEmployees

Get employees/team members in a workspace.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| activeOnly | bool | No | true | Only active employees |

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
      "employeeType": "Full-time",
      "accessLevel": "Admin",
      "isActive": true
    },
    {
      "userId": 11,
      "email": "jane@example.com",
      "fullName": "Jane Smith",
      "initials": "JS",
      "position": "Designer",
      "employeeType": "Full-time",
      "accessLevel": "Standard",
      "isActive": true
    }
  ]
}
```

### Example - Active Only

```
GetEmployees(tenantId: 1)
```

### Example - Include Inactive

```
GetEmployees(tenantId: 1, activeOnly: false)
```

---

## Access Levels

| Level | Description |
|-------|-------------|
| Admin | Full workspace access |
| Standard | Normal user access |
| Limited | Restricted access |
| ReadOnly | Read-only access |

---

## Employee Types

| Type | Description |
|------|-------------|
| Full-time | Full-time employee |
| Part-time | Part-time employee |
| Consultant | External consultant |
| Intern | Internship |

---

## Workflows

### Find Team for Project

```
User: "Who can work on the new project?"

AI:
1. GetEmployees(tenantId: 1)
2. "You have 5 active team members:

   Developers:
   - John Doe (Lead Developer)
   - Bob Johnson (Developer)

   Design:
   - Jane Smith (Designer)

   Who do you want to add to the project?"
```

### Check Team Capacity

```
User: "How many developers do we have?"

AI:
1. GetEmployees(tenantId: 1)
2. [Filter by position]
3. "You have 3 developers:
   - 2 full-time
   - 1 part-time (Bob Johnson)

   Total capacity: ~2.5 FTE"
```

### Find Administrator

```
User: "Who is admin?"

AI:
1. GetEmployees(tenantId: 1)
2. [Filter by accessLevel: Admin]
3. "John Doe is the only administrator in the workspace."
```

---

## Errors

| Error | Cause | Solution |
|-------|-------|----------|
| No employees found | Empty workspace | Add team members |
| Access denied | Insufficient permissions | Contact admin |

---

## Hints

- Use activeOnly: false to see former employees
- Filter by position to find specific roles
- Access level indicates permission scope
- Employee ID (userId) is used for task assignments

---

*Last Updated: 2026-01-17*
