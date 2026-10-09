using System.Reflection;

namespace Served.MCP;

/// <summary>
/// What this binary is built from, as stamped by the ServedMcpBuildStamp target in Served.MCP.csproj.
/// </summary>
public static class BuildStamp
{
    private static readonly Assembly Assembly = typeof(BuildStamp).Assembly;

    private static readonly Dictionary<string, string> Metadata = Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .GroupBy(a => a.Key)
        .ToDictionary(g => g.Key, g => g.Last().Value ?? "");

    /// <summary>Product version (Directory.Build.props), without any +metadata suffix.</summary>
    public static string Version { get; } =
        (Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
         ?? Assembly.GetName().Version?.ToString()
         ?? "0.0.0").Split('+')[0];

    public static string McpCommit => Metadata.GetValueOrDefault("McpCommit") ?? "unknown";
    public static bool McpDirty => Metadata.GetValueOrDefault("McpDirty") == "true";
    public static string SdkCommit => Metadata.GetValueOrDefault("SdkCommit") ?? "unknown";
    public static bool SdkDirty => Metadata.GetValueOrDefault("SdkDirty") == "true";
    public static string BuiltAtUtc => Metadata.GetValueOrDefault("BuiltAtUtc") ?? "unknown";

    /// <summary>
    /// Version as reported in serverInfo.version, e.g. "2026.10.1+mcp.78c3f33.sdk.bd9ac97.dirty".
    /// </summary>
    public static string ServerVersion =>
        $"{Version}+mcp.{Short(McpCommit)}{(McpDirty ? ".dirty" : "")}.sdk.{Short(SdkCommit)}{(SdkDirty ? ".dirty" : "")}";

    private static string Short(string sha) => sha.Length > 7 ? sha[..7] : sha;
}
