using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Served.MCP.Tools.State;

[JsonConverter(typeof(StringEnumConverter))]
public enum SubTaskState { Todo, InProgress, Done, Abandoned }

[JsonConverter(typeof(StringEnumConverter))]
public enum PlanState { Todo, InProgress, Done, Abandoned }

public class SubTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public int Order { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public SubTaskState State { get; set; } = SubTaskState.Todo;
    public string? Result { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public class AgentPlan
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = "";
    public string? Goal { get; set; }
    public PlanState State { get; set; } = PlanState.Todo;
    public List<SubTask> SubTasks { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public List<string> RevisionNotes { get; set; } = new();

    public void RefreshState()
    {
        if (State is PlanState.Done or PlanState.Abandoned) return;

        var allDone = SubTasks.All(s => s.State is SubTaskState.Done or SubTaskState.Abandoned);
        var anyInProgress = SubTasks.Any(s => s.State == SubTaskState.InProgress);
        var anyDone = SubTasks.Any(s => s.State == SubTaskState.Done);

        if (allDone && SubTasks.Count > 0)
            State = PlanState.Done;
        else if (anyInProgress || anyDone)
            State = PlanState.InProgress;
        else
            State = PlanState.Todo;
    }

    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## Plan: {Title} [{State}]");
        if (!string.IsNullOrEmpty(Goal))
            sb.AppendLine($"**Goal:** {Goal}");
        sb.AppendLine();

        for (var i = 0; i < SubTasks.Count; i++)
        {
            var st = SubTasks[i];
            var marker = st.State switch
            {
                SubTaskState.Done => "[x]",
                SubTaskState.InProgress => "[ ]",
                SubTaskState.Abandoned => "[~]",
                _ => "[ ]"
            };
            var status = st.State switch
            {
                SubTaskState.Done => "Done",
                SubTaskState.InProgress => "**WIP**",
                SubTaskState.Abandoned => "~~Abandoned~~",
                _ => ""
            };
            var result = st.State == SubTaskState.Done && !string.IsNullOrEmpty(st.Result)
                ? $" — {st.Result}" : "";
            var statusSuffix = !string.IsNullOrEmpty(status) ? $" ({status})" : "";
            sb.AppendLine($"- {marker} {i + 1}. {st.Title}{statusSuffix}{result}");
        }

        var done = SubTasks.Count(s => s.State == SubTaskState.Done);
        var wip = SubTasks.Count(s => s.State == SubTaskState.InProgress);
        var abandoned = SubTasks.Count(s => s.State == SubTaskState.Abandoned);
        sb.AppendLine();
        sb.Append($"**Progress:** {done}/{SubTasks.Count} done");
        if (wip > 0) sb.Append($", {wip} in progress");
        if (abandoned > 0) sb.Append($", {abandoned} abandoned");
        sb.AppendLine();

        return sb.ToString();
    }
}

public class AgentState
{
    public string SessionId { get; set; } = "";
    public string? AgentId { get; set; }
    public string? CurrentTask { get; set; }
    public List<string> ActiveToolGroups { get; set; } = new();
    public AgentPlan? CurrentPlan { get; set; }
    public Dictionary<string, object?> CustomState { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;
}
