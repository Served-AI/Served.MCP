# Time Tracking Tools

AI-drevet tidsregistrering via MCP.

---

## SuggestTimeEntries

Få AI-genererede forslag til tidsregistrering baseret på historiske mønstre og kalenderevents.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| startDate | string | Ja | Fra dato (YYYY-MM-DD) |
| endDate | string | Ja | Til dato (YYYY-MM-DD) |
| includeCalendar | bool | Nej | Inkluder kalenderevents (default: true) |
| minConfidence | double | Nej | Minimum sikkerhed 0.0-1.0 (default: 0.5) |

### Request

```json
{
  "tool": "SuggestTimeEntries",
  "tenantId": 1,
  "startDate": "2025-01-20",
  "endDate": "2025-01-24",
  "includeCalendar": true,
  "minConfidence": 0.5
}
```

### Response

```json
{
  "Type": "time_entry_suggestions",
  "Count": 5,
  "TotalHours": 35.5,
  "PatternCount": 3,
  "CalendarEventCount": 2,
  "Summary": "Analyseret 90 dages historik. Fandt 3 tidsmønstre og 2 kalenderevents.",
  "Suggestions": [
    {
      "Dato": "2025-01-20",
      "ProjektId": 101,
      "ProjektNavn": "Website Redesign",
      "OpgaveId": 201,
      "OpgaveNavn": "Frontend udvikling",
      "ForeslåedeTimer": 7.5,
      "Sikkerhed": "85%",
      "Kilde": "HistoricalPattern",
      "Begrundelse": "Du arbejder typisk 7.5 timer på denne opgave om mandagen",
      "Beskrivelse": "Frontend udvikling"
    },
    {
      "Dato": "2025-01-21",
      "ProjektId": 101,
      "ProjektNavn": "Website Redesign",
      "OpgaveId": 202,
      "OpgaveNavn": "Backend API",
      "ForeslåedeTimer": 4.0,
      "Sikkerhed": "70%",
      "Kilde": "CalendarEvent",
      "Begrundelse": "Baseret på kalenderaftale 'API Workshop'",
      "Beskrivelse": "Backend udvikling"
    }
  ]
}
```

### Confidence Levels

| Score | Niveau | Betydning |
|-------|--------|-----------|
| >= 0.8 | Høj | Stærkt mønster, pålidelig |
| 0.6-0.79 | Medium | Sandsynligt, men tjek |
| 0.5-0.59 | Lav | Usikker, bruger bør verificere |

### Suggestion Sources

| Kilde | Beskrivelse |
|-------|-------------|
| HistoricalPattern | Baseret på tidligere registreringer |
| CalendarEvent | Baseret på kalenderaftale |
| GapFill | Udfylder huller i tidsregistrering |
| ProjectDeadline | Projekt nærmer sig deadline |
| RecurringTask | Tilbagevendende opgave |

---

## AnalyzeTimePatterns

Analyser brugerens tidsmønstre over en periode.

### Parametre

| Parameter | Type | Påkrævet | Beskrivelse |
|-----------|------|----------|-------------|
| tenantId | int | Ja | Workspace ID |
| lookbackDays | int | Nej | Dage at analysere (default: 90) |

### Request

```json
{
  "tool": "AnalyzeTimePatterns",
  "tenantId": 1,
  "lookbackDays": 90
}
```

### Response

```json
{
  "Type": "time_patterns",
  "Count": 4,
  "LookbackDays": 90,
  "Patterns": [
    {
      "ProjektId": 101,
      "ProjektNavn": "Website Redesign",
      "OpgaveId": 201,
      "OpgaveNavn": "Frontend udvikling",
      "ForetrukkenDag": "Mandag",
      "GennemsnitligeTimer": "6.5",
      "Hyppighed": 12,
      "SidsteGang": "2025-01-13"
    },
    {
      "ProjektId": 101,
      "ProjektNavn": "Website Redesign",
      "OpgaveId": 202,
      "OpgaveNavn": "Backend API",
      "ForetrukkenDag": "Onsdag",
      "GennemsnitligeTimer": "5.0",
      "Hyppighed": 8,
      "SidsteGang": "2025-01-15"
    }
  ],
  "Summary": "Fundet 4 tidsmønstre baseret på de seneste 90 dage."
}
```

### Pattern Felter

| Felt | Beskrivelse |
|------|-------------|
| ForetrukkenDag | Dag hvor brugeren typisk arbejder på opgaven |
| GennemsnitligeTimer | Gennemsnitlig tid per session |
| Hyppighed | Antal gange opgaven er registreret |
| SidsteGang | Sidste registrering på opgaven |

---

## Workflow Eksempler

### Udfyld ugens tidsregistrering

```
Bruger: "Hjælp mig med at udfylde min tidsregistrering for denne uge"

AI:
1. SuggestTimeEntries(tenantId: 1, startDate: "2025-01-20", endDate: "2025-01-24")
2. "Baseret på dine mønstre og kalender, foreslår jeg:

   Mandag 20/1:
   - Website Redesign / Frontend: 7.5 timer (høj sikkerhed)

   Tirsdag 21/1:
   - Website Redesign / Backend: 4.0 timer (kalenderbaseret)
   - Mobile App / Design: 3.5 timer (mønster)

   ...

   Skal jeg oprette disse registreringer?"
```

### Forstå arbejdsmønstre

```
Bruger: "Hvordan ser mine arbejdsmønstre ud?"

AI:
1. AnalyzeTimePatterns(tenantId: 1, lookbackDays: 90)
2. "Over de sidste 90 dage har du:

   - Mandag: Primært frontend arbejde (~6.5 timer)
   - Onsdag: Backend fokus (~5 timer)
   - Fredag: Blandet med kortere sessioner

   Dit mest aktive projekt er 'Website Redesign' med 120 timer totalt."
```

---

## Fejlhåndtering

### Ugyldig dato

```
Fejl: Ugyldig startdato. Brug ISO format (yyyy-MM-dd).
```

### Slutdato før startdato

```
Fejl: Slutdato skal være efter startdato.
```

### Ingen mønstre fundet

```
Ingen tidsmønstre fundet. Brugeren har ikke nok historiske tidsregistreringer.
```

### Ingen forslag

```json
{
  "Type": "time_entry_suggestions",
  "Count": 0,
  "Message": "Ingen tidsregistreringsforslag fundet for den valgte periode."
}
```
