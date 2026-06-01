using System.Text.Json.Nodes;
using ContainerMcp.ContainerRuntime;
using ContainerMcp.Models;

namespace ContainerMcp.Tools;

internal sealed class ContainerCreateRequestBuilder
{
    private static readonly string[] RestartPolicies = ["no", "always", "unless-stopped", "on-failure"];

    private readonly VolumePolicy _volumePolicy;

    public ContainerCreateRequestBuilder(VolumePolicy volumePolicy) => _volumePolicy = volumePolicy;

    public static string BuildCreatePath(string? name, string? platform)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(name))
        {
            query.Add("name=" + Uri.EscapeDataString(name));
        }

        if (!string.IsNullOrWhiteSpace(platform))
        {
            query.Add("platform=" + Uri.EscapeDataString(platform));
        }

        return query.Count == 0 ? "/containers/create" : "/containers/create?" + string.Join('&', query);
    }

    public JsonObject Build(JsonElement args, string image)
    {
        var body = new JsonObject
        {
            ["Image"] = image
        };

        AddOptionalString(body, "WorkingDir", args, "workingDir");
        AddOptionalString(body, "User", args, "user");
        AddOptionalString(body, "Hostname", args, "hostname");
        AddOptionalBool(body, "Tty", args, "tty");

        if (ToolArgumentReader.OptionalStringArray(args, "command") is { Length: > 0 } command)
        {
            body["Cmd"] = StringArrayNode(command);
        }
        else if (ToolArgumentReader.OptionalString(args, "command") is { } commandLine)
        {
            body["Cmd"] = JsonNodeExtensions.Array(JsonValue.Create(commandLine));
        }

        if (ToolArgumentReader.OptionalStringDictionary(args, "env") is { Count: > 0 } env)
        {
            body["Env"] = StringArrayNode(env.Select(pair => $"{pair.Key}={pair.Value}"));
        }

        if (ToolArgumentReader.OptionalStringDictionary(args, "labels") is { Count: > 0 } labels)
        {
            body["Labels"] = JsonNodeExtensions.StringMapNode(labels);
        }

        if (ToolArgumentReader.OptionalStringArray(args, "entrypoint") is { Length: > 0 } entrypoint)
        {
            body["Entrypoint"] = StringArrayNode(entrypoint);
        }

        if (BuildHealthcheck(args) is { } healthcheck)
        {
            body["Healthcheck"] = healthcheck;
        }

        var hostConfig = new JsonObject();
        AddOptionalString(hostConfig, "NetworkMode", args, "networkMode");
        AddOptionalNonNegativeLong(hostConfig, "Memory", args, "memoryBytes", allowMinusOne: false);
        AddOptionalNonNegativeLong(hostConfig, "MemorySwap", args, "memorySwapBytes", allowMinusOne: true);
        AddOptionalNonNegativeLong(hostConfig, "MemoryReservation", args, "memoryReservationBytes", allowMinusOne: false);
        AddOptionalNonNegativeInt(hostConfig, "CpuShares", args, "cpuShares");
        AddOptionalNonNegativeInt(hostConfig, "CpuQuota", args, "cpuQuota");
        AddOptionalNonNegativeInt(hostConfig, "CpuPeriod", args, "cpuPeriod");
        AddOptionalNonNegativeLong(hostConfig, "NanoCpus", args, "nanoCpus", allowMinusOne: false);
        AddOptionalNonNegativeLong(hostConfig, "PidsLimit", args, "pidsLimit", allowMinusOne: true);

        if (ToolArgumentReader.OptionalString(args, "restartPolicy") is { } restartPolicy)
        {
            hostConfig["RestartPolicy"] = new JsonObject { ["Name"] = ValidateRestartPolicy(restartPolicy) };
        }

        var volumes = _volumePolicy.ValidateContainerCreateVolumes(ToolArgumentReader.OptionalStringArray(args, "volumes"));
        if (volumes.Length > 0)
        {
            hostConfig["Binds"] = StringArrayNode(volumes);
        }

        var portBindings = BuildPortBindings(args);
        if (portBindings.Count > 0)
        {
            hostConfig["PortBindings"] = portBindings;
            body["ExposedPorts"] = BuildExposedPorts(portBindings);
        }

        if (hostConfig.Count > 0)
        {
            body["HostConfig"] = hostConfig;
        }

        return body;
    }

    private static JsonObject? BuildHealthcheck(JsonElement args)
    {
        if (!args.TryGetProperty("healthcheck", out var healthcheck) || healthcheck.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        var result = new JsonObject();
        if (ToolArgumentReader.OptionalStringArray(healthcheck, "test") is { Length: > 0 } test)
        {
            result["Test"] = StringArrayNode(test);
        }

        AddOptionalHealthcheckDuration(result, "Interval", healthcheck, "intervalNanoseconds");
        AddOptionalHealthcheckDuration(result, "Timeout", healthcheck, "timeoutNanoseconds");
        AddOptionalHealthcheckDuration(result, "StartPeriod", healthcheck, "startPeriodNanoseconds");
        AddOptionalNonNegativeInt(result, "Retries", healthcheck, "retries", pathPrefix: "healthcheck.");

        return result.Count == 0 ? null : result;
    }

    private static JsonObject BuildPortBindings(JsonElement args)
    {
        var result = new JsonObject();
        if (!args.TryGetProperty("ports", out var ports) || ports.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return result;
        }

        if (ports.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in ports.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    AddPortMapping(result, item.GetString()!);
                }
            }

            return result;
        }

        if (ports.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in ports.EnumerateObject())
            {
                var containerPort = NormalizeContainerPort(property.Name, property.Name);
                var hostPort = property.Value.ValueKind == JsonValueKind.Number
                    ? property.Value.GetInt32().ToString(CultureInfo.InvariantCulture)
                    : property.Value.GetString();
                result[containerPort] = JsonNodeExtensions.Array(new JsonObject { ["HostPort"] = ValidateHostPort(hostPort ?? string.Empty) });
            }
        }

        return result;
    }

    private static JsonObject BuildExposedPorts(JsonObject portBindings)
    {
        var exposed = new JsonObject();
        foreach (var binding in portBindings)
        {
            exposed[binding.Key] = new JsonObject();
        }

        return exposed;
    }

    private static void AddPortMapping(JsonObject result, string value)
    {
        var parts = value.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            var port = NormalizeContainerPort(parts[0], value);
            result[port] = JsonNodeExtensions.Array(new JsonObject());
            return;
        }

        if (parts.Length == 2)
        {
            var containerPort = NormalizeContainerPort(parts[1], value);
            result[containerPort] = JsonNodeExtensions.Array(new JsonObject { ["HostPort"] = ValidateHostPort(parts[0]) });
            return;
        }

        if (parts.Length == 3)
        {
            var containerPort = NormalizeContainerPort(parts[2], value);
            result[containerPort] = JsonNodeExtensions.Array(new JsonObject { ["HostIp"] = parts[0], ["HostPort"] = ValidateHostPort(parts[1]) });
            return;
        }

        throw InvalidArgument(
            $"Invalid port mapping '{value}'. Expected containerPort[/protocol], hostPort:containerPort[/protocol], or hostIp:hostPort:containerPort[/protocol].");
    }

    private static JsonArray StringArrayNode(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.AddNode(JsonValue.Create(value));
        }

        return array;
    }

    private static void AddOptionalString(JsonObject target, string dockerName, JsonElement args, string argumentName)
    {
        if (ToolArgumentReader.OptionalString(args, argumentName) is { } value)
        {
            target[dockerName] = value;
        }
    }

    private static void AddOptionalBool(JsonObject target, string dockerName, JsonElement args, string argumentName)
    {
        if (args.TryGetProperty(argumentName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            target[dockerName] = value.GetBoolean();
        }
    }

    private static void AddOptionalNonNegativeInt(JsonObject target, string dockerName, JsonElement args, string argumentName, string pathPrefix = "")
    {
        var value = ToolArgumentReader.OptionalInt(args, argumentName);
        if (value is null)
        {
            return;
        }

        if (value.Value < 0)
        {
            throw InvalidArgument($"Argument '{pathPrefix}{argumentName}' must be greater than or equal to 0.");
        }

        target[dockerName] = value.Value;
    }

    private static void AddOptionalNonNegativeLong(JsonObject target, string dockerName, JsonElement args, string argumentName, bool allowMinusOne)
    {
        var value = ToolArgumentReader.OptionalLong(args, argumentName);
        if (value is null)
        {
            return;
        }

        if (value.Value < 0 && !(allowMinusOne && value.Value == -1))
        {
            throw InvalidArgument($"Argument '{argumentName}' must be greater than or equal to 0.");
        }

        target[dockerName] = value.Value;
    }

    private static void AddOptionalHealthcheckDuration(JsonObject target, string dockerName, JsonElement args, string argumentName)
    {
        var value = ToolArgumentReader.OptionalLong(args, argumentName);
        if (value is null)
        {
            return;
        }

        if (value.Value != 0 && value.Value < 1_000_000)
        {
            throw InvalidArgument($"Argument 'healthcheck.{argumentName}' must be 0 or at least 1000000.");
        }

        target[dockerName] = value.Value;
    }

    private static string NormalizeContainerPort(string value, string mapping)
    {
        var pieces = value.Split('/', StringSplitOptions.TrimEntries);
        if (pieces.Length > 2)
        {
            throw InvalidPortMapping(mapping);
        }

        if (!IsPort(pieces[0]))
        {
            throw InvalidPortMapping(mapping);
        }

        var protocol = pieces.Length == 2 ? pieces[1] : "tcp";
        if (!protocol.Equals("tcp", StringComparison.Ordinal) && !protocol.Equals("udp", StringComparison.Ordinal))
        {
            throw InvalidArgument($"Invalid port mapping '{mapping}'. Protocol must be tcp or udp.");
        }

        return $"{pieces[0]}/{protocol}";
    }

    private static string ValidateHostPort(string value)
    {
        if (!IsPort(value))
        {
            throw InvalidArgument($"Invalid host port '{value}'. Host port must be between 1 and 65535.");
        }

        return value;
    }

    private static string ValidateRestartPolicy(string value)
    {
        if (!RestartPolicies.Contains(value, StringComparer.Ordinal))
        {
            throw InvalidArgument("Argument 'restartPolicy' must be one of: no, always, unless-stopped, on-failure.");
        }

        return value;
    }

    private static bool IsPort(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is >= 1 and <= 65535;

    private static ContainerMcpException InvalidPortMapping(string mapping) =>
        InvalidArgument($"Invalid port mapping '{mapping}'. Container port must be between 1 and 65535.");

    private static ContainerMcpException InvalidArgument(string message) =>
        new(McpErrorCode.InvalidArgument, message, StatusCodes.Status400BadRequest);
}
