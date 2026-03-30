using System.Net.Http.Json;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using Served.SDK.Client;

namespace Served.MCP.Tools;

/// <summary>
/// Atlas Computer Control Tools - gives Atlas ability to see and control Eden (Thomas' Mac).
/// Communicates with IntelligenceManager on localhost:6943.
/// </summary>
public static class AtlasControlTools
{
    private static HttpClient? _atlasHttp;

    private static HttpClient GetAtlasHttp()
    {
        return _atlasHttp ??= new HttpClient
        {
            BaseAddress = new Uri("http://localhost:6943"),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Register all Atlas Control tools.
    /// </summary>
    public static void Register(McpServer server, ServedClient client)
    {
        var http = GetAtlasHttp();

        // Health check
        server.RegisterTool("AtlasControlHealth",
            "Check if Atlas Control (IntelligenceManager) is running on Eden.",
            async (args) =>
            {
                try
                {
                    var response = await http.GetStringAsync("/atlas/health");
                    return response;
                }
                catch (Exception ex)
                {
                    return new { success = false, error = $"Atlas Control not available: {ex.Message}", hint = "Ensure IntelligenceManager is running on Eden" };
                }
            });

        // Window management
        server.RegisterTool("AtlasListWindows",
            "List all visible windows on Eden.",
            async (args) =>
            {
                try
                {
                    var response = await http.GetStringAsync("/atlas/windows");
                    return response;
                }
                catch (Exception ex)
                {
                    return new { success = false, error = $"Failed to list windows: {ex.Message}" };
                }
            });

        server.RegisterTool("AtlasFocusWindow",
            "Focus a window by windowId or appName.",
            async (args) =>
            {
                var windowId = args["windowId"]?.Value<int?>();
                var appName = args["appName"]?.Value<string>();

                if (windowId == null && string.IsNullOrEmpty(appName))
                    return new { success = false, error = "Provide windowId or appName" };

                try
                {
                    var dto = windowId.HasValue
                        ? new { windowId = windowId.Value }
                        : (object)new { appName = appName };

                    var response = await http.PostAsJsonAsync("/atlas/windows/focus", dto);
                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    return new { success = false, error = $"Failed to focus window: {ex.Message}" };
                }
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["windowId"] = new JObject { ["type"] = "integer", ["description"] = "Window ID to focus" },
                    ["appName"] = new JObject { ["type"] = "string", ["description"] = "Application name to focus" }
                }
            });

        // Screenshot
        server.RegisterTool("AtlasScreenshot",
            "Take a screenshot of a window. Returns base64 data or saves to file when savePath is provided.",
            async (args) =>
            {
                var windowId = args["windowId"]?.Value<int?>();
                var appName = args["appName"]?.Value<string>();
                var immediate = args["immediate"]?.Value<bool>() ?? false;
                var savePath = args["savePath"]?.Value<string>();

                if (windowId == null && string.IsNullOrEmpty(appName))
                    return new { success = false, error = "Provide windowId or appName" };

                try
                {
                    var endpoint = immediate ? "/atlas/screenshot/immediate" : "/atlas/screenshot";
                    var dto = windowId.HasValue
                        ? new { windowId = windowId.Value }
                        : (object)new { appName = appName };

                    var response = await http.PostAsJsonAsync(endpoint, dto);
                    var raw = await response.Content.ReadAsStringAsync();

                    if (!string.IsNullOrEmpty(savePath))
                    {
                        try
                        {
                            var json = JObject.Parse(raw);
                            var base64Data = json["data"]?.Value<string>();
                            if (!string.IsNullOrEmpty(base64Data))
                            {
                                var bytes = Convert.FromBase64String(base64Data);
                                var dir = Path.GetDirectoryName(savePath);
                                if (!string.IsNullOrEmpty(dir))
                                    Directory.CreateDirectory(dir);
                                await File.WriteAllBytesAsync(savePath, bytes);
                                var format = json["format"]?.Value<string>() ?? "jpeg";
                                return new { success = true, path = savePath, size = bytes.Length, format };
                            }
                        }
                        catch (JsonException)
                        {
                            // Response wasn't JSON with data field, save raw
                        }
                    }

                    return raw;
                }
                catch (Exception ex)
                {
                    return new { success = false, error = $"Failed to take screenshot: {ex.Message}" };
                }
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["windowId"] = new JObject { ["type"] = "integer", ["description"] = "Window ID to screenshot" },
                    ["appName"] = new JObject { ["type"] = "string", ["description"] = "Application name to screenshot" },
                    ["immediate"] = new JObject { ["type"] = "boolean", ["description"] = "Skip animation, capture immediately" },
                    ["savePath"] = new JObject { ["type"] = "string", ["description"] = "File path to save screenshot (returns path instead of base64)" }
                }
            });

        // Mouse control
        server.RegisterTool("AtlasMouseMove",
            "Move the mouse cursor to coordinates.",
            async (args) =>
            {
                var x = args["x"]?.Value<double>() ?? throw new ArgumentException("x required");
                var y = args["y"]?.Value<double>() ?? throw new ArgumentException("y required");
                var smooth = args["smooth"]?.Value<bool>() ?? true;

                try
                {
                    var response = await http.PostAsJsonAsync("/atlas/mouse/move", new { x, y, smooth });
                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    return new { success = false, error = $"Failed to move mouse: {ex.Message}" };
                }
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["x"] = new JObject { ["type"] = "number", ["description"] = "X coordinate" },
                    ["y"] = new JObject { ["type"] = "number", ["description"] = "Y coordinate" },
                    ["smooth"] = new JObject { ["type"] = "boolean", ["description"] = "Animate movement (default: true)" }
                },
                ["required"] = new JArray { "x", "y" }
            });

        server.RegisterTool("AtlasMouseClick",
            "Click at coordinates. Type: click, double, right.",
            async (args) =>
            {
                var x = args["x"]?.Value<double>() ?? throw new ArgumentException("x required");
                var y = args["y"]?.Value<double>() ?? throw new ArgumentException("y required");
                var clickType = args["type"]?.Value<string>() ?? "click";

                try
                {
                    var endpoint = clickType.ToLower() switch
                    {
                        "double" or "doubleclick" => "/atlas/mouse/doubleclick",
                        "right" or "rightclick" => "/atlas/mouse/rightclick",
                        _ => "/atlas/mouse/click"
                    };

                    var response = await http.PostAsJsonAsync(endpoint, new { x, y });
                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    return new { success = false, error = $"Failed to click: {ex.Message}" };
                }
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["x"] = new JObject { ["type"] = "number", ["description"] = "X coordinate" },
                    ["y"] = new JObject { ["type"] = "number", ["description"] = "Y coordinate" },
                    ["type"] = new JObject { ["type"] = "string", ["description"] = "Click type: click, double, right" }
                },
                ["required"] = new JArray { "x", "y" }
            });

        // Keyboard control
        server.RegisterTool("AtlasType",
            "Type text using the keyboard.",
            async (args) =>
            {
                var text = args["text"]?.Value<string>() ?? throw new ArgumentException("text required");

                try
                {
                    var response = await http.PostAsJsonAsync("/atlas/keyboard/type", new { text });
                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    return new { success = false, error = $"Failed to type: {ex.Message}" };
                }
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["text"] = new JObject { ["type"] = "string", ["description"] = "Text to type" }
                },
                ["required"] = new JArray { "text" }
            });

        server.RegisterTool("AtlasKeyPress",
            "Press a keyboard key (e.g., Return, Tab, Escape).",
            async (args) =>
            {
                var key = args["key"]?.Value<string>() ?? throw new ArgumentException("key required");

                try
                {
                    var response = await http.PostAsJsonAsync("/atlas/keyboard/key", new { key });
                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    return new { success = false, error = $"Failed to press key: {ex.Message}" };
                }
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["key"] = new JObject { ["type"] = "string", ["description"] = "Key to press (Return, Tab, Escape, etc.)" }
                },
                ["required"] = new JArray { "key" }
            });

        server.RegisterTool("AtlasKeyboardShortcut",
            "Execute a keyboard shortcut (copy, paste, cut, undo, save, selectAll).",
            async (args) =>
            {
                var shortcut = args["shortcut"]?.Value<string>() ?? throw new ArgumentException("shortcut required");

                try
                {
                    var response = await http.PostAsJsonAsync("/atlas/keyboard/shortcut", new { shortcut });
                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    return new { success = false, error = $"Failed to execute shortcut: {ex.Message}" };
                }
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["shortcut"] = new JObject { ["type"] = "string", ["description"] = "Shortcut: copy, paste, cut, undo, save, selectAll" }
                },
                ["required"] = new JArray { "shortcut" }
            });

        // Visual pointer overlay
        server.RegisterTool("AtlasShowPointer",
            "Show a visual pointer overlay at coordinates (for teaching/demo).",
            async (args) =>
            {
                var x = args["x"]?.Value<double>() ?? throw new ArgumentException("x required");
                var y = args["y"]?.Value<double>() ?? throw new ArgumentException("y required");
                var label = args["label"]?.Value<string>();

                try
                {
                    var response = await http.PostAsJsonAsync("/atlas/pointer/show", new { x, y, label });
                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    return new { success = false, error = $"Failed to show pointer: {ex.Message}" };
                }
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["x"] = new JObject { ["type"] = "number", ["description"] = "X coordinate" },
                    ["y"] = new JObject { ["type"] = "number", ["description"] = "Y coordinate" },
                    ["label"] = new JObject { ["type"] = "string", ["description"] = "Optional label text" }
                },
                ["required"] = new JArray { "x", "y" }
            });

        server.RegisterTool("AtlasHidePointer",
            "Hide the visual pointer overlay.",
            async (args) =>
            {
                try
                {
                    var response = await http.PostAsJsonAsync("/atlas/pointer/hide", new { });
                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    return new { success = false, error = $"Failed to hide pointer: {ex.Message}" };
                }
            });

        server.RegisterTool("AtlasMovePointer",
            "Move the visual pointer overlay to new coordinates.",
            async (args) =>
            {
                var x = args["x"]?.Value<double>() ?? throw new ArgumentException("x required");
                var y = args["y"]?.Value<double>() ?? throw new ArgumentException("y required");

                try
                {
                    var response = await http.PostAsJsonAsync("/atlas/pointer/move", new { x, y });
                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    return new { success = false, error = $"Failed to move pointer: {ex.Message}" };
                }
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["x"] = new JObject { ["type"] = "number", ["description"] = "X coordinate" },
                    ["y"] = new JObject { ["type"] = "number", ["description"] = "Y coordinate" }
                },
                ["required"] = new JArray { "x", "y" }
            });
    }
}
