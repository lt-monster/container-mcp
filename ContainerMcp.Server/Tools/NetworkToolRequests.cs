using System.Text.Json.Nodes;
using ContainerMcp.Models;

namespace ContainerMcp.Tools;

internal static class NetworkToolRequests
{
    public static string BuildInspectPath(string idOrName) =>
        $"/networks/{Uri.EscapeDataString(idOrName)}";

    public static string BuildRemovePath(string idOrName) =>
        $"/networks/{Uri.EscapeDataString(idOrName)}";

    public static string BuildConnectPath(string network) =>
        $"/networks/{Uri.EscapeDataString(network)}/connect";

    public static string BuildDisconnectPath(string network) =>
        $"/networks/{Uri.EscapeDataString(network)}/disconnect";

    public static JsonObject BuildCreateBody(JsonElement args, string name)
    {
        var body = new JsonObject { ["Name"] = name };

        if (ToolArgumentReader.OptionalString(args, "driver") is { } driver)
        {
            body["Driver"] = driver;
        }

        AddOptionalBool(body, "Internal", args, "internal");
        AddOptionalBool(body, "Attachable", args, "attachable");
        AddOptionalBool(body, "EnableIPv6", args, "enableIPv6");

        if (ToolArgumentReader.OptionalStringDictionary(args, "options") is { Count: > 0 } options)
        {
            body["Options"] = JsonNodeExtensions.StringMapNode(options);
        }

        if (ToolArgumentReader.OptionalStringDictionary(args, "labels") is { Count: > 0 } labels)
        {
            body["Labels"] = JsonNodeExtensions.StringMapNode(labels);
        }

        return body;
    }

    public static JsonObject BuildConnectBody(JsonElement args, string container)
    {
        var body = new JsonObject { ["Container"] = container };
        var endpointConfig = new JsonObject();

        if (ToolArgumentReader.OptionalStringArray(args, "aliases") is { Length: > 0 } aliases)
        {
            endpointConfig["Aliases"] = StringArray(aliases);
        }

        var ipamConfig = new JsonObject();
        if (ToolArgumentReader.OptionalString(args, "ipv4Address") is { } ipv4Address)
        {
            ipamConfig["IPv4Address"] = ipv4Address;
        }

        if (ToolArgumentReader.OptionalString(args, "ipv6Address") is { } ipv6Address)
        {
            ipamConfig["IPv6Address"] = ipv6Address;
        }

        if (ipamConfig.Count > 0)
        {
            endpointConfig["IPAMConfig"] = ipamConfig;
        }

        if (endpointConfig.Count > 0)
        {
            body["EndpointConfig"] = endpointConfig;
        }

        return body;
    }

    public static JsonObject BuildDisconnectBody(JsonElement args, string container) => new()
    {
        ["Container"] = container,
        ["Force"] = ToolArgumentReader.OptionalBool(args, "force")
    };

    public static string BuildPrunePath(JsonElement args)
    {
        var filters = new JsonObject();
        if (ToolArgumentReader.OptionalString(args, "until") is { } until)
        {
            filters["until"] = StringArray([until]);
        }

        if (ToolArgumentReader.OptionalStringArray(args, "labels") is { Length: > 0 } labels)
        {
            filters["label"] = StringArray(labels);
        }

        if (ToolArgumentReader.OptionalStringArray(args, "labelNe") is { Length: > 0 } labelNe)
        {
            filters["label!"] = StringArray(labelNe);
        }

        return filters.Count == 0
            ? "/networks/prune"
            : "/networks/prune?filters=" + Uri.EscapeDataString(filters.ToCompactJson());
    }

    private static void AddOptionalBool(JsonObject body, string dockerName, JsonElement args, string argumentName)
    {
        if (args.TryGetProperty(argumentName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            body[dockerName] = value.GetBoolean();
        }
    }

    private static JsonArray StringArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.AddNode(JsonValue.Create(value));
        }

        return array;
    }
}
