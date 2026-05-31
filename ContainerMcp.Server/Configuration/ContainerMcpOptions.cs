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

internal sealed record ContainerMcpOptions(
    TransportMode Transport,
    string Urls,
    ContainerEngine DefaultEngine,
    string DefaultTarget,
    TimeSpan ApiTimeout,
    TimeSpan ApiProbeTimeout,
    TimeSpan ToolTimeout)
{
    public static ContainerMcpOptions From(string[] args)
    {
        var transport = ReadOption(args, "--transport")
            ?? Environment.GetEnvironmentVariable("CONTAINER_MCP_TRANSPORT")
            ?? "http";
        var urls = ReadOption(args, "--urls")
            ?? Environment.GetEnvironmentVariable("CONTAINER_MCP_HTTP_URLS")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
            ?? "http://127.0.0.1:7010";
        var defaultEngine = ReadOption(args, "--default-engine")
            ?? Environment.GetEnvironmentVariable("CONTAINER_MCP_DEFAULT_ENGINE")
            ?? "auto";
        var defaultTarget = ReadOption(args, "--default-target")
            ?? Environment.GetEnvironmentVariable("CONTAINER_MCP_DEFAULT_TARGET")
            ?? "local";
        var apiTimeout = ReadTimeout(
            ReadOption(args, "--api-timeout-seconds")
            ?? Environment.GetEnvironmentVariable("CONTAINER_MCP_API_TIMEOUT_SECONDS"),
            defaultSeconds: 10);
        var apiProbeTimeout = ReadTimeout(
            ReadOption(args, "--api-probe-timeout-seconds")
            ?? Environment.GetEnvironmentVariable("CONTAINER_MCP_API_PROBE_TIMEOUT_SECONDS"),
            defaultSeconds: 2);
        var toolTimeout = ReadTimeout(
            ReadOption(args, "--tool-timeout-seconds")
            ?? Environment.GetEnvironmentVariable("CONTAINER_MCP_TOOL_TIMEOUT_SECONDS"),
            defaultSeconds: 15);

        var normalized = NormalizeTimeouts(apiTimeout, apiProbeTimeout, toolTimeout);

        return new ContainerMcpOptions(
            ParseTransport(transport),
            urls,
            ParseEngine(defaultEngine),
            defaultTarget,
            normalized.ApiTimeout,
            normalized.ApiProbeTimeout,
            normalized.ToolTimeout);
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

    private static TimeSpan ReadTimeout(string? value, int defaultSeconds)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            seconds = defaultSeconds;
        }

        return TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 600));
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
