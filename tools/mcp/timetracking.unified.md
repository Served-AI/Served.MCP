---
type: mcp-tool
name: TimeTracking
version: 2026.1.2
domain: timetracking
tags: [mcp, timetracking, ai, suggestions, patterns]
description: AI-powered time tracking with suggestions based on historical patterns and calendar events.
---

# TimeTracking MCP Tools

AI-powered time tracking via MCP.

---

## SuggestTimeEntries

Get AI-generated time entry suggestions based on historical patterns and calendar events.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| startDate | string | Yes | - | From date (YYYY-MM-DD) |
| endDate | string | Yes | - | To date (YYYY-MM-DD) |
| includeCalendar | bool | No | true | Include calendar events |
| minConfidence | double | No | 0.5 | Minimum confidence 0.0-1.0 |

### Response

```json
{
  "success": true,
  "Type": "time_entry_suggestions",
  "Count": 5,
  "TotalHours": 35.5,
  "PatternCount": 3,
  "CalendarEventCount": 2,
  "Summary": "Analyzed 90 days history. Found 3 patterns and 2 calendar events.",
  "Suggestions": [
    {
      "Date": "2026-01-20",
      "ProjectId": 101,
      "ProjectName": "Website Redesign",
      "TaskId": 201,
      "TaskName": "Frontend development",
      "SuggestedHours": 7.5,
      "Confidence": "85%",
      "Source": "HistoricalPattern",
      "Reason": "You typically work 7.5 hours on this task on Mondays",
      "Description": "Frontend development"
    },
    {
      "Date": "2026-01-21",
      "ProjectId": 101,
      "ProjectName": "Website Redesign",
      "TaskId": 202,
      "TaskName": "Backend API",
      "SuggestedHours": 4.0,
      "Confidence": "70%",
      "Source": "CalendarEvent",
      "Reason": "Based on calendar event 'API Workshop'",
      "Description": "Backend development"
    }
  ]
}
```

### Confidence Levels

| Score | Level | Meaning |
|-------|-------|---------|
| >= 0.8 | High | Strong pattern, reliable |
| 0.6-0.79 | Medium | Likely, but verify |
| 0.5-0.59 | Low | Uncertain, user should check |

### Suggestion Sources

| Source | Description |
|--------|-------------|
| HistoricalPattern | Based on previous time entries |
| CalendarEvent | Based on calendar appointment |
| GapFill | Fills gaps in time tracking |
| ProjectDeadline | Project approaching deadline |
| RecurringTask | Recurring task |

### Example

```
SuggestTimeEntries(
  tenantId: 1,
  startDate: "2026-01-20",
  endDate: "2026-01-24",
  includeCalendar: true,
  minConfidence: 0.5
)
```

---

## AnalyzeTimePatterns

Analyze user's time patterns over a period.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| lookbackDays | int | No | 90 | Days to analyze |

### Response

```json
{
  "success": true,
  "Type": "time_patterns",
  "Count": 4,
  "LookbackDays": 90,
  "Patterns": [
    {
      "ProjectId": 101,
      "ProjectName": "Website Redesign",
      "TaskId": 201,
      "TaskName": "Frontend development",
      "PreferredDay": "Monday",
      "AverageHours": "6.5",
      "Frequency": 12,
      "LastTime": "2026-01-13"
    },
    {
      "ProjectId": 101,
      "ProjectName": "Website Redesign",
      "TaskId": 202,
      "TaskName": "Backend API",
      "PreferredDay": "Wednesday",
      "AverageHours": "5.0",
      "Frequency": 8,
      "LastTime": "2026-01-15"
    }
  ],
  "Summary": "Found 4 time patterns based on the last 90 days."
}
```

### Pattern Fields

| Field | Description |
|-------|-------------|
| PreferredDay | Day user typically works on task |
| AverageHours | Average time per session |
| Frequency | Number of times task was logged |
| LastTime | Last time entry for task |

### Example

```
AnalyzeTimePatterns(tenantId: 1, lookbackDays: 90)
```

---

## Workflows

### Fill Weekly Time Entries

```
User: "Help me fill in my time entries for this week"

AI:
1. SuggestTimeEntries(tenantId: 1, startDate: "2026-01-20", endDate: "2026-01-24")
2. "Based on your patterns and calendar, I suggest:

   Monday 20/1:
   - Website Redesign / Frontend: 7.5 hours (high confidence)

   Tuesday 21/1:
   - Website Redesign / Backend: 4.0 hours (calendar-based)
   - Mobile App / Design: 3.5 hours (pattern)

   ...

   Should I create these entries?"
```

### Understand Work Patterns

```
User: "What do my work patterns look like?"

AI:
1. AnalyzeTimePatterns(tenantId: 1, lookbackDays: 90)
2. "Over the last 90 days you have:

   - Monday: Primarily frontend work (~6.5 hours)
   - Wednesday: Backend focus (~5 hours)
   - Friday: Mixed with shorter sessions

   Your most active project is 'Website Redesign' with 120 hours total."
```

---

## Errors

| Error | Cause | Solution |
|-------|-------|----------|
| Invalid date | Wrong format | Use ISO format (yyyy-MM-dd) |
| End before start | Date order | Ensure endDate is after startDate |
| No patterns found | No history | User needs more time entries |

---

## Hints

- Higher minConfidence means fewer but more reliable suggestions
- includeCalendar pulls from user's calendar appointments
- Patterns improve with more historical data
- Use AnalyzeTimePatterns to understand work habits

---

*Last Updated: 2026-01-17*
