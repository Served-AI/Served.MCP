# Project Intelligence Tools

AI-powered analyse og estimering via MCP.

---

## AnalyzeProjectHealth

Analysér projektsundhed med score, risici og anbefalinger.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| projectId | int | Ja | Projekt ID |
| includeRecommendations | bool | Nej | Inkluder anbefalinger (default: true) |

### Request

```json
{
  "tool": "AnalyzeProjectHealth",
  "tenantId": 1,
  "projectId": 101,
  "includeRecommendations": true
}
```

### Response

```json
{
  "ProjectId": 101,
  "ProjectName": "Website Redesign",
  "ProjectNo": "PRJ-2025-001",
  "OverallHealthScore": 72,
  "Status": "I Risiko",
  "AlertLevel": "Gul",
  "Metrics": {
    "TotalTasks": 15,
    "CompletedTasks": 8,
    "InProgressTasks": 5,
    "OverdueTasks": 2,
    "TaskCompletionPercentage": 53.3,
    "ProjectProgress": 45,
    "TotalHoursLogged": 120.5,
    "BillableHoursLogged": 98.0,
    "BillablePercentage": 81.3,
    "DaysRemaining": 45,
    "DaysElapsed": 30,
    "DailyBurnRate": 4.02,
    "WeeklyVelocity": 1.5
  },
  "Risks": [
    {
      "Description": "2 opgaver er overskredet deadline",
      "Severity": "Medium",
      "Impact": "Kan forsinke hele projektet",
      "MitigationSuggestion": "Prioriter og omfordel ressourcer til forsinkede opgaver"
    },
    {
      "Description": "Lav hastighed i opgavefærdiggørelse",
      "Severity": "Medium",
      "Impact": "Projektet kan komme bagud",
      "MitigationSuggestion": "Identificer blokeringer og øg teamets fokus"
    }
  ],
  "Recommendations": [
    {
      "Action": "Afhold et standup-møde for at adressere forsinkede opgaver",
      "Priority": "Høj",
      "ExpectedImpact": "Hurtigere identificering af blokeringer",
      "Effort": "Lav (15-30 min)"
    },
    {
      "Action": "Opdel store opgaver i mindre, mere håndterbare dele",
      "Priority": "Høj",
      "ExpectedImpact": "Øget hastighed og bedre fremskridt tracking",
      "Effort": "Medium (1-2 timer)"
    }
  ],
  "Forecast": {
    "OnTimeProbability": 65,
    "ExpectedCompletionDate": "2025-07-15",
    "DaysOverdue": 15,
    "Notes": "Projektet kan blive 15 dage forsinket med nuværende hastighed"
  },
  "AnalysisTimestamp": "2025-01-18T10:30:00Z"
}
```

### Health Score

| Score | Status | AlertLevel | Betydning |
|-------|--------|------------|-----------|
| 80-100 | Sundt | Grøn | Projektet kører godt |
| 60-79 | I Risiko | Gul | Opmærksomhed påkrævet |
| 0-59 | Kritisk | Rød | Handling nødvendig |

---

## SuggestTaskDecomposition

Få AI-forslag til hvordan en opgave kan brydes ned baseret på lignende projekter.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| description | string | Ja | Beskrivelse af opgave/feature |
| projectType | string | Ja | Projekttype eller keywords |

### Request

```json
{
  "tool": "SuggestTaskDecomposition",
  "tenantId": 1,
  "description": "Implementer brugerlogin med OAuth",
  "projectType": "web development"
}
```

### Response

```json
{
  "Description": "Implementer brugerlogin med OAuth",
  "ProjectType": "web development",
  "SimilarProjectsFound": 5,
  "Analysis": {
    "AverageTasksPerProject": 8.5,
    "AverageTaskDurationDays": 3.2,
    "CommonTaskPatterns": [
      { "TaskName": "design", "OccurrenceCount": 4, "RecommendedForNewProject": true },
      { "TaskName": "implementering", "OccurrenceCount": 5, "RecommendedForNewProject": true },
      { "TaskName": "test", "OccurrenceCount": 4, "RecommendedForNewProject": true },
      { "TaskName": "dokumentation", "OccurrenceCount": 3, "RecommendedForNewProject": true }
    ],
    "ProjectPatterns": [
      {
        "ProjectName": "Kundeportal Login",
        "TaskCount": 7,
        "SampleTasks": ["Design login flow", "Implementer OAuth", "Frontend UI", "Backend API", "Test", "Deploy"],
        "AverageTaskDuration": 2.5,
        "CompletionRate": 100
      }
    ]
  },
  "Recommendations": {
    "SuggestedTaskCount": 9,
    "SuggestedDurationDays": 29,
    "KeyTasksToInclude": ["design", "implementering", "test", "dokumentation", "deploy"],
    "Notes": [
      "Husk at inkludere planlægnings- og designfaser før implementering",
      "Kvalitetssikring er en vigtig del af lignende projekter",
      "Baseret på historiske data anbefales at definere klare afhængigheder mellem opgaver",
      "Overvej at tilføje buffer tid (15-20%) for uforudsete komplikationer"
    ]
  }
}
```

---

## EstimateEffort

Få AI-estimat baseret på historiske data.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| description | string | Ja | Opgavebeskrivelse |
| featureList | string | Ja | Komma-separeret liste af features |
| teamSize | int | Nej | Teamstørrelse (default: 1) |
| riskTolerance | string | Nej | conservative/moderate/aggressive |

### Risk Tolerance

| Værdi | Beskrivelse |
|-------|-------------|
| conservative | Høj buffer, sikker estimering |
| moderate | Balanceret estimering (default) |
| aggressive | Optimistisk, minimal buffer |

### Request

```json
{
  "tool": "EstimateEffort",
  "tenantId": 1,
  "description": "E-commerce checkout flow",
  "featureList": "shopping cart, payment integration, order confirmation, email notifications",
  "teamSize": 2,
  "riskTolerance": "moderate"
}
```

### Response

```json
{
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
  },
  "Notes": [
    "Payment integration er typisk kompleks - overvej ekstra tid til test",
    "Email notifications kræver ofte flere iterationer"
  ]
}
```

---

## FindSimilarProjects

Find lignende projekter baseret på beskrivelse.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| description | string | Ja | Søgebeskrivelse |
| count | int | Nej | Max resultater (default: 5) |
| includePatterns | bool | Nej | Inkluder mønsteranalyse (default: true) |

### Request

```json
{
  "tool": "FindSimilarProjects",
  "tenantId": 1,
  "description": "website redesign corporate",
  "count": 5,
  "includePatterns": true
}
```

### Response

```json
{
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
        "Design fase tog længere end forventet",
        "Tidlig stakeholder involvering var kritisk"
      ]
    },
    {
      "ProjectId": 67,
      "ProjectName": "Redesign Company Site",
      "Similarity": 0.72,
      "Status": "Completed",
      "Duration": 60,
      "TotalHours": 280
    }
  ],
  "PatternAnalysis": {
    "CommonPhases": ["Discovery", "Design", "Development", "Testing", "Launch"],
    "AverageDuration": 75,
    "AverageHours": 365,
    "SuccessFactors": [
      "Klar scope definition fra start",
      "Regelmæssig kunde-feedback",
      "Dedikeret test fase"
    ]
  }
}
```

---

## Workflow Eksempler

### Vurder nyt projekt

```
Bruger: "Jeg skal estimere et nyt e-commerce projekt"

AI:
1. FindSimilarProjects(description: "e-commerce webshop")
2. SuggestTaskDecomposition(description: "e-commerce platform", projectType: "web")
3. EstimateEffort(description: "e-commerce", featureList: "product catalog, cart, checkout, admin panel")

"Baseret på 4 lignende projekter:
- Forventet varighed: 3-4 måneder
- Estimeret arbejde: 400-500 timer
- Anbefalet teamstørrelse: 2-3 udviklere
- Nøgleopgaver: [liste fra decomposition]"
```

### Projekt health check

```
Bruger: "Hvordan går Website projektet?"

AI:
1. AnalyzeProjectHealth(projectId: 101)

"Projektet er 'I Risiko' med score 72/100:

⚠️ 2 opgaver er forsinkede
⚠️ Hastigheden er lav (1.5 tasks/uge)
✅ Faktureringsgrad er god (81%)

Anbefalinger:
1. Hold standup om forsinkede opgaver
2. Opdel store opgaver i mindre dele

Forecast: 65% sandsynlighed for at nå deadline."
```
