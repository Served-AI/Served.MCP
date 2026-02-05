using System.Text;
using Newtonsoft.Json.Linq;
using Served.SDK.Client;

namespace Served.MCP.Tools;

/// <summary>
/// BRICK: Roof - MCP tools for media analytics
/// Provides AI assistants with media download, analysis, and lyrics capabilities
/// </summary>
public static class MediaTools
{
    /// <summary>
    /// Register all media analytics tools.
    /// </summary>
    public static void Register(McpServer server, ServedClient client)
    {
        // ─────────────────────────────────────────────────────────────
        // Tool 1: media_download
        // ─────────────────────────────────────────────────────────────
        server.RegisterTool("media_download",
            "Download video/audio from YouTube or other supported sites. Use presets: teaching (360p), deep-analysis (4K), music-video (1080p+FLAC), podcast (audio only), quick-preview (480p).",
            async (args) =>
            {
                var url = args["url"]?.Value<string>() ?? throw new ArgumentException("url required");
                var preset = args["preset"]?.Value<string>() ?? "quick-preview";
                var analyze = args["analyze"]?.Value<bool>() ?? false;
                var extractLyrics = args["extract_lyrics"]?.Value<bool>() ?? false;

                var payload = new JObject
                {
                    ["url"] = url,
                    ["preset"] = preset,
                    ["analyze"] = analyze,
                    ["extractLyrics"] = extractLyrics
                };

                var response = await server.Http.PostAsync("/api/media/download",
                    new StringContent(payload.ToString(), System.Text.Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return $"@mediaDownload {{ success: false, error: \"{error}\" }}";
                }

                var result = JObject.Parse(await response.Content.ReadAsStringAsync());

                var sb = new StringBuilder();
                sb.AppendLine("@mediaDownload {");
                sb.AppendLine($"  success: {result["success"]?.ToString().ToLower() ?? "false"}");
                if (result["filePath"] != null)
                    sb.AppendLine($"  filePath: \"{result["filePath"]}\"");
                if (result["audioPath"] != null)
                    sb.AppendLine($"  audioPath: \"{result["audioPath"]}\"");
                if (result["title"] != null)
                    sb.AppendLine($"  title: \"{result["title"]}\"");
                if (result["durationSeconds"] != null)
                    sb.AppendLine($"  durationSeconds: {result["durationSeconds"]}");
                if (result["error"] != null)
                    sb.AppendLine($"  error: \"{result["error"]}\"");
                sb.AppendLine("}");

                return sb.ToString();
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["url"] = new JObject { ["type"] = "string", ["description"] = "URL to download from (YouTube, etc.)" },
                    ["preset"] = new JObject { ["type"] = "string", ["description"] = "Quality preset: teaching, deep-analysis, music-video, podcast, quick-preview", ["default"] = "quick-preview" },
                    ["analyze"] = new JObject { ["type"] = "boolean", ["description"] = "Analyze after download", ["default"] = false },
                    ["extract_lyrics"] = new JObject { ["type"] = "boolean", ["description"] = "Extract lyrics after download", ["default"] = false }
                },
                ["required"] = new JArray { "url" }
            });

        // ─────────────────────────────────────────────────────────────
        // Tool 2: media_analyze_audio
        // ─────────────────────────────────────────────────────────────
        server.RegisterTool("media_analyze_audio",
            "Analyze audio file for BPM, key, sections, mood. Uses librosa for analysis and optionally Ollama for mood detection.",
            async (args) =>
            {
                var filePath = args["file_path"]?.Value<string>() ?? throw new ArgumentException("file_path required");
                var detectBpm = args["detect_bpm"]?.Value<bool>() ?? true;
                var detectKey = args["detect_key"]?.Value<bool>() ?? true;
                var detectSections = args["detect_sections"]?.Value<bool>() ?? true;
                var useOllamaMood = args["use_ollama_mood"]?.Value<bool>() ?? true;

                var payload = new JObject
                {
                    ["filePath"] = filePath,
                    ["detectBpm"] = detectBpm,
                    ["detectKey"] = detectKey,
                    ["detectSections"] = detectSections,
                    ["useOllamaMood"] = useOllamaMood
                };

                var response = await server.Http.PostAsync("/api/media/analyze/audio",
                    new StringContent(payload.ToString(), System.Text.Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return $"@audioAnalysis {{ success: false, error: \"{error}\" }}";
                }

                var result = JObject.Parse(await response.Content.ReadAsStringAsync());

                var sb = new StringBuilder();
                sb.AppendLine("@audioAnalysis {");
                sb.AppendLine($"  success: {result["success"]?.ToString().ToLower() ?? "false"}");
                if (result["bpm"] != null)
                    sb.AppendLine($"  bpm: {result["bpm"]}");
                if (result["key"] != null)
                    sb.AppendLine($"  key: \"{result["key"]}\"");
                if (result["mode"] != null)
                    sb.AppendLine($"  mode: \"{result["mode"]}\"");
                if (result["durationSeconds"] != null)
                    sb.AppendLine($"  durationSeconds: {result["durationSeconds"]}");
                if (result["mood"] != null)
                    sb.AppendLine($"  mood: \"{result["mood"]}\"");

                var sections = result["sections"] as JArray;
                if (sections != null && sections.Count > 0)
                {
                    sb.AppendLine($"  sections: [{sections.Count}] {{");
                    foreach (var section in sections)
                    {
                        sb.AppendLine($"    {{ label: \"{section["label"]}\", start: {section["startSeconds"]}s, end: {section["endSeconds"]}s, energy: {section["energy"]} }}");
                    }
                    sb.AppendLine("  }");
                }

                if (result["error"] != null)
                    sb.AppendLine($"  error: \"{result["error"]}\"");
                sb.AppendLine("}");

                return sb.ToString();
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["file_path"] = new JObject { ["type"] = "string", ["description"] = "Path to audio file" },
                    ["detect_bpm"] = new JObject { ["type"] = "boolean", ["description"] = "Detect BPM", ["default"] = true },
                    ["detect_key"] = new JObject { ["type"] = "boolean", ["description"] = "Detect musical key", ["default"] = true },
                    ["detect_sections"] = new JObject { ["type"] = "boolean", ["description"] = "Detect sections (intro, verse, chorus)", ["default"] = true },
                    ["use_ollama_mood"] = new JObject { ["type"] = "boolean", ["description"] = "Use Ollama for mood detection", ["default"] = true }
                },
                ["required"] = new JArray { "file_path" }
            });

        // ─────────────────────────────────────────────────────────────
        // Tool 3: media_analyze_video
        // ─────────────────────────────────────────────────────────────
        server.RegisterTool("media_analyze_video",
            "Analyze video for scenes, faces, text (OCR), teaching content detection (slides, code blocks).",
            async (args) =>
            {
                var filePath = args["file_path"]?.Value<string>() ?? throw new ArgumentException("file_path required");
                var detectScenes = args["detect_scenes"]?.Value<bool>() ?? true;
                var detectFaces = args["detect_faces"]?.Value<bool>() ?? true;
                var extractText = args["extract_text"]?.Value<bool>() ?? true;
                var detectTeaching = args["detect_teaching"]?.Value<bool>() ?? true;
                var generateThumbnails = args["generate_thumbnails"]?.Value<bool>() ?? true;

                var payload = new JObject
                {
                    ["filePath"] = filePath,
                    ["detectScenes"] = detectScenes,
                    ["detectFaces"] = detectFaces,
                    ["extractText"] = extractText,
                    ["detectTeaching"] = detectTeaching,
                    ["generateThumbnails"] = generateThumbnails
                };

                var response = await server.Http.PostAsync("/api/media/analyze/video",
                    new StringContent(payload.ToString(), System.Text.Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return $"@videoAnalysis {{ success: false, error: \"{error}\" }}";
                }

                var result = JObject.Parse(await response.Content.ReadAsStringAsync());

                var sb = new StringBuilder();
                sb.AppendLine("@videoAnalysis {");
                sb.AppendLine($"  success: {result["success"]?.ToString().ToLower() ?? "false"}");
                if (result["durationSeconds"] != null)
                    sb.AppendLine($"  durationSeconds: {result["durationSeconds"]}");
                if (result["resolution"] != null)
                    sb.AppendLine($"  resolution: \"{result["resolution"]}\"");
                if (result["fps"] != null)
                    sb.AppendLine($"  fps: {result["fps"]}");
                if (result["sceneCount"] != null)
                    sb.AppendLine($"  sceneCount: {result["sceneCount"]}");
                if (result["faceCount"] != null)
                    sb.AppendLine($"  faceCount: {result["faceCount"]}");
                if (result["slidesDetected"] != null)
                    sb.AppendLine($"  slidesDetected: {result["slidesDetected"]}");
                if (result["codeBlocksDetected"] != null)
                    sb.AppendLine($"  codeBlocksDetected: {result["codeBlocksDetected"]}");

                var texts = result["detectedTexts"] as JArray;
                if (texts != null && texts.Count > 0)
                {
                    sb.AppendLine($"  detectedTexts: [{texts.Count}]");
                }

                if (result["error"] != null)
                    sb.AppendLine($"  error: \"{result["error"]}\"");
                sb.AppendLine("}");

                return sb.ToString();
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["file_path"] = new JObject { ["type"] = "string", ["description"] = "Path to video file" },
                    ["detect_scenes"] = new JObject { ["type"] = "boolean", ["description"] = "Detect scene changes", ["default"] = true },
                    ["detect_faces"] = new JObject { ["type"] = "boolean", ["description"] = "Detect faces", ["default"] = true },
                    ["extract_text"] = new JObject { ["type"] = "boolean", ["description"] = "Extract text via OCR", ["default"] = true },
                    ["detect_teaching"] = new JObject { ["type"] = "boolean", ["description"] = "Detect teaching content (slides, code)", ["default"] = true },
                    ["generate_thumbnails"] = new JObject { ["type"] = "boolean", ["description"] = "Generate thumbnails", ["default"] = true }
                },
                ["required"] = new JArray { "file_path" }
            });

        // ─────────────────────────────────────────────────────────────
        // Tool 4: media_analyze_full
        // ─────────────────────────────────────────────────────────────
        server.RegisterTool("media_analyze_full",
            "Full media analysis: audio features + video analysis + AI insights (mood, genre, themes via Ollama).",
            async (args) =>
            {
                var filePath = args["file_path"]?.Value<string>() ?? throw new ArgumentException("file_path required");
                var includeAiInsights = args["include_ai_insights"]?.Value<bool>() ?? true;
                var ollamaModel = args["ollama_model"]?.Value<string>() ?? "llama3.2";

                var payload = new JObject
                {
                    ["filePath"] = filePath,
                    ["includeAiInsights"] = includeAiInsights,
                    ["ollamaModel"] = ollamaModel
                };

                var response = await server.Http.PostAsync("/api/media/analyze/full",
                    new StringContent(payload.ToString(), System.Text.Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return $"@fullAnalysis {{ success: false, error: \"{error}\" }}";
                }

                var result = JObject.Parse(await response.Content.ReadAsStringAsync());

                var sb = new StringBuilder();
                sb.AppendLine("@fullAnalysis {");
                sb.AppendLine($"  success: {result["success"]?.ToString().ToLower() ?? "false"}");

                var audio = result["audio"];
                if (audio != null)
                {
                    sb.AppendLine("  audio: {");
                    if (audio["bpm"] != null) sb.AppendLine($"    bpm: {audio["bpm"]}");
                    if (audio["key"] != null) sb.AppendLine($"    key: \"{audio["key"]}\"");
                    if (audio["mode"] != null) sb.AppendLine($"    mode: \"{audio["mode"]}\"");
                    sb.AppendLine("  }");
                }

                var video = result["video"];
                if (video != null)
                {
                    sb.AppendLine("  video: {");
                    if (video["resolution"] != null) sb.AppendLine($"    resolution: \"{video["resolution"]}\"");
                    if (video["sceneCount"] != null) sb.AppendLine($"    sceneCount: {video["sceneCount"]}");
                    if (video["faceCount"] != null) sb.AppendLine($"    faceCount: {video["faceCount"]}");
                    sb.AppendLine("  }");
                }

                var ai = result["aiInsights"];
                if (ai != null)
                {
                    sb.AppendLine("  aiInsights: {");
                    if (ai["mood"] != null) sb.AppendLine($"    mood: \"{ai["mood"]}\"");
                    if (ai["genre"] != null) sb.AppendLine($"    genre: \"{ai["genre"]}\"");
                    var themes = ai["themes"] as JArray;
                    if (themes != null && themes.Count > 0)
                        sb.AppendLine($"    themes: [{string.Join(", ", themes.Select(t => $"\"{t}\""))}]");
                    if (ai["summary"] != null) sb.AppendLine($"    summary: \"{ai["summary"]}\"");
                    sb.AppendLine("  }");
                }

                if (result["error"] != null)
                    sb.AppendLine($"  error: \"{result["error"]}\"");
                sb.AppendLine("}");

                return sb.ToString();
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["file_path"] = new JObject { ["type"] = "string", ["description"] = "Path to media file" },
                    ["include_ai_insights"] = new JObject { ["type"] = "boolean", ["description"] = "Include AI insights from Ollama", ["default"] = true },
                    ["ollama_model"] = new JObject { ["type"] = "string", ["description"] = "Ollama model to use", ["default"] = "llama3.2" }
                },
                ["required"] = new JArray { "file_path" }
            });

        // ─────────────────────────────────────────────────────────────
        // Tool 5: lyrics_fetch
        // ─────────────────────────────────────────────────────────────
        server.RegisterTool("lyrics_fetch",
            "Fetch lyrics using multi-source pipeline: Cache → Genius → Whisper transcription → Ollama generation. Falls back automatically.",
            async (args) =>
            {
                var artist = args["artist"]?.Value<string>();
                var title = args["title"]?.Value<string>();
                var audioFile = args["audio_file"]?.Value<string>();
                var mode = args["mode"]?.Value<string>() ?? "auto";
                var useCache = args["use_cache"]?.Value<bool>() ?? true;
                var fallbackGenerate = args["fallback_generate"]?.Value<bool>() ?? true;

                if (string.IsNullOrEmpty(artist) && string.IsNullOrEmpty(title) && string.IsNullOrEmpty(audioFile))
                    throw new ArgumentException("Either artist+title or audio_file required");

                var payload = new JObject
                {
                    ["artist"] = artist,
                    ["title"] = title,
                    ["audioFilePath"] = audioFile,
                    ["mode"] = mode,
                    ["useCache"] = useCache,
                    ["fallbackToGeneration"] = fallbackGenerate
                };

                var response = await server.Http.PostAsync("/api/lyrics/fetch",
                    new StringContent(payload.ToString(), System.Text.Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return $"@lyrics {{ success: false, error: \"{error}\" }}";
                }

                var result = JObject.Parse(await response.Content.ReadAsStringAsync());

                var sb = new StringBuilder();
                sb.AppendLine("@lyrics {");
                sb.AppendLine($"  success: {result["success"]?.ToString().ToLower() ?? "false"}");
                if (result["source"] != null)
                    sb.AppendLine($"  source: \"{result["source"]}\"");
                if (result["artist"] != null)
                    sb.AppendLine($"  artist: \"{result["artist"]}\"");
                if (result["title"] != null)
                    sb.AppendLine($"  title: \"{result["title"]}\"");
                if (result["confidence"] != null)
                    sb.AppendLine($"  confidence: {result["confidence"]}");

                var lyrics = result["lyrics"]?.Value<string>();
                if (!string.IsNullOrEmpty(lyrics))
                {
                    // Truncate for display but indicate full length
                    var lines = lyrics.Split('\n');
                    sb.AppendLine($"  lyrics: [{lines.Length} lines]");
                    sb.AppendLine("  ---");
                    sb.AppendLine(lyrics.Length > 2000 ? lyrics[..2000] + "\n[truncated...]" : lyrics);
                    sb.AppendLine("  ---");
                }

                if (result["error"] != null)
                    sb.AppendLine($"  error: \"{result["error"]}\"");
                sb.AppendLine("}");

                return sb.ToString();
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["artist"] = new JObject { ["type"] = "string", ["description"] = "Artist name" },
                    ["title"] = new JObject { ["type"] = "string", ["description"] = "Song title" },
                    ["audio_file"] = new JObject { ["type"] = "string", ["description"] = "Audio file path for transcription fallback" },
                    ["mode"] = new JObject { ["type"] = "string", ["description"] = "Mode: auto, genius, whisper, ollama_generate, cache_only", ["default"] = "auto" },
                    ["use_cache"] = new JObject { ["type"] = "boolean", ["description"] = "Use cache", ["default"] = true },
                    ["fallback_generate"] = new JObject { ["type"] = "boolean", ["description"] = "Fall back to Ollama generation if not found", ["default"] = true }
                }
            });

        // ─────────────────────────────────────────────────────────────
        // Tool 6: lyrics_analyze
        // ─────────────────────────────────────────────────────────────
        server.RegisterTool("lyrics_analyze",
            "Analyze lyrics for themes, emotions, key lines, and optionally translate. Uses Ollama for deep analysis.",
            async (args) =>
            {
                var lyrics = args["lyrics"]?.Value<string>() ?? throw new ArgumentException("lyrics required");
                var extractThemes = args["extract_themes"]?.Value<bool>() ?? true;
                var detectEmotions = args["detect_emotions"]?.Value<bool>() ?? true;
                var extractKeyLines = args["extract_key_lines"]?.Value<bool>() ?? true;
                var generateSummary = args["generate_summary"]?.Value<bool>() ?? true;
                var translate = args["translate"]?.Value<bool>() ?? false;
                var targetLanguage = args["target_language"]?.Value<string>();
                var model = args["model"]?.Value<string>() ?? "llama3.2";

                var payload = new JObject
                {
                    ["lyrics"] = lyrics,
                    ["extractThemes"] = extractThemes,
                    ["detectEmotions"] = detectEmotions,
                    ["extractKeyLines"] = extractKeyLines,
                    ["generateSummary"] = generateSummary,
                    ["translate"] = translate,
                    ["targetLanguage"] = targetLanguage,
                    ["model"] = model
                };

                var response = await server.Http.PostAsync("/api/lyrics/analyze",
                    new StringContent(payload.ToString(), System.Text.Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return $"@lyricsAnalysis {{ success: false, error: \"{error}\" }}";
                }

                var result = JObject.Parse(await response.Content.ReadAsStringAsync());

                var sb = new StringBuilder();
                sb.AppendLine("@lyricsAnalysis {");
                sb.AppendLine($"  success: {result["success"]?.ToString().ToLower() ?? "false"}");

                var themes = result["themes"] as JArray;
                if (themes != null && themes.Count > 0)
                {
                    sb.AppendLine($"  themes: [{themes.Count}] {{");
                    foreach (var theme in themes)
                    {
                        sb.AppendLine($"    {{ name: \"{theme["name"]}\", intensity: {theme["intensity"]} }}");
                    }
                    sb.AppendLine("  }");
                }

                if (result["primaryEmotion"] != null)
                    sb.AppendLine($"  primaryEmotion: \"{result["primaryEmotion"]}\"");

                var emotions = result["secondaryEmotions"] as JArray;
                if (emotions != null && emotions.Count > 0)
                    sb.AppendLine($"  secondaryEmotions: [{string.Join(", ", emotions.Select(e => $"\"{e}\""))}]");

                if (result["emotionalArc"] != null)
                    sb.AppendLine($"  emotionalArc: \"{result["emotionalArc"]}\"");

                var keyLines = result["keyLines"] as JArray;
                if (keyLines != null && keyLines.Count > 0)
                {
                    sb.AppendLine($"  keyLines: [{keyLines.Count}]");
                    foreach (var line in keyLines.Take(5))
                    {
                        sb.AppendLine($"    - \"{line}\"");
                    }
                }

                if (result["detectedLanguage"] != null)
                    sb.AppendLine($"  detectedLanguage: \"{result["detectedLanguage"]}\"");

                if (result["summary"] != null)
                    sb.AppendLine($"  summary: \"{result["summary"]}\"");

                if (result["translation"] != null)
                    sb.AppendLine($"  translation: \"{result["translation"]}\"");

                if (result["error"] != null)
                    sb.AppendLine($"  error: \"{result["error"]}\"");
                sb.AppendLine("}");

                return sb.ToString();
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["lyrics"] = new JObject { ["type"] = "string", ["description"] = "Lyrics text to analyze" },
                    ["extract_themes"] = new JObject { ["type"] = "boolean", ["description"] = "Extract themes", ["default"] = true },
                    ["detect_emotions"] = new JObject { ["type"] = "boolean", ["description"] = "Detect emotions", ["default"] = true },
                    ["extract_key_lines"] = new JObject { ["type"] = "boolean", ["description"] = "Extract key lines", ["default"] = true },
                    ["generate_summary"] = new JObject { ["type"] = "boolean", ["description"] = "Generate summary", ["default"] = true },
                    ["translate"] = new JObject { ["type"] = "boolean", ["description"] = "Translate lyrics", ["default"] = false },
                    ["target_language"] = new JObject { ["type"] = "string", ["description"] = "Target language for translation" },
                    ["model"] = new JObject { ["type"] = "string", ["description"] = "Ollama model", ["default"] = "llama3.2" }
                },
                ["required"] = new JArray { "lyrics" }
            });

        // ─────────────────────────────────────────────────────────────
        // Tool 7: lyrics_generate
        // ─────────────────────────────────────────────────────────────
        server.RegisterTool("lyrics_generate",
            "Generate lyrics using Ollama based on musical features (BPM, key, mood) and style context.",
            async (args) =>
            {
                var artist = args["artist"]?.Value<string>();
                var title = args["title"]?.Value<string>();
                var bpm = args["bpm"]?.Value<double?>();
                var key = args["key"]?.Value<string>();
                var mood = args["mood"]?.Value<string>();
                var genre = args["genre"]?.Value<string>();
                var context = args["context"]?.Value<string>();
                var model = args["model"]?.Value<string>() ?? "llama3.2";

                var payload = new JObject
                {
                    ["artist"] = artist,
                    ["title"] = title,
                    ["bpm"] = bpm,
                    ["key"] = key,
                    ["mood"] = mood,
                    ["genre"] = genre,
                    ["additionalContext"] = context,
                    ["model"] = model
                };

                var response = await server.Http.PostAsync("/api/lyrics/generate",
                    new StringContent(payload.ToString(), System.Text.Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return $"@generatedLyrics {{ success: false, error: \"{error}\" }}";
                }

                var result = JObject.Parse(await response.Content.ReadAsStringAsync());

                var sb = new StringBuilder();
                sb.AppendLine("@generatedLyrics {");
                sb.AppendLine($"  success: {result["success"]?.ToString().ToLower() ?? "false"}");
                if (result["modelUsed"] != null)
                    sb.AppendLine($"  modelUsed: \"{result["modelUsed"]}\"");
                if (result["confidence"] != null)
                    sb.AppendLine($"  confidence: {result["confidence"]}");

                var lyrics = result["lyrics"]?.Value<string>();
                if (!string.IsNullOrEmpty(lyrics))
                {
                    sb.AppendLine("  lyrics:");
                    sb.AppendLine("  ---");
                    sb.AppendLine(lyrics);
                    sb.AppendLine("  ---");
                }

                if (result["error"] != null)
                    sb.AppendLine($"  error: \"{result["error"]}\"");
                sb.AppendLine("}");

                return sb.ToString();
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["artist"] = new JObject { ["type"] = "string", ["description"] = "Artist style to emulate" },
                    ["title"] = new JObject { ["type"] = "string", ["description"] = "Song title/theme" },
                    ["bpm"] = new JObject { ["type"] = "number", ["description"] = "BPM for rhythm matching" },
                    ["key"] = new JObject { ["type"] = "string", ["description"] = "Musical key" },
                    ["mood"] = new JObject { ["type"] = "string", ["description"] = "Mood (happy, sad, energetic, etc.)" },
                    ["genre"] = new JObject { ["type"] = "string", ["description"] = "Genre" },
                    ["context"] = new JObject { ["type"] = "string", ["description"] = "Additional context/instructions" },
                    ["model"] = new JObject { ["type"] = "string", ["description"] = "Ollama model", ["default"] = "llama3.2" }
                }
            });

        // ─────────────────────────────────────────────────────────────
        // Tool 8: media_get_metadata
        // ─────────────────────────────────────────────────────────────
        server.RegisterTool("media_get_metadata",
            "Get metadata for a video URL without downloading (title, duration, thumbnail, chapters, etc.).",
            async (args) =>
            {
                var url = args["url"]?.Value<string>() ?? throw new ArgumentException("url required");

                var response = await server.Http.GetAsync($"/api/media/metadata?url={Uri.EscapeDataString(url)}");

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return $"@mediaMetadata {{ success: false, error: \"{error}\" }}";
                }

                var result = JObject.Parse(await response.Content.ReadAsStringAsync());

                var sb = new StringBuilder();
                sb.AppendLine("@mediaMetadata {");
                sb.AppendLine($"  success: true");
                if (result["title"] != null)
                    sb.AppendLine($"  title: \"{result["title"]}\"");
                if (result["artist"] != null)
                    sb.AppendLine($"  artist: \"{result["artist"]}\"");
                if (result["channel"] != null)
                    sb.AppendLine($"  channel: \"{result["channel"]}\"");
                if (result["durationSeconds"] != null)
                    sb.AppendLine($"  durationSeconds: {result["durationSeconds"]}");
                if (result["thumbnailUrl"] != null)
                    sb.AppendLine($"  thumbnailUrl: \"{result["thumbnailUrl"]}\"");
                if (result["viewCount"] != null)
                    sb.AppendLine($"  viewCount: {result["viewCount"]}");
                if (result["uploadDate"] != null)
                    sb.AppendLine($"  uploadDate: \"{result["uploadDate"]}\"");

                var tags = result["tags"] as JArray;
                if (tags != null && tags.Count > 0)
                    sb.AppendLine($"  tags: [{string.Join(", ", tags.Take(10).Select(t => $"\"{t}\""))}]");

                var chapters = result["chapters"] as JArray;
                if (chapters != null && chapters.Count > 0)
                {
                    sb.AppendLine($"  chapters: [{chapters.Count}] {{");
                    foreach (var chapter in chapters)
                    {
                        sb.AppendLine($"    {{ title: \"{chapter["title"]}\", start: {chapter["startSeconds"]}s }}");
                    }
                    sb.AppendLine("  }");
                }

                sb.AppendLine("}");

                return sb.ToString();
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["url"] = new JObject { ["type"] = "string", ["description"] = "URL to get metadata for" }
                },
                ["required"] = new JArray { "url" }
            });
    }
}
