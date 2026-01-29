---
type: mcp-tool
name: Intelligence
version: 2026.1.2
domain: intelligence
tags: [mcp, ai, analytics, estimation, health, similar-projects]
description: AI-powered analysis and estimation tools for project health, task decomposition, and effort estimation.
---

# Intelligence MCP Tools

AI-powered analysis and estimation via MCP.

---

## AnalyzeProjectHealth

Analyze project health with score, risks, and recommendations.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| projectId | int | Yes | - | Project ID |
| includeRecommendations | bool | No | true | Include recommendations |

### Response

```json
{
  "success": true,
  "ProjectId": 101,
  "ProjectName": "Website Redesign",
  "OverallHealthScore": 72,
  "Status": "At Risk",
  "AlertLevel": "Yellow",
  "Metrics": {
    "TotalTasks": 15,
    "CompletedTasks": 8,
    "InProgressTasks": 5,
    "OverdueTasks": 2,
    "TaskCompletionPercentage": 53.3,
    "ProjectProgress": 45,
    "TotalHoursLogged": 120.5,
    "BillablePercentage": 81.3,
    "DaysRemaining": 45,
    "WeeklyVelocity": 1.5
  },
  "Risks": [
    {
      "Description": "2 tasks overdue",
      "Severity": "Medium",
      "Impact": "May delay entire project",
      "MitigationSuggestion": "Prioritize and reallocate resources"
    }
  ],
  "Recommendations": [
    {
      "Action": "Hold standup to address delayed tasks",
      "Priority": "High",
      "ExpectedImpact": "Faster blocker identification",
      "Effort": "Low (15-30 min)"
    }
  ],
  "Forecast": {
    "OnTimeProbability": 65,
    "ExpectedCompletionDate": "2026-07-15",
    "DaysOverdue": 15,
    "Notes": "Project may be 15 days late at current velocity"
  }
}
```

### Health Score

| Score | Status | AlertLevel | Meaning |
|-------|--------|------------|---------|
| 80-100 | Healthy | Green | Project running well |
| 60-79 | At Risk | Yellow | Attention required |
| 0-59 | Critical | Red | Action needed |

### Example

```
AnalyzeProjectHealth(tenantId: 1, projectId: 101)
```

---

## SuggestTaskDecomposition

Get AI suggestions for breaking down a task based on similar projects.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| description | string | Yes | - | Task/feature description |
| projectType | string | Yes | - | Project type or keywords |

### Response

```json
{
  "success": true,
  "Description": "Implement user login with OAuth",
  "ProjectType": "web development",
  "SimilarProjectsFound": 5,
  "Analysis": {
    "AverageTasksPerProject": 8.5,
    "AverageTaskDurationDays": 3.2,
    "CommonTaskPatterns": [
      { "TaskName": "design", "OccurrenceCount": 4, "RecommendedForNewProject": true },
      { "TaskName": "implementation", "OccurrenceCount": 5, "RecommendedForNewProject": true },
      { "TaskName": "test", "OccurrenceCount": 4, "RecommendedForNewProject": true }
    ]
  },
  "Recommendations": {
    "SuggestedTaskCount": 9,
    "SuggestedDurationDays": 29,
    "KeyTasksToInclude": ["design", "implementation", "test", "documentation", "deploy"],
    "Notes": [
      "Include planning and design phases before implementation",
      "Add buffer time (15-20%) for unexpected complications"
    ]
  }
}
```

### Example

```
SuggestTaskDecomposition(
  tenantId: 1,
  description: "Implement user login with OAuth",
  projectType: "web development"
)
```

---

## EstimateEffort

Get AI estimate based on historical data.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| description | string | Yes | - | Task description |
| featureList | string | Yes | - | Comma-separated features |
| teamSize | int | No | 1 | Team size |
| riskTolerance | string | No | moderate | conservative/moderate/aggressive |

### Risk Tolerance

| Value | Description |
|-------|-------------|
| conservative | High buffer, safe estimation |
| moderate | Balanced estimation (default) |
| aggressive | Optimistic, minimal buffer |

### Response

```json
{
  "success": true,
  "Description": "E-commerce checkout flow",
  "Features": ["shopping cart", "payment integration", "order confirmation", "email notifications"],
  "TeamSize": 2,
  "RiskTolerance": "moderate",
  "Estimate": {
    "TotalHours": 160,
    "TotalDays": 20,
    "HoursPerFeature": {
      "shopping cart": 40,
      "payment integration": 50,
      "order confirmation": 35,
      "email notifications": 35
    },
    "ConfidenceLevel": "Medium",
    "Range": {
      "Optimistic": 140,
      "Expected": 160,
      "Pessimistic": 200
    }
  },
  "BasedOn": {
    "SimilarProjectsAnalyzed": 3,
    "HistoricalAccuracy": "78%"
  }
}
```

### Example

```
EstimateEffort(
  tenantId: 1,
  description: "E-commerce checkout flow",
  featureList: "shopping cart, payment integration, order confirmation, email notifications",
  teamSize: 2,
  riskTolerance: "moderate"
)
```

---

## FindSimilarProjects

Find similar projects based on description.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| tenantId | int | Yes | - | Workspace ID |
| description | string | Yes | - | Search description |
| count | int | No | 5 | Max results |
| includePatterns | bool | No | true | Include pattern analysis |

### Response

```json
{
  "success": true,
  "SearchDescription": "website redesign corporate",
  "ResultCount": 3,
  "SimilarProjects": [
    {
      "ProjectId": 45,
      "ProjectName": "Corporate Website Refresh",
      "Similarity": 0.85,
      "Status": "Completed",
      "Duration": 90,
      "TotalHours": 450,
      "TaskCount": 12,
      "KeyLearnings": [
        "Design phase took longer than expected",
        "Early stakeholder involvement was critical"
      ]
    }
  ],
  "PatternAnalysis": {
    "CommonPhases": ["Discovery", "Design", "Development", "Testing", "Launch"],
    "AverageDuration": 75,
    "AverageHours": 365,
    "SuccessFactors": [
      "Clear scope definition from start",
      "Regular customer feedback",
      "Dedicated test phase"
    ]
  }
}
```

---

## Workflows

### Evaluate New Project

```
1. FindSimilarProjects(description: "e-commerce webshop")
2. SuggestTaskDecomposition(description: "e-commerce platform", projectType: "web")
3. EstimateEffort(description: "e-commerce", featureList: "product catalog, cart, checkout, admin panel")

"Based on 4 similar projects:
- Expected duration: 3-4 months
- Estimated work: 400-500 hours
- Recommended team size: 2-3 developers
- Key tasks: [list from decomposition]"
```

### Project Health Check

```
1. AnalyzeProjectHealth(projectId: 101)

"Project is 'At Risk' with score 72/100:

Warning: 2 tasks overdue
Warning: Low velocity (1.5 tasks/week)
OK: Good billing rate (81%)

Recommendations:
1. Hold standup for delayed tasks
2. Break large tasks into smaller pieces

Forecast: 65% probability of hitting deadline."
```

---

## Hints

- Use AnalyzeProjectHealth for regular health checks
- EstimateEffort works best with detailed feature lists
- SimilarProjects helps plan new projects based on history
- Health score includes velocity, completion rate, and risk factors

---

*Last Updated: 2026-01-17*
