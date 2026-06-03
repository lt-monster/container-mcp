namespace ContainerMcp.Configuration;

internal enum TransportMode
{
    Stdio,
    Http
}

internal enum ContainerEngine
{
    Auto,
    Docker,
    Podman
}

internal sealed record HttpToken(
    string Id,
    string Value,
    bool Enabled,
    DateTimeOffset? CreatedAt,
    string? Description);

internal sealed record ContainerMcpOptions(
    TransportMode Transport,
    string Urls,
    ContainerEngine DefaultEngine,
    string DefaultTarget,
    TimeSpan ApiTimeout,
    TimeSpan ApiProbeTimeout,
    TimeSpan ToolTimeout,
    long MaxHttpRequestBodyBytes,
    IReadOnlyList<HttpToken> HttpTokens)
{
    public static ContainerMcpOptions From(string[] args) =>
        From(args, AppContext.BaseDirectory);

    internal static ContainerMcpOptions From(string[] args, string appBaseDirectory)
    {
        var config = ContainerMcpConfigurationLoader.Load(args, appBaseDirectory);
        var transport = ReadOption(args, "--transport")
            ?? Environment.GetEnvironmentVariable("CONTAINER_MCP_TRANSPORT")
            ?? config?.Transport
            ?? "http";
        var urls = ReadOption(args, "--urls")
            ?? Environment.GetEnvironmentVariable("CONTAINER_MCP_HTTP_URLS")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
            ?? config?.Urls
            ?? "http://127.0.0.1:7010";
        var defaultEngine = ReadOption(args, "--default-engine")
            ?? Environment.GetEnvironmentVariable("CONTAINER_MCP_DEFAULT_ENGINE")
            ?? config?.DefaultEngine
            ?? "auto";
        var defaultTarget = ReadOption(args, "--default-target")
            ?? Environment.GetEnvironmentVariable("CONTAINER_MCP_DEFAULT_TARGET")
            ?? config?.DefaultTarget
            ?? "local";
        var apiTimeout = ReadTimeout(
            ReadOption(args, "--api-timeout-seconds")
            ?? Environment.GetEnvironmentVariable("CONTAINER_MCP_API_TIMEOUT_SECONDS"),
            config?.Timeouts?.ApiSeconds,
            defaultSeconds: 10);
        var apiProbeTimeout = ReadTimeout(
            ReadOption(args, "--api-probe-timeout-seconds")
            ?? Environment.GetEnvironmentVariable("CONTAINER_MCP_API_PROBE_TIMEOUT_SECONDS"),
            config?.Timeouts?.ApiProbeSeconds,
            defaultSeconds: 2);
        var toolTimeout = ReadTimeout(
            ReadOption(args, "--tool-timeout-seconds")
            ?? Environment.GetEnvironmentVariable("CONTAINER_MCP_TOOL_TIMEOUT_SECONDS"),
            config?.Timeouts?.ToolSeconds,
            defaultSeconds: 15);
        var maxHttpRequestBodyBytes = ReadLong(
            ReadOption(args, "--http-max-request-body-bytes")
            ?? Environment.GetEnvironmentVariable("CONTAINER_MCP_HTTP_MAX_REQUEST_BODY_BYTES"),
            config?.Http?.MaxRequestBodyBytes,
            ProgramSupport.MaxMcpHttpRequestBodyBytes,
            min: 1,
            max: 16 * 1024 * 1024);
        var httpTokens = HttpTokenValidator.ValidTokens(config?.Http?.Tokens ?? []);

        var normalized = NormalizeTimeouts(apiTimeout, apiProbeTimeout, toolTimeout);

        return new ContainerMcpOptions(
            ParseTransport(transport),
            urls,
            ParseEngine(defaultEngine),
            defaultTarget,
            normalized.ApiTimeout,
            normalized.ApiProbeTimeout,
            normalized.ToolTimeout,
            maxHttpRequestBodyBytes,
            httpTokens);
    }

    public ContainerEngine ResolveRequestedEngine(string? engine)
    {
        if (string.IsNullOrWhiteSpace(engine))
        {
            return DefaultEngine;
        }

        return ParseEngine(engine);
    }

    private static string? ReadOption(string[] args, string name)
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

    private static TransportMode ParseTransport(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "http" => TransportMode.Http,
            "streamable-http" => TransportMode.Http,
            "stdio" => TransportMode.Stdio,
            _ => TransportMode.Stdio
        };

    public static ContainerEngine ParseEngine(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "docker" => ContainerEngine.Docker,
            "podman" => ContainerEngine.Podman,
            _ => ContainerEngine.Auto
        };

    private static bool ReadBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static TimeSpan ReadTimeout(string? value, int? configSeconds, int defaultSeconds)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            seconds = configSeconds ?? defaultSeconds;
        }

        return TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 600));
    }

    private static long ReadLong(string? value, long? configValue, long defaultValue, long min, long max)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            result = configValue ?? defaultValue;
        }

        return Math.Clamp(result, min, max);
    }

    private static (TimeSpan ApiTimeout, TimeSpan ApiProbeTimeout, TimeSpan ToolTimeout) NormalizeTimeouts(
        TimeSpan apiTimeout,
        TimeSpan apiProbeTimeout,
        TimeSpan toolTimeout)
    {
        if (apiProbeTimeout > apiTimeout)
        {
            apiProbeTimeout = apiTimeout;
        }

        if (apiTimeout > toolTimeout)
        {
            apiTimeout = toolTimeout;
        }

        if (apiProbeTimeout > apiTimeout)
        {
            apiProbeTimeout = apiTimeout;
        }

        return (apiTimeout, apiProbeTimeout, toolTimeout);
    }
}
