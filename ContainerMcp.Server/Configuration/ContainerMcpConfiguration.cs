using ContainerMcp.Models;
using System.Text.Json;

namespace ContainerMcp.Configuration;

internal sealed record ContainerMcpConfiguration(
    int Version,
    string? Transport,
    string? Urls,
    string? DefaultEngine,
    string? DefaultTarget,
    TimeoutConfiguration? Timeouts,
    HttpConfiguration? Http);

internal sealed record TimeoutConfiguration(
    int? ToolSeconds,
    int? ApiSeconds,
    int? ApiProbeSeconds);

internal sealed record HttpConfiguration(
    long? MaxRequestBodyBytes,
    IReadOnlyList<HttpTokenConfiguration>? Tokens);

internal sealed record HttpTokenConfiguration(
    string? Id,
    string? Value,
    bool? Enabled,
    DateTimeOffset? CreatedAt,
    string? Description);

internal sealed record ResolvedConfigurationFile(string? Path, bool IsExplicit);

internal static class ContainerMcpConfigurationLoader
{
    public const string DefaultConfigurationFileName = "container-mcp.config.json";

    public static ContainerMcpConfiguration? Load(string[] args, string appBaseDirectory)
    {
        var resolved = Resolve(args, appBaseDirectory);
        if (resolved.Path is null)
        {
            return null;
        }

        if (!File.Exists(resolved.Path))
        {
            if (resolved.IsExplicit)
            {
                throw new InvalidOperationException($"Configuration file not found: {resolved.Path}");
            }

            return null;
        }

        try
        {
            using var stream = File.OpenRead(resolved.Path);
            return JsonSerializer.Deserialize(stream, ContainerMcpJsonContext.Default.ContainerMcpConfiguration);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Configuration file is not valid JSON: {resolved.Path}", ex);
        }
    }

    public static ResolvedConfigurationFile Resolve(string[] args, string appBaseDirectory)
    {
        if (ReadOption(args, "--config") is { } argPath)
        {
            return new ResolvedConfigurationFile(Path.GetFullPath(argPath), IsExplicit: true);
        }

        if (Environment.GetEnvironmentVariable("CONTAINER_MCP_CONFIG") is { Length: > 0 } envPath)
        {
            return new ResolvedConfigurationFile(Path.GetFullPath(envPath), IsExplicit: true);
        }

        return new ResolvedConfigurationFile(
            Path.Combine(appBaseDirectory, DefaultConfigurationFileName),
            IsExplicit: false);
    }

    internal static string? ReadOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }

            if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[(name.Length + 1)..];
            }
        }

        return null;
    }
}
