using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Served.SDK.Client;
using Served.SDK.Utilities;

namespace Served.MCP.Tools;

/// <summary>
/// Knowledge management MCP tools — vault access for Atlas.
/// Enables AI to read/search/write Obsidian vault data directly.
/// Self-contained: discovers vaults without CLI dependency.
///
/// BRICK: Decoration — MCP bridge for vault intelligence.
/// </summary>
public static class NotesTools
{
    private static Dictionary<string, VaultInfo>? _vaults;
    private static string? _activeVaultPath;

    private static void EnsureVaults()
    {
        if (_vaults != null) return;
        _vaults = DiscoverVaults();
        _activeVaultPath = _vaults.Values
            .Where(v => v.IsOpen)
            .Select(v => v.Path)
            .FirstOrDefault()
            ?? _vaults.Values.FirstOrDefault()?.Path;
    }

    public static void Register(McpServer server, ServedClient client)
    {
        // ── Read ──

        server.RegisterTool("VaultRead",
            "Read a note from the Obsidian vault. Returns full markdown content. Use file (wikilink name) or path (exact path).",
            async (args) =>
            {
                EnsureVaults();
                var file = args.GetOptionalString("file");
                var path = args.GetOptionalString("path");
                var vault = args.GetOptionalString("vault");

                if (string.IsNullOrEmpty(file) && string.IsNullOrEmpty(path))
                    return new { error = "Provide either 'file' (wikilink) or 'path' (exact path)" };

                var vaultPath = ResolveVaultPath(vault);
                if (vaultPath == null)
                    return new { error = "No vault found" };

                var filePath = ResolveFilePath(vaultPath, file, path);
                if (filePath == null || !File.Exists(filePath))
                    return new { error = $"Note not found: {file ?? path}" };

                var content = await File.ReadAllTextAsync(filePath);
                return (object)new { content, path = Path.GetRelativePath(vaultPath, filePath) };
            },
            JObject.Parse(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""file"": { ""type"": ""string"", ""description"": ""Note name (wikilink resolution)"" },
                    ""path"": { ""type"": ""string"", ""description"": ""Exact file path relative to vault"" },
                    ""vault"": { ""type"": ""string"", ""description"": ""Target vault name (optional)"" }
                },
                ""required"": []
            }"));

        // ── Search ──

        server.RegisterTool("VaultSearch",
            "Search across all notes in the Obsidian vault. Returns matching files with context lines. Use for finding relevant knowledge.",
            async (args) =>
            {
                EnsureVaults();
                var query = args.GetRequiredString("query");
                var folder = args.GetOptionalString("path");
                var limit = args.GetOptionalInt("limit", 20);
                var vault = args.GetOptionalString("vault");

                var vaultPath = ResolveVaultPath(vault);
                if (vaultPath == null)
                    return new { error = "No vault found", results = 0 };

                var searchPath = folder != null ? Path.Combine(vaultPath, folder) : vaultPath;
                if (!Directory.Exists(searchPath))
                    return new { error = $"Path not found: {folder}", results = 0 };

                var files = Directory.GetFiles(searchPath, "*.md", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("/.obsidian/"))
                    .ToArray();

                var sb = new StringBuilder();
                var count = 0;
                var pattern = new Regex(Regex.Escape(query), RegexOptions.IgnoreCase);

                foreach (var file in files)
                {
                    if (count >= limit) break;
                    var content = await File.ReadAllTextAsync(file);
                    if (!pattern.IsMatch(content)) continue;

                    var relative = Path.GetRelativePath(vaultPath, file);
                    sb.AppendLine($"--- {relative} ---");

                    var lines = content.Split('\n');
                    for (var i = 0; i < lines.Length; i++)
                    {
                        if (pattern.IsMatch(lines[i]))
                        {
                            sb.AppendLine($"  L{i + 1}: {lines[i].TrimEnd()}");
                        }
                    }
                    sb.AppendLine();
                    count++;
                }

                return (object)new { content = sb.ToString(), results = count, query };
            },
            JObject.Parse(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""query"": { ""type"": ""string"", ""description"": ""Search query (searches file content)"" },
                    ""path"": { ""type"": ""string"", ""description"": ""Limit search to folder path"" },
                    ""limit"": { ""type"": ""integer"", ""description"": ""Max results (default: 20)"" },
                    ""vault"": { ""type"": ""string"", ""description"": ""Target vault name"" }
                },
                ""required"": [""query""]
            }"));

        // ── Daily Notes ──

        server.RegisterTool("VaultDailyRead",
            "Read today's daily note from the Obsidian vault. Returns the full content of the daily note.",
            async (args) =>
            {
                EnsureVaults();
                var vault = args.GetOptionalString("vault");
                var vaultPath = ResolveVaultPath(vault);
                if (vaultPath == null)
                    return new { error = "No vault found" };

                var dailyPath = FindDailyNote(vaultPath);
                if (dailyPath == null || !File.Exists(dailyPath))
                    return new { error = "No daily note found for today" };

                var content = await File.ReadAllTextAsync(dailyPath);
                return (object)new { content, date = DateTime.Now.ToString("yyyy-MM-dd") };
            },
            JObject.Parse(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""vault"": { ""type"": ""string"", ""description"": ""Target vault name"" }
                },
                ""required"": []
            }"));

        server.RegisterTool("VaultDailyAppend",
            "Append content to today's daily note. Creates the daily note if it doesn't exist. Use for capturing ideas, task updates, or notes.",
            async (args) =>
            {
                EnsureVaults();
                var content = args.GetRequiredString("content");
                var vault = args.GetOptionalString("vault");

                var vaultPath = ResolveVaultPath(vault);
                if (vaultPath == null)
                    return new { error = "No vault found" };

                var dailyPath = FindDailyNote(vaultPath, createIfMissing: true);
                if (dailyPath == null)
                    return new { error = "Could not create daily note" };

                await File.AppendAllTextAsync(dailyPath, "\n" + content + "\n");
                return (object)new { success = true, message = "Appended to daily note", date = DateTime.Now.ToString("yyyy-MM-dd") };
            },
            JObject.Parse(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""content"": { ""type"": ""string"", ""description"": ""Content to append to today's daily note"" },
                    ""vault"": { ""type"": ""string"", ""description"": ""Target vault name"" }
                },
                ""required"": [""content""]
            }"));

        // ── Vault Status ──

        server.RegisterTool("VaultStatus",
            "Get vault overview — available vaults, file counts, active vault info. Use this to understand what knowledge vaults are available.",
            async (args) =>
            {
                EnsureVaults();

                var sb = new StringBuilder();
                sb.AppendLine("@vaultStatus {");

                if (_vaults!.Count > 0)
                {
                    sb.AppendLine($"  vaults: [{_vaults.Count}] {{");
                    foreach (var v in _vaults.Values)
                    {
                        sb.AppendLine($"    @vault {{ name: \"{v.Name}\", open: {v.IsOpen.ToString().ToLower()}, path: \"{v.Path}\" }}");
                    }
                    sb.AppendLine("  }");
                }
                else
                {
                    sb.AppendLine("  vaults: []");
                    sb.AppendLine("  status: \"No Obsidian vaults found\"");
                }

                sb.AppendLine("}");
                return sb.ToString();
            },
            JObject.Parse(@"{
                ""type"": ""object"",
                ""properties"": {},
                ""required"": []
            }"));

        // ── Tasks ──

        server.RegisterTool("VaultTasks",
            "Find tasks (checkboxes) across vault notes. Returns todo and done items with file locations.",
            async (args) =>
            {
                EnsureVaults();
                var vault = args.GetOptionalString("vault");
                var todo = args.GetOptionalBool("todo", false);
                var done = args.GetOptionalBool("done", false);
                var limit = args.GetOptionalInt("limit", 50);

                var vaultPath = ResolveVaultPath(vault);
                if (vaultPath == null)
                    return new { error = "No vault found" };

                var files = Directory.GetFiles(vaultPath, "*.md", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("/.obsidian/"))
                    .ToArray();

                var taskPattern = new Regex(@"^(\s*)-\s*\[([ xX])\]\s*(.+)$", RegexOptions.Multiline);
                var sb = new StringBuilder();
                var totalTodo = 0;
                var totalDone = 0;

                foreach (var file in files)
                {
                    var content = await File.ReadAllTextAsync(file);
                    var matches = taskPattern.Matches(content);
                    if (matches.Count == 0) continue;

                    var relative = Path.GetRelativePath(vaultPath, file);
                    var fileTasks = new List<string>();

                    foreach (Match m in matches)
                    {
                        var isDone = m.Groups[2].Value != " ";
                        var text = m.Groups[3].Value.Trim();

                        if (isDone) totalDone++;
                        else totalTodo++;

                        if (todo && isDone) continue;
                        if (done && !isDone) continue;

                        fileTasks.Add($"  [{(isDone ? "x" : " ")}] {text}");
                    }

                    if (fileTasks.Count > 0 && sb.ToString().Split('\n').Length < limit + 10)
                    {
                        sb.AppendLine($"--- {relative} ---");
                        foreach (var t in fileTasks) sb.AppendLine(t);
                        sb.AppendLine();
                    }
                }

                return (object)new { content = sb.ToString(), todo = totalTodo, done = totalDone, total = totalTodo + totalDone };
            },
            JObject.Parse(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""todo"": { ""type"": ""boolean"", ""description"": ""Only show uncompleted tasks"" },
                    ""done"": { ""type"": ""boolean"", ""description"": ""Only show completed tasks"" },
                    ""limit"": { ""type"": ""integer"", ""description"": ""Max output lines (default: 50)"" },
                    ""vault"": { ""type"": ""string"", ""description"": ""Target vault name"" }
                },
                ""required"": []
            }"));

        // ── Tags ──

        server.RegisterTool("VaultTags",
            "List all tags used across vault notes with frequency counts. Use to understand knowledge categorization.",
            async (args) =>
            {
                EnsureVaults();
                var vault = args.GetOptionalString("vault");

                var vaultPath = ResolveVaultPath(vault);
                if (vaultPath == null)
                    return new { error = "No vault found" };

                var files = Directory.GetFiles(vaultPath, "*.md", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("/.obsidian/"))
                    .ToArray();

                var tagPattern = new Regex(@"(?:^|\s)#([a-zA-Z][a-zA-Z0-9_/-]*)");
                var tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var file in files)
                {
                    var content = await File.ReadAllTextAsync(file);
                    foreach (Match m in tagPattern.Matches(content))
                    {
                        var tag = m.Groups[1].Value;
                        tagCounts[tag] = tagCounts.GetValueOrDefault(tag) + 1;
                    }
                }

                var sorted = tagCounts.OrderByDescending(kv => kv.Value).ToList();
                var sb = new StringBuilder();
                foreach (var kv in sorted)
                {
                    sb.AppendLine($"#{kv.Key} ({kv.Value})");
                }

                return (object)new { content = sb.ToString(), total = sorted.Count };
            },
            JObject.Parse(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""vault"": { ""type"": ""string"", ""description"": ""Target vault name"" }
                },
                ""required"": []
            }"));

        // ── Backlinks ──

        server.RegisterTool("VaultBacklinks",
            "Find all notes that link TO a specific note. Returns incoming wikilink references. Use to understand how knowledge connects.",
            async (args) =>
            {
                EnsureVaults();
                var file = args.GetRequiredString("file");
                var vault = args.GetOptionalString("vault");

                var vaultPath = ResolveVaultPath(vault);
                if (vaultPath == null)
                    return new { error = "No vault found" };

                var filePath = ResolveFilePath(vaultPath, file, null);
                if (filePath == null)
                    return new { error = $"Note not found: {file}" };

                var targetName = Path.GetFileNameWithoutExtension(filePath);
                var linkPattern = new Regex(
                    $@"\[\[([^\]]*\/)?{Regex.Escape(targetName)}(\|[^\]]+)?\]\]",
                    RegexOptions.IgnoreCase);

                var allFiles = Directory.GetFiles(vaultPath, "*.md", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("/.obsidian/") && f != filePath)
                    .ToArray();

                var sb = new StringBuilder();
                var count = 0;

                foreach (var f in allFiles)
                {
                    var content = await File.ReadAllTextAsync(f);
                    var matches = linkPattern.Matches(content);
                    if (matches.Count == 0) continue;

                    var relative = Path.GetRelativePath(vaultPath, f);
                    sb.AppendLine($"{relative} ({matches.Count})");
                    count++;
                }

                return (object)new { content = sb.ToString(), backlinks = count, note = file };
            },
            JObject.Parse(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""file"": { ""type"": ""string"", ""description"": ""Note name to find backlinks for"" },
                    ""vault"": { ""type"": ""string"", ""description"": ""Target vault name"" }
                },
                ""required"": [""file""]
            }"));

        // ── Unresolved ──

        server.RegisterTool("VaultUnresolved",
            "Find broken/unresolved wikilinks across the vault. Returns dead links sorted by frequency. Use for vault health checks.",
            async (args) =>
            {
                EnsureVaults();
                var vault = args.GetOptionalString("vault");
                var limit = args.GetOptionalInt("limit", 30);

                var vaultPath = ResolveVaultPath(vault);
                if (vaultPath == null)
                    return new { error = "No vault found" };

                var wikiLinkRegex = new Regex(@"\[\[([^\]|#]+)(?:[#|][^\]]*)?\]\]");
                var mdFiles = Directory.GetFiles(vaultPath, "*.md", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("/.obsidian/"))
                    .ToArray();

                var noteNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in mdFiles)
                    noteNames.Add(Path.GetFileNameWithoutExtension(f));

                var unresolved = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var f in mdFiles)
                {
                    var content = await File.ReadAllTextAsync(f);
                    foreach (Match m in wikiLinkRegex.Matches(content))
                    {
                        var target = m.Groups[1].Value.Trim();
                        if (target.Contains('/'))
                        {
                            var fullPath = Path.Combine(vaultPath, target.EndsWith(".md") ? target : $"{target}.md");
                            if (File.Exists(fullPath)) continue;
                        }
                        else if (noteNames.Contains(target))
                        {
                            continue;
                        }

                        unresolved[target] = unresolved.GetValueOrDefault(target) + 1;
                    }
                }

                var sorted = unresolved.OrderByDescending(kv => kv.Value).Take(limit);
                var sb = new StringBuilder();
                foreach (var kv in sorted)
                    sb.AppendLine($"[[{kv.Key}]] ({kv.Value})");

                return (object)new { content = sb.ToString(), total = unresolved.Count };
            },
            JObject.Parse(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""limit"": { ""type"": ""integer"", ""description"": ""Max results (default: 30)"" },
                    ""vault"": { ""type"": ""string"", ""description"": ""Target vault name"" }
                },
                ""required"": []
            }"));

        // ── Create ──

        server.RegisterTool("VaultCreate",
            "Create a new note in the Obsidian vault. Content should be markdown.",
            async (args) =>
            {
                EnsureVaults();
                var name = args.GetRequiredString("name");
                var content = args.GetOptionalString("content");
                var path = args.GetOptionalString("path");
                var vault = args.GetOptionalString("vault");

                var vaultPath = ResolveVaultPath(vault);
                if (vaultPath == null)
                    return new { error = "No vault found" };

                var fileName = name.EndsWith(".md") ? name : $"{name}.md";
                var filePath = path != null
                    ? Path.Combine(vaultPath, path)
                    : Path.Combine(vaultPath, fileName);

                if (File.Exists(filePath))
                    return new { error = $"Note already exists: {fileName}" };

                var dir = Path.GetDirectoryName(filePath);
                if (dir != null && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                await File.WriteAllTextAsync(filePath, content ?? $"# {name}\n");
                return (object)new { success = true, message = $"Created: {fileName}" };
            },
            JObject.Parse(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""name"": { ""type"": ""string"", ""description"": ""Note name (without .md extension)"" },
                    ""content"": { ""type"": ""string"", ""description"": ""Note content (markdown)"" },
                    ""path"": { ""type"": ""string"", ""description"": ""Full path for the note (relative to vault)"" },
                    ""vault"": { ""type"": ""string"", ""description"": ""Target vault name"" }
                },
                ""required"": [""name""]
            }"));
    }

    // ═══════════════════════════════════════════════════════════════
    // Internal: Vault Discovery (self-contained, no CLI dependency)
    // ═══════════════════════════════════════════════════════════════

    private static string? ResolveVaultPath(string? vaultName)
    {
        if (string.IsNullOrEmpty(vaultName))
            return _activeVaultPath;

        var match = _vaults?.Values
            .FirstOrDefault(v => v.Name.Equals(vaultName, StringComparison.OrdinalIgnoreCase));
        return match?.Path;
    }

    private static string? ResolveFilePath(string vaultPath, string? file, string? path)
    {
        if (path != null)
        {
            var full = Path.Combine(vaultPath, path);
            return File.Exists(full) ? full : null;
        }

        if (file == null) return null;

        // Wikilink resolution
        var fileName = file.EndsWith(".md") ? file : $"{file}.md";
        var direct = Path.Combine(vaultPath, fileName);
        if (File.Exists(direct)) return direct;

        // Search recursively
        var matches = Directory.GetFiles(vaultPath, fileName, SearchOption.AllDirectories)
            .Where(f => !f.Contains("/.obsidian/"))
            .ToArray();

        return matches.Length > 0 ? matches[0] : null;
    }

    private static string? FindDailyNote(string vaultPath, bool createIfMissing = false)
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var candidates = new[]
        {
            Path.Combine(vaultPath, $"{today}.md"),
            Path.Combine(vaultPath, "Daily", $"{today}.md"),
            Path.Combine(vaultPath, "daily", $"{today}.md"),
            Path.Combine(vaultPath, "Daily Notes", $"{today}.md"),
            Path.Combine(vaultPath, "Journal", $"{today}.md"),
        };

        var existing = candidates.FirstOrDefault(File.Exists);
        if (existing != null) return existing;

        if (!createIfMissing) return null;

        // Create in first existing daily folder, or root
        var dailyDir = new[] { "Daily", "daily", "Daily Notes", "Journal" }
            .Select(f => Path.Combine(vaultPath, f))
            .FirstOrDefault(Directory.Exists);

        var newPath = dailyDir != null
            ? Path.Combine(dailyDir, $"{today}.md")
            : Path.Combine(vaultPath, $"{today}.md");

        File.WriteAllText(newPath, $"# {today}\n\n");
        return newPath;
    }

    private static Dictionary<string, VaultInfo> DiscoverVaults()
    {
        var result = new Dictionary<string, VaultInfo>(StringComparer.OrdinalIgnoreCase);
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "obsidian", "obsidian.json");

        if (!File.Exists(configPath)) return result;

        try
        {
            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("vaults", out var vaults))
            {
                foreach (var vault in vaults.EnumerateObject())
                {
                    var id = vault.Name;
                    var obj = vault.Value;
                    var vaultPath = obj.TryGetProperty("path", out var p) ? p.GetString() : null;
                    var isOpen = obj.TryGetProperty("open", out var o) && o.GetBoolean();

                    if (vaultPath == null || !Directory.Exists(vaultPath)) continue;

                    var name = Path.GetFileName(vaultPath);
                    result[name] = new VaultInfo { Id = id, Name = name, Path = vaultPath, IsOpen = isOpen };
                }
            }
        }
        catch { }

        return result;
    }

    private class VaultInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public bool IsOpen { get; set; }
    }
}
