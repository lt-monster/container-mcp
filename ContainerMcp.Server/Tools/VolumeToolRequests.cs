using System.Text.Json.Nodes;
using ContainerMcp.Models;

namespace ContainerMcp.Tools;

internal static class VolumeToolRequests
{
    public static string BuildInspectPath(string name) =>
        $"/volumes/{Uri.EscapeDataString(name)}";

    public static string BuildPrunePath(JsonElement args)
    {
        var filters = new JsonObject();
        if (ToolArgumentReader.OptionalStringArray(args, "labels") is { Length: > 0 } labels)
        {
            filters["label"] = StringArray(labels);
        }

        if (ToolArgumentReader.OptionalStringArray(args, "labelNe") is { Length: > 0 } labelNe)
        {
            filters["label!"] = StringArray(labelNe);
        }

        return filters.Count == 0
            ? "/volumes/prune"
            : "/volumes/prune?filters=" + Uri.EscapeDataString(filters.ToCompactJson());
    }

    public static JsonObject BuildCreateBody(JsonElement args, string name)
    {
        var body = new JsonObject { ["Name"] = name };

        if (ToolArgumentReader.OptionalString(args, "driver") is { } driver)
        {
            body["Driver"] = driver;
        }

        if (ToolArgumentReader.OptionalStringDictionary(args, "driverOptions") is { Count: > 0 } driverOptions)
        {
            body["DriverOpts"] = JsonNodeExtensions.StringMapNode(driverOptions);
        }

        if (ToolArgumentReader.OptionalStringDictionary(args, "labels") is { Count: > 0 } labels)
        {
            body["Labels"] = JsonNodeExtensions.StringMapNode(labels);
        }

        return body;
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
