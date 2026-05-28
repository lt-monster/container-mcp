using System.Text.Json.Nodes;
using ContainerMcp.ContainerRuntime;
using ContainerMcp.Models;

namespace ContainerMcp.Tools;

internal sealed class ContainerCreateRequestBuilder
{
    private readonly VolumePolicy _volumePolicy;

    public ContainerCreateRequestBuilder(VolumePolicy volumePolicy) => _volumePolicy = volumePolicy;

    public JsonObject Build(JsonElement args, string image)
    {
        var body = new JsonObject
        {
            ["Image"] = image
        };

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

        var hostConfig = new JsonObject();
        if (ToolArgumentReader.OptionalString(args, "restartPolicy") is { } restartPolicy)
        {
            hostConfig["RestartPolicy"] = new JsonObject { ["Name"] = restartPolicy };
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
                var containerPort = NormalizeContainerPort(property.Name);
                var hostPort = property.Value.ValueKind == JsonValueKind.Number
                    ? property.Value.GetInt32().ToString(CultureInfo.InvariantCulture)
                    : property.Value.GetString();
                result[containerPort] = JsonNodeExtensions.Array(new JsonObject { ["HostPort"] = hostPort });
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
            var port = NormalizeContainerPort(parts[0]);
            result[port] = JsonNodeExtensions.Array(new JsonObject());
            return;
        }

        if (parts.Length == 2)
        {
            var containerPort = NormalizeContainerPort(parts[1]);
            result[containerPort] = JsonNodeExtensions.Array(new JsonObject { ["HostPort"] = parts[0] });
            return;
        }

        if (parts.Length == 3)
        {
            var containerPort = NormalizeContainerPort(parts[2]);
            result[containerPort] = JsonNodeExtensions.Array(new JsonObject { ["HostIp"] = parts[0], ["HostPort"] = parts[1] });
        }
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

    private static string NormalizeContainerPort(string value) =>
        value.Contains('/', StringComparison.Ordinal) ? value : $"{value}/tcp";
}
