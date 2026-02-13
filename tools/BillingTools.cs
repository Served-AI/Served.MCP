using System.Text;
using Newtonsoft.Json.Linq;
using Served.SDK.Client;
using Served.SDK.Utilities;

namespace Served.MCP.Tools;

/// <summary>
/// SaaS Billing Tools - Plan catalog, subscriptions, funnel analytics, revenue metrics.
/// Provides AI access to billing operations and conversion optimization.
/// </summary>
public static class BillingTools
{
    /// <summary>
    /// Register all billing-related tools.
    /// </summary>
    public static void Register(McpServer server, ServedClient client)
    {
        // ----- Plan Catalog -----

        server.RegisterTool("GetSaasPlans",
            "List all active SaaS plans with pricing details. Returns plan names, prices (DKK/EUR), included features, and Stripe price IDs.",
            async (args) =>
            {
                var plans = await client.Billing.GetPlansAsync();

                var sb = new StringBuilder();
                sb.AppendLine("# SaaS Plans");
                sb.AppendLine();

                foreach (var plan in plans)
                {
                    sb.AppendLine($"## {plan.Name} ({plan.PlanId})");
                    if (!string.IsNullOrEmpty(plan.Badge))
                        sb.AppendLine($"  Badge: {plan.Badge}");
                    sb.AppendLine($"  Monthly: {plan.MonthlyPriceDkk?.ToString("N0") ?? "Custom"} DKK / {plan.MonthlyPriceEur?.ToString("N0") ?? "Custom"} EUR");
                    sb.AppendLine($"  Annual:  {plan.AnnualPriceDkk?.ToString("N0") ?? "Custom"} DKK / {plan.AnnualPriceEur?.ToString("N0") ?? "Custom"} EUR");
                    sb.AppendLine($"  Users:   {plan.IncludedUsers?.ToString() ?? "Custom"}");
                    sb.AppendLine($"  Contact Sales: {plan.IsContactSales}");
                    if (plan.Features?.Length > 0)
                    {
                        sb.AppendLine($"  Features:");
                        foreach (var f in plan.Features)
                            sb.AppendLine($"    - {f}");
                    }
                    sb.AppendLine();
                }

                return sb.ToString();
            });

        // ----- Subscription -----

        server.RegisterTool("GetSubscription",
            "Get the current tenant's subscription details including plan, billing cycle, seat count, add-ons, and Stripe status.",
            async (args) =>
            {
                var sub = await client.Billing.GetSubscriptionAsync();

                var sb = new StringBuilder();
                sb.AppendLine("# Current Subscription");
                sb.AppendLine();
                sb.AppendLine($"  Plan: {sub.PlanName ?? "Unknown"}");
                sb.AppendLine($"  Status: {sub.Status ?? "Unknown"}");
                sb.AppendLine($"  Users: {sub.UserCount}");
                if (sub.AddOns?.Length > 0)
                    sb.AppendLine($"  Add-Ons: {string.Join(", ", sub.AddOns)}");
                if (sub.CurrentPeriodStart.HasValue)
                    sb.AppendLine($"  Period: {sub.CurrentPeriodStart:yyyy-MM-dd} → {sub.CurrentPeriodEnd:yyyy-MM-dd}");
                if (sub.MonthlyRevenue.HasValue)
                    sb.AppendLine($"  MRR: {sub.MonthlyRevenue:N0} DKK");
                if (sub.CancelledAt.HasValue)
                    sb.AppendLine($"  Cancelled: {sub.CancelledAt:yyyy-MM-dd}");

                return sb.ToString();
            });

        // ----- Funnel Analytics -----

        server.RegisterTool("GetFunnelDashboard",
            "Get signup funnel analytics dashboard with conversion rates, drop-off points, average time per step, and breakdown by source/device. Use for conversion optimization.",
            async (args) =>
            {
                var days = args.GetOptionalInt("days", 30);
                var dashboard = await client.Billing.GetFunnelDashboardAsync(days);

                var sb = new StringBuilder();
                sb.AppendLine($"# Funnel Dashboard (Last {days} Days)");
                sb.AppendLine();
                sb.AppendLine($"  Total Sessions: {dashboard.TotalSessions}");
                sb.AppendLine($"  Conversions: {dashboard.Conversions}");
                sb.AppendLine($"  Conversion Rate: {dashboard.ConversionRate:P1}");
                sb.AppendLine($"  Avg Session Duration: {dashboard.AvgSessionDurationMs / 1000.0:N1}s");
                sb.AppendLine($"  Avg Steps Completed: {dashboard.AvgStepsCompleted:N1}");
                sb.AppendLine();

                if (dashboard.StepBreakdown?.Count > 0)
                {
                    sb.AppendLine("## Step Breakdown");
                    foreach (var kvp in dashboard.StepBreakdown)
                        sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                    sb.AppendLine();
                }

                if (dashboard.SourceBreakdown?.Count > 0)
                {
                    sb.AppendLine("## By Source");
                    foreach (var kvp in dashboard.SourceBreakdown)
                        sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                    sb.AppendLine();
                }

                if (dashboard.DeviceBreakdown?.Count > 0)
                {
                    sb.AppendLine("## By Device");
                    foreach (var kvp in dashboard.DeviceBreakdown)
                        sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }

                return sb.ToString();
            },
            JObject.Parse(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""days"": { ""type"": ""integer"", ""description"": ""Number of days to look back (default: 30)"" }
                },
                ""required"": []
            }"));

        server.RegisterTool("GetFunnelSessions",
            "Get recent signup funnel sessions with detailed step data. Shows individual user journeys for debugging and analysis.",
            async (args) =>
            {
                var limit = args.GetOptionalInt("limit", 20);
                var sessions = await client.Billing.GetFunnelSessionsAsync(limit);

                var sb = new StringBuilder();
                sb.AppendLine($"# Recent Funnel Sessions ({sessions.Length} results)");
                sb.AppendLine();

                foreach (var session in sessions)
                {
                    sb.AppendLine($"## Session: {session.SessionId}");
                    sb.AppendLine($"  Source: {session.Source ?? "unknown"}");
                    sb.AppendLine($"  Device: {session.DeviceType ?? "unknown"}");
                    sb.AppendLine($"  Steps: {session.StepsCompleted}");
                    sb.AppendLine($"  Last Step: {session.LastStep ?? "unknown"}");
                    sb.AppendLine($"  Duration: {(session.DurationMs ?? 0) / 1000.0:N1}s");
                    sb.AppendLine($"  Converted: {session.IsConverted}");
                    if (!string.IsNullOrEmpty(session.SelectedPlan))
                        sb.AppendLine($"  Plan: {session.SelectedPlan}");
                    sb.AppendLine();
                }

                return sb.ToString();
            },
            JObject.Parse(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""limit"": { ""type"": ""integer"", ""description"": ""Number of sessions to return (default: 20, max: 100)"" }
                },
                ""required"": []
            }"));

        // ----- Revenue Metrics -----

        server.RegisterTool("GetRevenueMetrics",
            "Get revenue metrics including MRR (Monthly Recurring Revenue), active subscriptions, churn rate, and growth trends.",
            async (args) =>
            {
                // Aggregate from subscription data
                var sub = await client.Billing.GetSubscriptionAsync();
                var plans = await client.Billing.GetPlansAsync();

                var sb = new StringBuilder();
                sb.AppendLine("# Revenue Metrics");
                sb.AppendLine();
                sb.AppendLine($"  Active Plans: {plans.Length}");
                if (sub.MonthlyRevenue.HasValue)
                    sb.AppendLine($"  Current MRR: {sub.MonthlyRevenue:N0} DKK");
                sb.AppendLine($"  Current Plan: {sub.PlanName ?? "Unknown"}");
                sb.AppendLine($"  Seat Count: {sub.UserCount}");
                sb.AppendLine();
                sb.AppendLine("Note: Full revenue analytics across all tenants requires admin access.");

                return sb.ToString();
            });

        // ----- AI Analysis -----

        server.RegisterTool("AnalyzeConversion",
            "AI-powered analysis of the signup funnel. Identifies bottlenecks, suggests optimizations, and highlights conversion opportunities based on recent funnel data.",
            async (args) =>
            {
                var days = args.GetOptionalInt("days", 30);
                var dashboard = await client.Billing.GetFunnelDashboardAsync(days);
                var sessions = await client.Billing.GetFunnelSessionsAsync(50);

                var sb = new StringBuilder();
                sb.AppendLine($"# Conversion Analysis (Last {days} Days)");
                sb.AppendLine();

                // Basic metrics
                sb.AppendLine("## Summary");
                sb.AppendLine($"  Sessions: {dashboard.TotalSessions}");
                sb.AppendLine($"  Conversions: {dashboard.Conversions}");
                sb.AppendLine($"  Rate: {dashboard.ConversionRate:P1}");
                sb.AppendLine();

                // Find drop-off points
                if (dashboard.StepBreakdown?.Count > 0)
                {
                    sb.AppendLine("## Drop-Off Analysis");
                    var steps = dashboard.StepBreakdown.OrderByDescending(kvp => kvp.Value).ToList();
                    var prevCount = steps.FirstOrDefault().Value;
                    foreach (var step in steps)
                    {
                        var dropOff = prevCount > 0 ? (double)(prevCount - step.Value) / prevCount * 100 : 0;
                        sb.AppendLine($"  {step.Key}: {step.Value} sessions ({dropOff:N1}% drop-off from previous)");
                        prevCount = step.Value;
                    }
                    sb.AppendLine();
                }

                // Device analysis
                if (dashboard.DeviceBreakdown?.Count > 0)
                {
                    sb.AppendLine("## Device Insights");
                    foreach (var kvp in dashboard.DeviceBreakdown)
                        sb.AppendLine($"  {kvp.Key}: {kvp.Value} sessions");
                    sb.AppendLine();
                }

                // Session patterns
                var abandoned = sessions.Count(s => !s.IsConverted);
                var converted = sessions.Count(s => s.IsConverted);
                var avgDuration = sessions.Where(s => s.DurationMs.HasValue).Select(s => s.DurationMs!.Value).DefaultIfEmpty(0).Average();

                sb.AppendLine("## Session Patterns (Last 50)");
                sb.AppendLine($"  Converted: {converted}");
                sb.AppendLine($"  Abandoned: {abandoned}");
                sb.AppendLine($"  Avg Duration: {avgDuration / 1000.0:N1}s");

                // Common abandonment steps
                var abandonSteps = sessions
                    .Where(s => !s.IsConverted && !string.IsNullOrEmpty(s.LastStep))
                    .GroupBy(s => s.LastStep)
                    .OrderByDescending(g => g.Count())
                    .Take(3);

                if (abandonSteps.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("## Top Abandonment Points");
                    foreach (var group in abandonSteps)
                        sb.AppendLine($"  {group.Key}: {group.Count()} sessions");
                }

                return sb.ToString();
            },
            JObject.Parse(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""days"": { ""type"": ""integer"", ""description"": ""Number of days to analyze (default: 30)"" }
                },
                ""required"": []
            }"));
    }
}
