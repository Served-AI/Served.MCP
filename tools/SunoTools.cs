using System.Text;
using Newtonsoft.Json.Linq;
using Served.MCP.Tools.Services;
using Served.SDK.Client;

namespace Served.MCP.Tools;

/// <summary>
/// BRICK: Roof - MCP tools for Suno AI music generation
/// Provides AI assistants with song generation, lyrics, and credit management
/// </summary>
public static class SunoTools
{
    /// <summary>
    /// Register all Suno music generation tools.
    /// </summary>
    public static void Register(McpServer server, ServedClient client)
    {
        // ─────────────────────────────────────────────────────────────
        // Tool 1: suno_generate
        // ─────────────────────────────────────────────────────────────
        server.RegisterTool("suno_generate",
            "Generate a song using Suno AI. Provide a text prompt describing the song, optionally with style tags, title, and custom lyrics. By default waits for completion (up to 2 min) and returns audio URLs.",
            async (args) =>
            {
                var prompt = args["prompt"]?.Value<string>() ?? throw new ArgumentException("prompt required");
                var style = args["style"]?.Value<string>();
                var title = args["title"]?.Value<string>();
                var lyrics = args["lyrics"]?.Value<string>();
                var instrumental = args["instrumental"]?.Value<bool>() ?? false;
                var model = args["model"]?.Value<string>() ?? "V3_5";
                var wait = args["wait"]?.Value<bool>() ?? true;

                try
                {
                    using var suno = new SunoService();
                    var result = await suno.GenerateAsync(prompt, style, title, lyrics, instrumental, model);

                    var sb = new StringBuilder();

                    if (wait)
                    {
                        var clips = await suno.PollUntilComplete(result.TaskId);

                        sb.AppendLine("@sunoGenerate {");
                        sb.AppendLine($"  success: true");
                        sb.AppendLine($"  taskId: \"{result.TaskId}\"");
                        sb.AppendLine($"  status: \"complete\"");
                        sb.AppendLine($"  clips: [{clips.Length}] {{");
                        foreach (var clip in clips)
                        {
                            sb.AppendLine($"    {{");
                            sb.AppendLine($"      id: \"{clip.Id}\"");
                            sb.AppendLine($"      title: \"{clip.Title}\"");
                            sb.AppendLine($"      audioUrl: \"{clip.AudioUrl}\"");
                            if (!string.IsNullOrEmpty(clip.ImageUrl))
                                sb.AppendLine($"      imageUrl: \"{clip.ImageUrl}\"");
                            sb.AppendLine($"      duration: {clip.Duration}s");
                            sb.AppendLine($"      status: \"{clip.Status}\"");
                            sb.AppendLine($"    }}");
                        }
                        sb.AppendLine("  }");
                        sb.AppendLine("}");

                        // Record to history
                        SunoService.RecordHistory(new SunoService.SunoHistoryEntry
                        {
                            TaskId = result.TaskId,
                            ClipIds = clips.Select(c => c.Id).ToArray(),
                            Prompt = prompt,
                            Style = style,
                            Title = title ?? clips.FirstOrDefault()?.Title ?? "",
                            Model = model
                        });
                    }
                    else
                    {
                        sb.AppendLine("@sunoGenerate {");
                        sb.AppendLine($"  success: true");
                        sb.AppendLine($"  taskId: \"{result.TaskId}\"");
                        sb.AppendLine($"  status: \"submitted\"");
                        sb.AppendLine($"  message: \"Generation started. Use suno_status with taskId to check progress.\"");
                        sb.AppendLine("}");
                    }

                    return sb.ToString();
                }
                catch (Exception ex)
                {
                    return $"@sunoGenerate {{ success: false, error: \"{ex.Message.Replace("\"", "'")}\" }}";
                }
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["prompt"] = new JObject { ["type"] = "string", ["description"] = "Text prompt describing the song to generate" },
                    ["style"] = new JObject { ["type"] = "string", ["description"] = "Style/genre tags (e.g. 'electronic, upbeat, energetic')" },
                    ["title"] = new JObject { ["type"] = "string", ["description"] = "Song title" },
                    ["lyrics"] = new JObject { ["type"] = "string", ["description"] = "Custom lyrics (use with style for custom mode)" },
                    ["instrumental"] = new JObject { ["type"] = "boolean", ["description"] = "Generate instrumental only (no vocals)", ["default"] = false },
                    ["model"] = new JObject { ["type"] = "string", ["description"] = "Suno model version: V3_5, V4", ["default"] = "V3_5" },
                    ["wait"] = new JObject { ["type"] = "boolean", ["description"] = "Wait for completion (true) or return task ID immediately (false)", ["default"] = true }
                },
                ["required"] = new JArray { "prompt" }
            });

        // ─────────────────────────────────────────────────────────────
        // Tool 2: suno_generate_lyrics
        // ─────────────────────────────────────────────────────────────
        server.RegisterTool("suno_generate_lyrics",
            "Generate lyrics from a theme or description using Suno AI. Returns suggested lyrics and title.",
            async (args) =>
            {
                var prompt = args["prompt"]?.Value<string>() ?? throw new ArgumentException("prompt required");

                try
                {
                    using var suno = new SunoService();
                    var result = await suno.GenerateLyricsAsync(prompt);

                    var sb = new StringBuilder();
                    sb.AppendLine("@sunoLyrics {");
                    sb.AppendLine($"  success: true");
                    if (!string.IsNullOrEmpty(result.Title))
                        sb.AppendLine($"  title: \"{result.Title}\"");
                    sb.AppendLine("  lyrics:");
                    sb.AppendLine("  ---");
                    sb.AppendLine(result.Text);
                    sb.AppendLine("  ---");
                    sb.AppendLine("}");

                    return sb.ToString();
                }
                catch (Exception ex)
                {
                    return $"@sunoLyrics {{ success: false, error: \"{ex.Message.Replace("\"", "'")}\" }}";
                }
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["prompt"] = new JObject { ["type"] = "string", ["description"] = "Theme or description to generate lyrics from" }
                },
                ["required"] = new JArray { "prompt" }
            });

        // ─────────────────────────────────────────────────────────────
        // Tool 3: suno_status
        // ─────────────────────────────────────────────────────────────
        server.RegisterTool("suno_status",
            "Check the status of a Suno generation task. Returns current status and clip details when complete.",
            async (args) =>
            {
                var taskId = args["task_id"]?.Value<string>() ?? throw new ArgumentException("task_id required");

                try
                {
                    using var suno = new SunoService();
                    var status = await suno.GetStatusAsync(taskId);

                    var sb = new StringBuilder();
                    sb.AppendLine("@sunoStatus {");
                    sb.AppendLine($"  taskId: \"{taskId}\"");
                    sb.AppendLine($"  status: \"{status.Status}\"");

                    if (status.Clips.Length > 0)
                    {
                        sb.AppendLine($"  clips: [{status.Clips.Length}] {{");
                        foreach (var clip in status.Clips)
                        {
                            sb.AppendLine($"    {{");
                            sb.AppendLine($"      id: \"{clip.Id}\"");
                            sb.AppendLine($"      title: \"{clip.Title}\"");
                            sb.AppendLine($"      status: \"{clip.Status}\"");
                            if (!string.IsNullOrEmpty(clip.AudioUrl))
                                sb.AppendLine($"      audioUrl: \"{clip.AudioUrl}\"");
                            if (clip.Duration > 0)
                                sb.AppendLine($"      duration: {clip.Duration}s");
                            sb.AppendLine($"    }}");
                        }
                        sb.AppendLine("  }");
                    }

                    sb.AppendLine("}");
                    return sb.ToString();
                }
                catch (Exception ex)
                {
                    return $"@sunoStatus {{ success: false, error: \"{ex.Message.Replace("\"", "'")}\" }}";
                }
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["task_id"] = new JObject { ["type"] = "string", ["description"] = "Task ID from suno_generate" }
                },
                ["required"] = new JArray { "task_id" }
            });

        // ─────────────────────────────────────────────────────────────
        // Tool 4: suno_download
        // ─────────────────────────────────────────────────────────────
        server.RegisterTool("suno_download",
            "Download a Suno-generated audio clip to disk. Saves as MP3 to ~/.served/suno/songs/ by default.",
            async (args) =>
            {
                var clipId = args["clip_id"]?.Value<string>() ?? throw new ArgumentException("clip_id required");
                var outputDir = args["output_dir"]?.Value<string>();

                try
                {
                    using var suno = new SunoService();

                    // First get the clip info to find audio URL
                    // Try to find from recent history or status
                    string? audioUrl = args["audio_url"]?.Value<string>();
                    string? clipTitle = null;

                    if (string.IsNullOrEmpty(audioUrl))
                    {
                        // Search history for clip ID
                        var history = SunoService.LoadHistory();
                        foreach (var entry in history.AsEnumerable().Reverse())
                        {
                            if (entry.ClipIds.Contains(clipId))
                            {
                                // Re-fetch status to get URL
                                var status = await suno.GetStatusAsync(entry.TaskId);
                                var clip = status.Clips.FirstOrDefault(c => c.Id == clipId);
                                if (clip != null)
                                {
                                    audioUrl = clip.AudioUrl;
                                    clipTitle = clip.Title;
                                }
                                break;
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(audioUrl))
                        return "@sunoDownload { success: false, error: \"Could not find audio URL for clip. Provide audio_url parameter or ensure clip is in history.\" }";

                    var slug = ToSlug(clipTitle ?? clipId);
                    var outputPath = !string.IsNullOrEmpty(outputDir)
                        ? Path.Combine(outputDir, $"{slug}.mp3")
                        : null;

                    var filePath = await suno.DownloadAsync(audioUrl, outputPath);

                    var sb = new StringBuilder();
                    sb.AppendLine("@sunoDownload {");
                    sb.AppendLine($"  success: true");
                    sb.AppendLine($"  clipId: \"{clipId}\"");
                    sb.AppendLine($"  filePath: \"{filePath}\"");
                    if (!string.IsNullOrEmpty(clipTitle))
                        sb.AppendLine($"  title: \"{clipTitle}\"");
                    sb.AppendLine("}");

                    return sb.ToString();
                }
                catch (Exception ex)
                {
                    return $"@sunoDownload {{ success: false, error: \"{ex.Message.Replace("\"", "'")}\" }}";
                }
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["clip_id"] = new JObject { ["type"] = "string", ["description"] = "Clip ID to download" },
                    ["audio_url"] = new JObject { ["type"] = "string", ["description"] = "Direct audio URL (optional, resolved from history if omitted)" },
                    ["output_dir"] = new JObject { ["type"] = "string", ["description"] = "Output directory (default: ~/.served/suno/songs/)" }
                },
                ["required"] = new JArray { "clip_id" }
            });

        // ─────────────────────────────────────────────────────────────
        // Tool 5: suno_extend
        // ─────────────────────────────────────────────────────────────
        server.RegisterTool("suno_extend",
            "Extend an existing Suno track. Continues the song from a specified point or from the end.",
            async (args) =>
            {
                var clipId = args["clip_id"]?.Value<string>() ?? throw new ArgumentException("clip_id required");
                var prompt = args["prompt"]?.Value<string>();
                var continueAt = args["continue_at"]?.Value<int?>();

                try
                {
                    using var suno = new SunoService();
                    var result = await suno.ExtendAsync(clipId, prompt, continueAt);
                    var clips = await suno.PollUntilComplete(result.TaskId);

                    var sb = new StringBuilder();
                    sb.AppendLine("@sunoExtend {");
                    sb.AppendLine($"  success: true");
                    sb.AppendLine($"  taskId: \"{result.TaskId}\"");
                    sb.AppendLine($"  originalClipId: \"{clipId}\"");
                    sb.AppendLine($"  clips: [{clips.Length}] {{");
                    foreach (var clip in clips)
                    {
                        sb.AppendLine($"    {{");
                        sb.AppendLine($"      id: \"{clip.Id}\"");
                        sb.AppendLine($"      title: \"{clip.Title}\"");
                        sb.AppendLine($"      audioUrl: \"{clip.AudioUrl}\"");
                        sb.AppendLine($"      duration: {clip.Duration}s");
                        sb.AppendLine($"    }}");
                    }
                    sb.AppendLine("  }");
                    sb.AppendLine("}");

                    return sb.ToString();
                }
                catch (Exception ex)
                {
                    return $"@sunoExtend {{ success: false, error: \"{ex.Message.Replace("\"", "'")}\" }}";
                }
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["clip_id"] = new JObject { ["type"] = "string", ["description"] = "Clip ID to extend" },
                    ["prompt"] = new JObject { ["type"] = "string", ["description"] = "Optional prompt for the extension" },
                    ["continue_at"] = new JObject { ["type"] = "integer", ["description"] = "Timestamp (seconds) to continue from" }
                },
                ["required"] = new JArray { "clip_id" }
            });

        // ─────────────────────────────────────────────────────────────
        // Tool 6: suno_credits
        // ─────────────────────────────────────────────────────────────
        server.RegisterTool("suno_credits",
            "Check remaining Suno AI generation credits. Shows credits remaining, total, and plan info.",
            async (args) =>
            {
                try
                {
                    using var suno = new SunoService();
                    var credits = await suno.GetCreditsAsync();

                    var sb = new StringBuilder();
                    sb.AppendLine("@sunoCredits {");
                    sb.AppendLine($"  remaining: {credits.Remaining}");
                    sb.AppendLine($"  total: {credits.Total}");
                    sb.AppendLine($"  plan: \"{credits.Plan}\"");
                    sb.AppendLine("}");

                    return sb.ToString();
                }
                catch (Exception ex)
                {
                    return $"@sunoCredits {{ success: false, error: \"{ex.Message.Replace("\"", "'")}\" }}";
                }
            });
    }

    private static string ToSlug(string input)
    {
        if (string.IsNullOrEmpty(input)) return "untitled";
        return new string(input.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : (c == ' ' ? '-' : '\0'))
            .Where(c => c != '\0')
            .ToArray())
            .Trim('-');
    }
}
