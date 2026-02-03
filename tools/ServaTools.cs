using System.Text;
using Newtonsoft.Json.Linq;
using Served.SDK.Client;

namespace Served.MCP.Tools;

/// <summary>
/// Serva AI Marketing Tools - Social media monitoring and automated response management.
/// </summary>
public static class ServaTools
{
    /// <summary>
    /// Register all Serva marketing and social monitoring tools.
    /// </summary>
    public static void Register(McpServer server, ServedClient client)
    {
        // ----- Queue Management -----

        server.RegisterTool("GetServaQueue",
            "Get the Serva review queue. Shows items pending human review before response.",
            async (args) =>
            {
                var response = await server.Http.GetAsync("/api/serva/queue");

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return $"Error getting Serva queue: {error}";
                }

                var queue = JArray.Parse(await response.Content.ReadAsStringAsync());

                if (queue.Count == 0)
                {
                    return "Serva queue is empty. No items pending review.";
                }

                var sb = new StringBuilder();
                sb.AppendLine("# Serva Review Queue");
                sb.AppendLine();

                foreach (var item in queue)
                {
                    sb.AppendLine($"## Item {item["id"]}");
                    sb.AppendLine($"**Source:** {item["source"]} | **Priority:** {item["priority"]}");
                    sb.AppendLine($"**Status:** {item["status"]} | **Created:** {item["createdAt"]}");
                    sb.AppendLine();
                    sb.AppendLine("**Original:**");
                    sb.AppendLine($"> {item["originalContent"]}");
                    sb.AppendLine();
                    sb.AppendLine("**Draft Response:**");
                    sb.AppendLine($"> {item["draftResponse"]}");
                    sb.AppendLine();
                    sb.AppendLine("---");
                }

                return sb.ToString();
            });

        server.RegisterTool("ApproveServaItem",
            "Approve a Serva queue item, optionally with edited response.",
            async (args) =>
            {
                var itemId = args["itemId"]?.Value<long>() ?? throw new ArgumentException("itemId required");
                var editedResponse = args["editedResponse"]?.Value<string>();

                var payload = new JObject();
                if (!string.IsNullOrEmpty(editedResponse))
                {
                    payload["editedResponse"] = editedResponse;
                }

                var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
                var response = await server.Http.PostAsync($"/api/serva/queue/{itemId}/approve", content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return $"Error approving item: {error}";
                }

                return $"Item {itemId} approved and response sent.";
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["itemId"] = new JObject { ["type"] = "integer", ["description"] = "Queue item ID to approve" },
                    ["editedResponse"] = new JObject { ["type"] = "string", ["description"] = "Edited response text (optional)" }
                },
                ["required"] = new JArray { "itemId" }
            });

        server.RegisterTool("RejectServaItem",
            "Reject a Serva queue item with optional reason.",
            async (args) =>
            {
                var itemId = args["itemId"]?.Value<long>() ?? throw new ArgumentException("itemId required");
                var reason = args["reason"]?.Value<string>();

                var payload = new JObject { ["reason"] = reason };
                var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
                var response = await server.Http.PostAsync($"/api/serva/queue/{itemId}/reject", content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return $"Error rejecting item: {error}";
                }

                return $"Item {itemId} rejected.";
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["itemId"] = new JObject { ["type"] = "integer", ["description"] = "Queue item ID to reject" },
                    ["reason"] = new JObject { ["type"] = "string", ["description"] = "Reason for rejection (optional)" }
                },
                ["required"] = new JArray { "itemId" }
            });

        server.RegisterTool("GetServaSettings",
            "Get Serva configuration settings including automation level and brand voice.",
            async (args) =>
            {
                var response = await server.Http.GetAsync("/api/serva/settings");

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return $"Error getting Serva settings: {error}";
                }

                var settings = JObject.Parse(await response.Content.ReadAsStringAsync());

                var sb = new StringBuilder();
                sb.AppendLine("# Serva Settings");
                sb.AppendLine();
                sb.AppendLine($"**Automation Level:** {settings["automationLevel"]} (1=manual, 4=full auto)");
                sb.AppendLine($"**Brand Voice:** {settings["brandVoice"]}");
                sb.AppendLine($"**Language:** {settings["responseLanguage"]}");
                sb.AppendLine($"**Auto-Response Enabled:** {settings["autoResponseEnabled"]}");

                if (settings["keywords"] is JArray keywords && keywords.Count > 0)
                {
                    sb.AppendLine($"**Keywords:** {string.Join(", ", keywords.Select(k => k.ToString()))}");
                }

                return sb.ToString();
            });

        // ----- Social Media Monitoring -----

        server.RegisterTool("GetSocialMentions",
            "Get social media mentions. Filter by status and platform.",
            async (args) =>
            {
                var status = args["status"]?.Value<string>();
                var platform = args["platform"]?.Value<string>();
                var limit = args["limit"]?.Value<int>() ?? 20;

                var url = $"/api/social/monitoring/mentions?limit={limit}";
                if (!string.IsNullOrEmpty(status)) url += $"&status={status}";
                if (!string.IsNullOrEmpty(platform)) url += $"&platform={platform}";

                var response = await server.Http.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return $"Error getting mentions: {error}";
                }

                var mentions = JArray.Parse(await response.Content.ReadAsStringAsync());

                if (mentions.Count == 0)
                {
                    return "No social mentions found.";
                }

                var sb = new StringBuilder();
                sb.AppendLine("# Social Media Mentions");
                sb.AppendLine();

                foreach (var m in mentions)
                {
                    var sentiment = m["sentimentScore"]?.Value<decimal>() ?? 0;
                    var emoji = sentiment > 0.3m ? "🟢" : sentiment < -0.3m ? "🔴" : "🟡";

                    sb.AppendLine($"## {emoji} {m["platform"]} - {m["authorName"]}");
                    sb.AppendLine($"**ID:** {m["id"]} | **Sentiment:** {sentiment:F2}");
                    sb.AppendLine($"**Status:** {m["status"]} | **Date:** {m["createdAt"]}");
                    sb.AppendLine();
                    sb.AppendLine($"> {m["content"]}");
                    sb.AppendLine();
                    sb.AppendLine("---");
                }

                return sb.ToString();
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["status"] = new JObject { ["type"] = "string", ["description"] = "Filter by status (new, reviewed, responded)" },
                    ["platform"] = new JObject { ["type"] = "string", ["description"] = "Filter by platform (twitter, facebook, instagram, linkedin)" },
                    ["limit"] = new JObject { ["type"] = "integer", ["description"] = "Maximum mentions to return (default: 20)" }
                }
            });

        server.RegisterTool("RespondToMention",
            "Send a response to a social media mention.",
            async (args) =>
            {
                var mentionId = args["mentionId"]?.Value<long>() ?? throw new ArgumentException("mentionId required");
                var responseText = args["response"]?.Value<string>() ?? throw new ArgumentException("response required");
                var scheduleFor = args["scheduleFor"]?.Value<string>();

                var payload = new JObject
                {
                    ["response"] = responseText
                };
                if (!string.IsNullOrEmpty(scheduleFor))
                {
                    payload["scheduleFor"] = scheduleFor;
                }

                var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
                var response = await server.Http.PostAsync($"/api/social/monitoring/mentions/{mentionId}/respond", content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return $"Error responding to mention: {error}";
                }

                return string.IsNullOrEmpty(scheduleFor)
                    ? $"Response sent to mention {mentionId}."
                    : $"Response scheduled for {scheduleFor} to mention {mentionId}.";
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["mentionId"] = new JObject { ["type"] = "integer", ["description"] = "Mention ID to respond to" },
                    ["response"] = new JObject { ["type"] = "string", ["description"] = "Response text" },
                    ["scheduleFor"] = new JObject { ["type"] = "string", ["format"] = "date-time", ["description"] = "Schedule response for later (optional)" }
                },
                ["required"] = new JArray { "mentionId", "response" }
            });

        server.RegisterTool("GetSocialAnalytics",
            "Get social media analytics and engagement metrics.",
            async (args) =>
            {
                var days = args["days"]?.Value<int>() ?? 7;

                var response = await server.Http.GetAsync($"/api/social/monitoring/analytics?days={days}");

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return $"Error getting analytics: {error}";
                }

                var analytics = JObject.Parse(await response.Content.ReadAsStringAsync());

                var sb = new StringBuilder();
                sb.AppendLine($"# Social Analytics (Last {days} days)");
                sb.AppendLine();
                sb.AppendLine($"**Total Mentions:** {analytics["totalMentions"]}");
                sb.AppendLine($"**Avg Sentiment:** {analytics["averageSentiment"]:F2}");
                sb.AppendLine($"**Response Rate:** {analytics["responseRate"]:P0}");
                sb.AppendLine($"**Avg Response Time:** {analytics["averageResponseTime"]}");
                sb.AppendLine();

                if (analytics["byPlatform"] is JObject platforms)
                {
                    sb.AppendLine("## By Platform");
                    foreach (var p in platforms.Properties())
                    {
                        sb.AppendLine($"- **{p.Name}:** {p.Value["count"]} mentions, {p.Value["sentiment"]:F2} sentiment");
                    }
                }

                return sb.ToString();
            },
            new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["days"] = new JObject { ["type"] = "integer", ["description"] = "Number of days to analyze (default: 7)" }
                }
            });
    }
}
