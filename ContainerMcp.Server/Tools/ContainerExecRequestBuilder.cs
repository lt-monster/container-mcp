using System.Text.Json.Nodes;
using ContainerMcp.Models;

namespace ContainerMcp.Tools;

internal static class ContainerExecRequestBuilder
{
    private const int DefaultMaxBytes = 1024 * 1024;
    private const int HardMaxBytes = 4 * 1024 * 1024;

    public static string BuildCreatePath(string idOrName) =>
        $"/containers/{Uri.EscapeDataString(idOrName)}/exec";

    public static string BuildStartPath(string execId) =>
        $"/exec/{Uri.EscapeDataString(execId)}/start";

    public static JsonObject BuildCreateBody(JsonElement args)
    {
        var body = new JsonObject
        {
            ["AttachStdin"] = false,
            ["AttachStdout"] = OptionalBool(args, "attachStdout", defaultValue: true),
            ["AttachStderr"] = OptionalBool(args, "attachStderr", defaultValue: true),
            ["Tty"] = OptionalBool(args, "tty", defaultValue: false),
            ["Cmd"] = StringArrayNode(ToolArgumentReader.OptionalStringArray(args, "command") ?? [])
        };

        if (ToolArgumentReader.OptionalStringDictionary(args, "env") is { Count: > 0 } env)
        {
            body["Env"] = StringArrayNode(env.Select(pair => $"{pair.Key}={pair.Value}"));
        }

        if (ToolArgumentReader.OptionalString(args, "user") is { } user)
        {
            body["User"] = user;
        }

        if (ToolArgumentReader.OptionalString(args, "workingDir") is { } workingDir)
        {
            body["WorkingDir"] = workingDir;
        }

        return body;
    }

    public static JsonObject BuildStartBody(bool tty) => new()
    {
        ["Detach"] = false,
        ["Tty"] = tty
    };

    public static int NormalizeMaxBytes(int? maxBytes) =>
        Math.Clamp(maxBytes ?? DefaultMaxBytes, 1, HardMaxBytes);

    private static bool OptionalBool(JsonElement args, string name, bool defaultValue)
    {
        if (!args.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return defaultValue;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => defaultValue
        };
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
}
