using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;

namespace Served.MCP.Tools.Services;

/// <summary>
/// Lightweight infra service for MCP tools.
/// Reads state files directly, shells out to `served iac` for heavy operations.
/// </summary>
public class InfraMcpService
{
    private static readonly string StateDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".served", "infra", "state");

    private static readonly string HistoryDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".served", "infra", "history");

    // ─── State Reading ───────────────────────────────────────────

    public List<string> ListStacks()
    {
        if (!Directory.Exists(StateDir))
            return new List<string>();

        return Directory.GetFiles(StateDir, "*.state")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(n => n)
            .ToList();
    }

    public InfraStateInfo GetStackState(string stackName)
    {
        var path = Path.Combine(StateDir, $"{stackName}.state");
        if (!File.Exists(path))
            return null;

        // State is encrypted, we need to shell out
        var result = RunCli($"iac state {stackName}");
        if (result.ExitCode != 0)
            return null;

        return new InfraStateInfo
        {
            StackName = stackName,
            Output = result.StdOut
        };
    }

    // ─── CLI Delegation ──────────────────────────────────────────

    public async Task<CliResult> PlanAsync(string configPath)
    {
        return await RunCliAsync($"iac plan \"{configPath}\"");
    }

    public async Task<CliResult> ApplyAsync(string configPath, bool autoApprove = true)
    {
        var flag = autoApprove ? " --auto-approve" : "";
        return await RunCliAsync($"iac apply \"{configPath}\"{flag}");
    }

    public async Task<CliResult> DestroyAsync(string stackName, bool autoApprove = true)
    {
        var flag = autoApprove ? " --auto-approve" : "";
        return await RunCliAsync($"iac destroy {stackName}{flag}");
    }

    public async Task<CliResult> DriftAsync(string stackName)
    {
        return await RunCliAsync($"iac drift {stackName}");
    }

    public async Task<CliResult> ImportAsync(string provider, string id, string stack = "default")
    {
        return await RunCliAsync($"iac import {provider} {id} --stack {stack}");
    }

    // ─── Recommendations (embedded, no CLI needed) ───────────────

    public InfraRecommendation Recommend(string workload)
    {
        return workload.ToLower() switch
        {
            "ai-inference" => new InfraRecommendation
            {
                Workload = "ai-inference",
                Description = "AI model inference (Ollama, vLLM, Whisper)",
                Entries = new()
                {
                    new() { Rank = 1, Label = "RECOMMENDED", Provider = "ovh", Plan = "b3-16", Spec = "16 ARM cores, 64GB RAM, GPU", PriceMonthly = 52.00m, Location = "Gravelines (FR)", Reason = "Best ARM + GPU price in EU" },
                    new() { Rank = 2, Label = "BUDGET", Provider = "hetzner", Plan = "cax41", Spec = "16 ARM cores, 32GB RAM", PriceMonthly = 23.49m, Location = "Falkenstein (DE)", Reason = "No GPU but excellent CPU inference price" },
                    new() { Rank = 3, Label = "LOCAL", Provider = "proxmox", Plan = "custom", Spec = "12+ cores, 64GB RAM (shared)", PriceMonthly = 0, Location = "Local (Eden)", Reason = "Free, but shared resources" }
                }
            },
            "web-api" => new InfraRecommendation
            {
                Workload = "web-api",
                Description = "HTTP API server (.NET, Node, Go)",
                Entries = new()
                {
                    new() { Rank = 1, Label = "RECOMMENDED", Provider = "hetzner", Plan = "cx32", Spec = "4 shared cores, 8GB RAM", PriceMonthly = 7.69m, Location = "Falkenstein (DE)", Reason = "Best price/performance for EU API" },
                    new() { Rank = 2, Label = "LOCAL", Provider = "proxmox", Plan = "custom", Spec = "4+ cores, 8GB+ RAM", PriceMonthly = 0, Location = "Local (Eden)", Reason = "Free, zero latency" }
                }
            },
            "database" => new InfraRecommendation
            {
                Workload = "database",
                Description = "MySQL, PostgreSQL, Redis, MongoDB",
                Entries = new()
                {
                    new() { Rank = 1, Label = "RECOMMENDED", Provider = "hetzner", Plan = "ccx33", Spec = "16 dedicated cores, 64GB RAM", PriceMonthly = 36.49m, Location = "Falkenstein (DE)", Reason = "Dedicated vCPU, NVMe storage" },
                    new() { Rank = 2, Label = "LOCAL", Provider = "proxmox", Plan = "custom", Spec = "4+ cores, 16GB+ RAM", PriceMonthly = 0, Location = "Local (Eden)", Reason = "Local SSD, no network latency" }
                }
            },
            "build-server" => new InfraRecommendation
            {
                Workload = "build-server",
                Description = "CI/CD build host (Docker, Go, .NET, Rust)",
                Entries = new()
                {
                    new() { Rank = 1, Label = "RECOMMENDED", Provider = "hetzner", Plan = "ccx33", Spec = "16 dedicated cores, 64GB RAM", PriceMonthly = 36.49m, Location = "Falkenstein (DE)", Reason = "Cheap, powerful, disposable" },
                    new() { Rank = 2, Label = "LOCAL", Provider = "proxmox", Plan = "custom", Spec = "8+ cores, 32GB+ RAM", PriceMonthly = 0, Location = "Local (Eden)", Reason = "Free local builds" }
                }
            },
            "k8s-worker" => new InfraRecommendation
            {
                Workload = "k8s-worker",
                Description = "Kubernetes worker node",
                Entries = new()
                {
                    new() { Rank = 1, Label = "RECOMMENDED", Provider = "proxmox", Plan = "custom", Spec = "12+ cores, 64GB+ RAM", PriceMonthly = 0, Location = "Local (Eden)", Reason = "Eden cluster, local networking" },
                    new() { Rank = 2, Label = "CLOUD", Provider = "hetzner", Plan = "ccx23", Spec = "8 dedicated cores, 32GB RAM", PriceMonthly = 19.49m, Location = "Falkenstein (DE)", Reason = "Affordable cloud workers" }
                }
            },
            "game-server" => new InfraRecommendation
            {
                Workload = "game-server",
                Description = "Game server (Go, UDP-heavy, low latency)",
                Entries = new()
                {
                    new() { Rank = 1, Label = "RECOMMENDED", Provider = "hetzner", Plan = "ccx23", Spec = "8 dedicated cores, 32GB RAM", PriceMonthly = 19.49m, Location = "Falkenstein (DE)", Reason = "Low latency EU, excellent network" },
                    new() { Rank = 2, Label = "LOCAL", Provider = "proxmox", Plan = "custom", Spec = "4+ cores, 8GB+ RAM", PriceMonthly = 0, Location = "Local (Eden)", Reason = "Zero latency for local dev" }
                }
            },
            _ => null
        };
    }

    public List<string> ListWorkloads() => new()
    {
        "ai-inference", "web-api", "database", "build-server", "k8s-worker", "game-server", "monitoring"
    };

    // ─── Cost Estimation (embedded) ──────────────────────────────

    public decimal EstimateCost(string provider, string serverType, int cpu)
    {
        if (provider == "proxmox") return 0;

        if (!string.IsNullOrEmpty(serverType))
        {
            return (provider, serverType) switch
            {
                ("hetzner", "cx22") => 4.35m,
                ("hetzner", "cx32") => 7.69m,
                ("hetzner", "cx42") => 14.69m,
                ("hetzner", "cax11") => 3.79m,
                ("hetzner", "cax21") => 6.49m,
                ("hetzner", "cax31") => 12.49m,
                ("hetzner", "cax41") => 23.49m,
                ("hetzner", "ccx23") => 19.49m,
                ("hetzner", "ccx33") => 36.49m,
                ("hetzner", "ccx43") => 69.49m,
                ("ovh", "s1-2") => 3.50m,
                ("ovh", "s1-4") => 6.50m,
                ("ovh", "b3-8") => 26.00m,
                ("ovh", "b3-16") => 52.00m,
                _ => 0
            };
        }

        var cpuCount = cpu > 0 ? cpu : 2;
        return provider switch
        {
            "hetzner" => cpuCount switch { <= 2 => 4.35m, <= 4 => 7.69m, <= 8 => 19.49m, _ => 36.49m },
            "ovh" => cpuCount switch { <= 2 => 3.50m, <= 4 => 6.50m, <= 8 => 26.00m, _ => 52.00m },
            _ => 0
        };
    }

    // ─── History ─────────────────────────────────────────────────

    public List<InfraHistoryRecord> GetHistory(string? stackName = null, int limit = 20)
    {
        if (!Directory.Exists(HistoryDir))
            return new List<InfraHistoryRecord>();

        var all = new List<InfraHistoryRecord>();
        var pattern = string.IsNullOrEmpty(stackName) ? "*.history.json" : $"{stackName}.history.json";

        foreach (var file in Directory.GetFiles(HistoryDir, pattern))
        {
            try
            {
                var entries = JsonConvert.DeserializeObject<List<InfraHistoryRecord>>(File.ReadAllText(file));
                if (entries != null) all.AddRange(entries);
            }
            catch { }
        }

        return all.OrderByDescending(e => e.Timestamp).Take(limit).ToList();
    }

    // ─── CLI Execution ───────────────────────────────────────────

    private static CliResult RunCli(string args)
    {
        // Use Task.Run to avoid deadlocking the synchronization context
        return Task.Run(() => RunCliAsync(args)).GetAwaiter().GetResult();
    }

    private static async Task<CliResult> RunCliAsync(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "served",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try { await process.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException) { process.Kill(); }

        return new CliResult
        {
            ExitCode = process.ExitCode,
            StdOut = stdout,
            StdErr = stderr
        };
    }
}

// ─── MCP Service Models ──────────────────────────────────────────

public class InfraStateInfo
{
    public string StackName { get; set; } = "";
    public string Output { get; set; } = "";
}

public class CliResult
{
    public int ExitCode { get; set; }
    public string StdOut { get; set; } = "";
    public string StdErr { get; set; } = "";
    public bool Success => ExitCode == 0;
}

public class InfraRecommendation
{
    public string Workload { get; set; } = "";
    public string Description { get; set; } = "";
    public List<InfraRecEntry> Entries { get; set; } = new();
}

public class InfraRecEntry
{
    public int Rank { get; set; }
    public string Label { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Plan { get; set; } = "";
    public string Spec { get; set; } = "";
    public decimal PriceMonthly { get; set; }
    public string Location { get; set; } = "";
    public string Reason { get; set; } = "";
}

public class InfraHistoryRecord
{
    public DateTime Timestamp { get; set; }
    public string StackName { get; set; } = "";
    public string Action { get; set; } = "";
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Destroyed { get; set; }
    public int Failed { get; set; }
    public double DurationSeconds { get; set; }
    public List<string> Resources { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
