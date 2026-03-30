using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Served.MCP.Tools.Services;

/// <summary>
/// BRICK: Walls - Shared Suno AI music generation service
/// Used by both MCP tools (SunoTools.cs) and CLI (SunoCommand.cs)
/// Provider-agnostic: configurable base URL + API key
/// </summary>
public class SunoService : IDisposable
{
    private readonly HttpClient _http;
    private readonly SunoConfig _config;

    public SunoService() : this(LoadConfig()) { }

    public SunoService(SunoConfig config)
    {
        _config = config;
        _http = new HttpClient
        {
            BaseAddress = new Uri(_config.BaseUrl.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(180)
        };
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
    }

    /// <summary>
    /// Generate a song from a prompt. Returns task ID + initial clip info.
    /// </summary>
    public async Task<SunoGenerateResult> GenerateAsync(
        string prompt,
        string? style = null,
        string? title = null,
        string? lyrics = null,
        bool instrumental = false,
        string model = "V3_5")
    {
        var isCustomMode = !string.IsNullOrEmpty(style) || !string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(lyrics);

        var payload = new JObject
        {
            ["prompt"] = isCustomMode ? (lyrics ?? prompt) : prompt,
            ["customMode"] = isCustomMode,
            ["makeInstrumental"] = instrumental,
            ["model"] = model
        };

        if (isCustomMode)
        {
            if (!string.IsNullOrEmpty(style)) payload["tags"] = style;
            if (!string.IsNullOrEmpty(title)) payload["title"] = title;
        }

        var response = await PostAsync("/api/v1/generate", payload);
        var data = response["data"] ?? throw new Exception($"Suno API error: {response}");

        return new SunoGenerateResult
        {
            TaskId = data["taskId"]?.Value<string>() ?? "",
            Clips = ParseClips(data["clips"] as JArray)
        };
    }

    /// <summary>
    /// Check generation status by task ID.
    /// </summary>
    public async Task<SunoTaskStatus> GetStatusAsync(string taskId)
    {
        var response = await GetAsync($"/api/v1/generate/record-info?taskId={Uri.EscapeDataString(taskId)}");
        var data = response["data"] ?? throw new Exception($"Suno API error: {response}");

        return new SunoTaskStatus
        {
            Status = data["status"]?.Value<string>() ?? "unknown",
            Clips = ParseClips(data["clips"] as JArray)
        };
    }

    /// <summary>
    /// Generate lyrics from a theme/prompt.
    /// </summary>
    public async Task<SunoLyricsResult> GenerateLyricsAsync(string prompt)
    {
        var payload = new JObject { ["prompt"] = prompt };
        var response = await PostAsync("/api/v1/generate/lyrics", payload);
        var data = response["data"] ?? throw new Exception($"Suno API error: {response}");

        return new SunoLyricsResult
        {
            Text = data["text"]?.Value<string>() ?? data["lyrics"]?.Value<string>() ?? "",
            Title = data["title"]?.Value<string>() ?? ""
        };
    }

    /// <summary>
    /// Extend an existing clip.
    /// </summary>
    public async Task<SunoGenerateResult> ExtendAsync(string clipId, string? prompt = null, int? continueAt = null)
    {
        var payload = new JObject
        {
            ["clipId"] = clipId
        };
        if (!string.IsNullOrEmpty(prompt)) payload["prompt"] = prompt;
        if (continueAt.HasValue) payload["continueAt"] = continueAt.Value;

        var response = await PostAsync("/api/v1/generate/extend", payload);
        var data = response["data"] ?? throw new Exception($"Suno API error: {response}");

        return new SunoGenerateResult
        {
            TaskId = data["taskId"]?.Value<string>() ?? "",
            Clips = ParseClips(data["clips"] as JArray)
        };
    }

    /// <summary>
    /// Check remaining credits.
    /// </summary>
    public async Task<SunoCreditsResult> GetCreditsAsync()
    {
        var response = await GetAsync("/api/v1/generate/credits");
        var data = response["data"] ?? response;

        return new SunoCreditsResult
        {
            Remaining = data["remaining"]?.Value<int>() ?? data["creditsLeft"]?.Value<int>() ?? 0,
            Total = data["total"]?.Value<int>() ?? data["creditsTotal"]?.Value<int>() ?? 0,
            Plan = data["plan"]?.Value<string>() ?? "unknown"
        };
    }

    /// <summary>
    /// Download audio file to disk.
    /// </summary>
    public async Task<string> DownloadAsync(string audioUrl, string? outputPath = null)
    {
        if (string.IsNullOrEmpty(outputPath))
        {
            var fileName = $"suno-{DateTime.Now:yyyyMMdd-HHmmss}.mp3";
            outputPath = Path.Combine(_config.DownloadDir, fileName);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        using var downloadClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var bytes = await downloadClient.GetByteArrayAsync(audioUrl);
        await File.WriteAllBytesAsync(outputPath, bytes);

        return outputPath;
    }

    /// <summary>
    /// Poll until generation is complete or timeout.
    /// </summary>
    public async Task<SunoClip[]> PollUntilComplete(string taskId, int timeoutSeconds = 120, int intervalSeconds = 5, Action<string>? onStatus = null)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            var status = await GetStatusAsync(taskId);
            onStatus?.Invoke(status.Status);

            if (status.Status == "complete")
                return status.Clips;

            if (status.Status == "error")
                throw new Exception($"Suno generation failed for task {taskId}");

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
        }

        throw new TimeoutException($"Suno generation timed out after {timeoutSeconds}s for task {taskId}");
    }

    // ─── History ──────────────────────────────────────────────────────

    public static void RecordHistory(SunoHistoryEntry entry)
    {
        var config = LoadConfig();
        var historyPath = Path.Combine(config.DownloadDir, "..", "history.json");
        historyPath = Path.GetFullPath(historyPath);
        Directory.CreateDirectory(Path.GetDirectoryName(historyPath)!);

        var entries = LoadHistory();
        entries.Add(entry);

        // Keep last 100
        if (entries.Count > 100)
            entries = entries.Skip(entries.Count - 100).ToList();

        File.WriteAllText(historyPath, JsonConvert.SerializeObject(entries, Formatting.Indented));
    }

    public static List<SunoHistoryEntry> LoadHistory()
    {
        var config = LoadConfig();
        var historyPath = Path.Combine(config.DownloadDir, "..", "history.json");
        historyPath = Path.GetFullPath(historyPath);

        if (!File.Exists(historyPath))
            return new List<SunoHistoryEntry>();

        var json = File.ReadAllText(historyPath);
        return JsonConvert.DeserializeObject<List<SunoHistoryEntry>>(json) ?? new List<SunoHistoryEntry>();
    }

    // ─── Config ───────────────────────────────────────────────────────

    public static SunoConfig LoadConfig()
    {
        var apiKey = Environment.GetEnvironmentVariable("SUNO_API_KEY") ?? "";
        var baseUrl = Environment.GetEnvironmentVariable("SUNO_API_URL") ?? "";

        // Fallback to config file
        var configPath = GetConfigPath();
        JObject? fileConfig = null;
        if (File.Exists(configPath))
        {
            fileConfig = JObject.Parse(File.ReadAllText(configPath));
        }

        if (string.IsNullOrEmpty(apiKey))
            apiKey = fileConfig?["apiKey"]?.Value<string>() ?? "";
        if (string.IsNullOrEmpty(baseUrl))
            baseUrl = fileConfig?["baseUrl"]?.Value<string>() ?? "https://apibox.erweima.ai";

        var downloadDir = fileConfig?["downloadDir"]?.Value<string>()
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".served", "suno", "songs");

        return new SunoConfig
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl,
            DownloadDir = downloadDir
        };
    }

    public static void SaveConfig(SunoConfig config)
    {
        var configPath = GetConfigPath();
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

        var json = new JObject
        {
            ["apiKey"] = config.ApiKey,
            ["baseUrl"] = config.BaseUrl,
            ["downloadDir"] = config.DownloadDir
        };

        File.WriteAllText(configPath, json.ToString(Formatting.Indented));
    }

    private static string GetConfigPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".served", "suno", "config.json");

    // ─── HTTP Helpers ─────────────────────────────────────────────────

    private async Task<JObject> PostAsync(string path, JObject payload)
    {
        var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(path, content);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Suno API {(int)response.StatusCode}: {body}");

        return JObject.Parse(body);
    }

    private async Task<JObject> GetAsync(string path)
    {
        var response = await _http.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Suno API {(int)response.StatusCode}: {body}");

        return JObject.Parse(body);
    }

    private static SunoClip[] ParseClips(JArray? clips)
    {
        if (clips == null) return Array.Empty<SunoClip>();

        return clips.Select(c => new SunoClip
        {
            Id = c["id"]?.Value<string>() ?? "",
            Title = c["title"]?.Value<string>() ?? "",
            AudioUrl = c["audioUrl"]?.Value<string>() ?? c["audio_url"]?.Value<string>() ?? "",
            ImageUrl = c["imageUrl"]?.Value<string>() ?? c["image_url"]?.Value<string>() ?? "",
            Duration = c["duration"]?.Value<double>() ?? 0,
            Status = c["status"]?.Value<string>() ?? "unknown",
            Style = c["tags"]?.Value<string>() ?? c["style"]?.Value<string>() ?? "",
            Lyrics = c["lyrics"]?.Value<string>() ?? "",
            Model = c["model"]?.Value<string>() ?? "",
            CreatedAt = c["createdAt"]?.Value<DateTime>() ?? DateTime.UtcNow
        }).ToArray();
    }

    public void Dispose() => _http.Dispose();

    // ─── Models ───────────────────────────────────────────────────────

    public class SunoConfig
    {
        public string ApiKey { get; set; } = "";
        public string BaseUrl { get; set; } = "https://apibox.erweima.ai";
        public string DownloadDir { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".served", "suno", "songs");
    }

    public class SunoClip
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string AudioUrl { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public double Duration { get; set; }
        public string Status { get; set; } = "";
        public string Style { get; set; } = "";
        public string Lyrics { get; set; } = "";
        public string Model { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class SunoGenerateResult
    {
        public string TaskId { get; set; } = "";
        public SunoClip[] Clips { get; set; } = Array.Empty<SunoClip>();
    }

    public class SunoTaskStatus
    {
        public string Status { get; set; } = "";
        public SunoClip[] Clips { get; set; } = Array.Empty<SunoClip>();
    }

    public class SunoLyricsResult
    {
        public string Text { get; set; } = "";
        public string Title { get; set; } = "";
    }

    public class SunoCreditsResult
    {
        public int Remaining { get; set; }
        public int Total { get; set; }
        public string Plan { get; set; } = "";
    }

    public class SunoHistoryEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string TaskId { get; set; } = "";
        public string[] ClipIds { get; set; } = Array.Empty<string>();
        public string Prompt { get; set; } = "";
        public string? Style { get; set; }
        public string? Title { get; set; }
        public string[] FilePaths { get; set; } = Array.Empty<string>();
        public string Model { get; set; } = "";
    }
}
