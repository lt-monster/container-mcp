using System.Text.Json.Nodes;
using ContainerMcp.Models;

namespace ContainerMcp.Tools;

internal static class ImageToolRequests
{
    private const int DefaultMaxEvents = 500;
    private const int HardMaxEvents = 2000;
    private const long DefaultMaxBytes = 1024L * 1024 * 1024;
    private const long HardMaxBytes = 8L * 1024 * 1024 * 1024;

    public static string BuildInspectPath(string image) =>
        $"/images/{Escape(image)}/json";

    public static string BuildTagPath(string source, string repo, string? tag)
    {
        var query = new List<string> { "repo=" + Escape(repo) };
        if (!string.IsNullOrWhiteSpace(tag))
        {
            query.Add("tag=" + Escape(tag));
        }

        return $"/images/{Escape(source)}/tag?{string.Join('&', query)}";
    }

    public static string BuildPrunePath(JsonElement args)
    {
        var filters = new JsonObject();
        if (args.TryGetProperty("dangling", out var dangling) && dangling.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            filters["dangling"] = JsonNodeExtensions.Array(JsonValue.Create(dangling.GetBoolean().ToString().ToLowerInvariant()));
        }

        if (ToolArgumentReader.OptionalString(args, "until") is { } until)
        {
            filters["until"] = JsonNodeExtensions.Array(JsonValue.Create(until));
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
            ? "/images/prune"
            : "/images/prune?filters=" + Escape(filters.ToCompactJson());
    }

    public static string BuildBuildPath(JsonElement args)
    {
        var query = new List<string>
        {
            "t=" + Escape(ToolArgumentReader.RequireString(args, "tag")),
            "rm=" + BoolQuery(!args.TryGetProperty("removeIntermediate", out var rm) || rm.ValueKind != JsonValueKind.False)
        };

        AddOptionalString(query, "dockerfile", ToolArgumentReader.OptionalString(args, "dockerfile"));
        AddOptionalBool(query, "nocache", args, "noCache");
        AddOptionalBool(query, "pull", args, "pull");
        AddOptionalBool(query, "forcerm", args, "forceRemoveIntermediate");

        return "/build?" + string.Join('&', query);
    }

    public static string BuildPushPath(string image, string? tag)
    {
        var path = $"/images/{Escape(image)}/push";
        return string.IsNullOrWhiteSpace(tag) ? path : path + "?tag=" + Escape(tag);
    }

    public static string BuildLoadPath(bool quiet) =>
        "/images/load?quiet=" + BoolQuery(quiet);

    public static string BuildSavePath(string image) =>
        $"/images/{Escape(image)}/get";

    public static int NormalizeMaxEvents(int? maxEvents) =>
        Math.Clamp(maxEvents ?? DefaultMaxEvents, 1, HardMaxEvents);

    public static long NormalizeMaxBytes(long? maxBytes) =>
        Math.Clamp(maxBytes ?? DefaultMaxBytes, 1, HardMaxBytes);

    private static void AddOptionalString(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"{name}={Escape(value)}");
        }
    }

    private static void AddOptionalBool(List<string> query, string dockerName, JsonElement args, string argumentName)
    {
        if (args.TryGetProperty(argumentName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            query.Add($"{dockerName}={BoolQuery(value.GetBoolean())}");
        }
    }

    private static string BoolQuery(bool value) =>
        value.ToString().ToLowerInvariant();

    private static JsonArray StringArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.AddNode(JsonValue.Create(value));
        }

        return array;
    }

    private static string Escape(string value) =>
        Uri.EscapeDataString(value);
}
