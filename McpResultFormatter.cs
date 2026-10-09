using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Served.MCP;

/// <summary>
/// Formats tool results as compact markdown instead of verbose JSON.
/// Tables show the columns an agent needs (id, name, status, dates, people, money) instead of the first
/// properties in declaration order, and always say which fields they left out.
/// </summary>
public static class McpResultFormatter
{
    private const int MaxColumns = 8;
    private const int MaxRows = 50;
    private const int MaxCellLength = 60;

    // Bookkeeping fields that never help an agent read a result
    private static readonly HashSet<string> NoiseFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "version", "rowVersion", "tenantId", "createdBy", "updatedBy", "createdById", "updatedById",
        "modifiedBy", "modifiedById", "deletedBy", "deletedAt", "isDeleted", "concurrencyStamp", "etag", "claimId"
    };

    // Column priority: earlier entries win. A field matches an entry exactly, or by prefix for entries ending in '*'.
    private static readonly string[] Priority =
    {
        "id", "name", "title", "displayName", "subject", "status", "workflowStatus", "state", "stage", "priority",
        "progress", "percentComplete", "dueDate", "plannedFinish", "startDate", "endDate", "deadline", "date",
        "assignee*", "assignedTo*", "owner*", "project*", "customer*", "amount", "total*", "hours", "price", "value"
    };

    /// <summary>
    /// Formats a tool result. Strings pass through as-is.
    /// </summary>
    /// <param name="result">The tool's return value.</param>
    /// <param name="detailsTool">A tool that returns one item with all fields, named when a table leaves fields out.</param>
    public static string Format(object? result, string? detailsTool = null)
    {
        if (result is string s) return s;

        var token = result == null ? JValue.CreateNull() : JToken.Parse(JsonConvert.SerializeObject(result));

        return token.Type switch
        {
            JTokenType.Array => FormatArray((JArray)token, detailsTool),
            JTokenType.Object => FormatObject((JObject)token, detailsTool),
            _ => token.ToString()
        };
    }

    public static string FormatArray(JArray arr, string? detailsTool = null)
    {
        if (arr.Count == 0) return "*(empty)*";

        var rows = arr.OfType<JObject>().ToList();
        if (rows.Count == 0)
            return string.Join(", ", arr.Select(x => x.ToString()));

        var shown = rows.Take(MaxRows).ToList();
        var (columns, omitted) = ChooseColumns(shown);

        var sb = new StringBuilder();
        sb.AppendLine($"**{arr.Count} items**\n");
        sb.AppendLine("| " + string.Join(" | ", columns) + " |");
        sb.AppendLine("| " + string.Join(" | ", columns.Select(_ => "---")) + " |");
        foreach (var row in shown)
        {
            sb.AppendLine("| " + string.Join(" | ", columns.Select(c => Cell(row[c]))) + " |");
        }
        if (arr.Count > MaxRows) sb.AppendLine($"\n*...and {arr.Count - MaxRows} more*");
        if (omitted.Count > 0)
        {
            sb.AppendLine($"\n*+{omitted.Count} more field{(omitted.Count == 1 ? "" : "s")}: {string.Join(", ", omitted)}" +
                          (detailsTool != null ? $" — use {detailsTool} for one item's full record*" : "*"));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Picks up to <see cref="MaxColumns"/> columns: drops noise and fields that are empty in every row,
    /// then orders by <see cref="Priority"/> (unlisted fields keep their order after the listed ones).
    /// Returns the omitted non-noise fields too.
    /// </summary>
    public static (List<string> Columns, List<string> Omitted) ChooseColumns(IReadOnlyList<JObject> rows)
    {
        var all = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
            foreach (var prop in row.Properties())
                if (seen.Add(prop.Name))
                    all.Add(prop.Name);

        var candidates = all
            .Where(name => !NoiseFields.Contains(name))
            .Where(name => rows.Any(r => HasValue(r[name])))
            .ToList();

        // Nested objects/arrays don't fit a table cell — they only count as omitted
        var scalar = candidates.Where(name => rows.All(r => r[name] is not (JObject or JArray))).ToList();

        var ordered = scalar
            .Select((name, index) => (name, rank: Rank(name), index))
            .OrderBy(c => c.rank)
            .ThenBy(c => c.index)
            .Select(c => c.name)
            .ToList();

        var columns = ordered.Take(MaxColumns).ToList();
        var omitted = candidates.Where(c => !columns.Contains(c)).ToList();
        return (columns, omitted);
    }

    private static int Rank(string field)
    {
        for (var i = 0; i < Priority.Length; i++)
        {
            var p = Priority[i];
            if (p.EndsWith('*')
                    ? field.StartsWith(p[..^1], StringComparison.OrdinalIgnoreCase)
                    : field.Equals(p, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return Priority.Length;
    }

    private static bool HasValue(JToken? v) => v switch
    {
        null => false,
        { Type: JTokenType.Null or JTokenType.Undefined } => false,
        { Type: JTokenType.String } => v.ToString().Length > 0,
        JArray a => a.Count > 0,
        JObject o => o.HasValues,
        _ => true
    };

    private static string Cell(JToken? v)
    {
        if (!HasValue(v)) return "-";
        var str = Scalar(v!).Replace("|", "\\|").Replace("\n", " ");
        return str.Length > MaxCellLength ? str[..(MaxCellLength - 3)] + "..." : str;
    }

    // Dates without time noise: midnight → date only, otherwise minutes precision
    private static string Scalar(JToken v)
    {
        if (v.Type == JTokenType.Date)
        {
            var d = v.Value<DateTime>();
            return d.TimeOfDay == TimeSpan.Zero
                ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
        return v.ToString(Formatting.None).Trim('"');
    }

    public static string FormatObject(JObject obj, string? detailsTool = null)
    {
        var sb = new StringBuilder();
        foreach (var prop in obj.Properties())
        {
            var val = prop.Value;
            if (!HasValue(val) || NoiseFields.Contains(prop.Name)) continue;

            if (val is JArray childArr && childArr[0] is JObject)
            {
                sb.AppendLine($"\n### {prop.Name}");
                sb.AppendLine(FormatArray(childArr, detailsTool));
            }
            else if (val is JArray simpleArr)
            {
                sb.AppendLine($"- **{prop.Name}**: {string.Join(", ", simpleArr.Select(Scalar))}");
            }
            else if (val is JObject childObj)
            {
                sb.AppendLine($"\n**{prop.Name}**:");
                foreach (var cp in childObj.Properties())
                {
                    if (HasValue(cp.Value) && !NoiseFields.Contains(cp.Name))
                        sb.AppendLine($"  - {cp.Name}: {(cp.Value is JObject or JArray ? cp.Value.ToString(Formatting.None) : Scalar(cp.Value))}");
                }
            }
            else
            {
                sb.AppendLine($"- **{prop.Name}**: {Scalar(val)}");
            }
        }
        return sb.ToString();
    }
}
