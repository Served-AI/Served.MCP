using Newtonsoft.Json;

namespace Served.MCP.Tools.State;

/// <summary>
/// Filesystem-backed state persistence for MCP agent sessions.
/// Stores state as JSON in .served/agent-states/{sessionId}.json.
/// Uses atomic temp+move writes to prevent corruption.
/// </summary>
public class AgentStateManager
{
    private readonly McpServer _server;
    private readonly ToolGroupRegistry _registry;
    private readonly string _statesDir;
    private AgentState? _current;
    private bool _dirty;
    private Timer? _autoSaveTimer;

    public AgentStateManager(McpServer server, ToolGroupRegistry registry)
    {
        _server = server;
        _registry = registry;
        _statesDir = FindStatesDir();
        Directory.CreateDirectory(_statesDir);
    }

    /// <summary>
    /// Get or create state for the current session.
    /// Loads from disk if a matching session file exists.
    /// </summary>
    public AgentState GetOrCreate()
    {
        if (_current != null) return _current;

        var sessionId = _server.SessionId ?? Guid.NewGuid().ToString("N")[..12];
        var path = GetPath(sessionId);

        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                _current = JsonConvert.DeserializeObject<AgentState>(json);
                if (_current != null)
                {
                    Console.Error.WriteLine($"[MCP] Restored agent state: {sessionId}");
                    // Re-activate tool groups from saved state
                    foreach (var group in _current.ActiveToolGroups)
                        _registry.ActivateGroup(group);
                    return _current;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MCP] Failed to restore state: {ex.Message}");
            }
        }

        _current = new AgentState
        {
            SessionId = sessionId,
            AgentId = _server.AgentId
        };
        return _current;
    }

    /// <summary>
    /// Load a specific session's state from disk.
    /// </summary>
    public AgentState? Load(string sessionId)
    {
        var path = GetPath(sessionId);
        if (!File.Exists(path)) return null;

        try
        {
            return JsonConvert.DeserializeObject<AgentState>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Save the current state to disk. Snapshots active tool groups.
    /// </summary>
    public void Save()
    {
        if (_current == null) return;

        _current.UpdatedAt = DateTime.UtcNow;
        _current.ActiveToolGroups = _registry.ListGroups()
            .Where(g => g.Active && !g.AlwaysActive)
            .Select(g => g.Name)
            .ToList();

        var path = GetPath(_current.SessionId);
        var json = JsonConvert.SerializeObject(_current, Formatting.Indented);

        // Atomic write: temp file → move
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);

        _dirty = false;
    }

    /// <summary>
    /// Mark state as needing a save on the next auto-save tick.
    /// </summary>
    public void MarkDirty()
    {
        _dirty = true;
    }

    /// <summary>
    /// Save if dirty, then mark clean.
    /// </summary>
    public void SaveIfDirty()
    {
        if (_dirty) Save();
    }

    /// <summary>
    /// List all saved agent states.
    /// </summary>
    public List<(string SessionId, DateTime UpdatedAt, string? Task, bool HasPlan)> ListStates()
    {
        var results = new List<(string, DateTime, string?, bool)>();
        if (!Directory.Exists(_statesDir)) return results;

        foreach (var file in Directory.GetFiles(_statesDir, "*.json"))
        {
            try
            {
                var state = JsonConvert.DeserializeObject<AgentState>(File.ReadAllText(file));
                if (state != null)
                    results.Add((state.SessionId, state.UpdatedAt, state.CurrentTask, state.CurrentPlan != null));
            }
            catch { /* skip corrupt files */ }
        }

        return results.OrderByDescending(r => r.Item2).ToList();
    }

    /// <summary>
    /// Delete state files older than the given age.
    /// </summary>
    public int Cleanup(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var deleted = 0;
        foreach (var file in Directory.GetFiles(_statesDir, "*.json"))
        {
            if (File.GetLastWriteTimeUtc(file) < cutoff)
            {
                File.Delete(file);
                deleted++;
            }
        }
        return deleted;
    }

    /// <summary>
    /// Start auto-save on a periodic interval.
    /// </summary>
    public void StartAutoSave(TimeSpan interval)
    {
        _autoSaveTimer = new Timer(_ => SaveIfDirty(), null, interval, interval);
        Console.Error.WriteLine($"[MCP] Auto-save started ({interval.TotalMinutes:0}m interval)");
    }

    /// <summary>
    /// Stop auto-save and do a final save.
    /// </summary>
    public void StopAutoSave()
    {
        _autoSaveTimer?.Dispose();
        _autoSaveTimer = null;
        if (_current != null && _dirty)
            Save();
    }

    private string GetPath(string sessionId)
        => Path.Combine(_statesDir, $"{SanitizeFileName(sessionId)}.json");

    private static string SanitizeFileName(string name)
        => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private static string FindStatesDir()
    {
        var dir = Directory.GetCurrentDirectory();
        for (var i = 0; i < 10; i++)
        {
            var servedDir = Path.Combine(dir, ".served");
            if (Directory.Exists(servedDir))
                return Path.Combine(servedDir, "agent-states");
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return Path.Combine(Directory.GetCurrentDirectory(), ".served", "agent-states");
    }
}
