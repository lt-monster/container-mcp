using ContainerMcp.Configuration;
using ContainerMcp.Models;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace ContainerMcp.Mcp;

internal static class McpToolLogger
{
    public static long Timestamp() => Stopwatch.GetTimestamp();

    public static void Start(string requestId, string tool, JsonElement arguments, McpRequestLogContext context)
    {
        Console.Error.WriteLine(
            $"info: mcp tool start requestId={Escape(requestId)} tool={Escape(tool)}{CommonFields(tool, arguments)} remote={Escape(context.Remote)}{TokenField(context.TokenId)}");
    }

    public static void Success(string requestId, string tool, JsonElement arguments, JsonObject result, long startTimestamp)
    {
        Console.Error.WriteLine(
            $"info: mcp tool success requestId={Escape(requestId)} tool={Escape(tool)}{CommonFields(tool, arguments, result)} status=ok durationMs={ElapsedMilliseconds(startTimestamp)}");
    }

    public static void Error(string requestId, string tool, JsonElement arguments, ContainerMcpException exception, long startTimestamp)
    {
        Console.Error.WriteLine(
            $"error: mcp tool error requestId={Escape(requestId)} tool={Escape(tool)}{CommonFields(tool, arguments)} errorCode={LogErrorCode(exception.ErrorCode)} statusCode={exception.StatusCode} durationMs={ElapsedMilliseconds(startTimestamp)} message=\"{EscapeMessage(exception.Message)}\"");
    }

    public static void Error(string requestId, string tool, JsonElement arguments, Exception exception, long startTimestamp)
    {
        Console.Error.WriteLine(
            $"error: mcp tool error requestId={Escape(requestId)} tool={Escape(tool)}{CommonFields(tool, arguments)} errorCode={LogErrorCode(McpErrorCode.OperationFailed)} statusCode=500 durationMs={ElapsedMilliseconds(startTimestamp)} message=\"{EscapeMessage(exception.Message)}\"");
    }

    private static string CommonFields(string tool, JsonElement arguments, JsonObject? result = null)
    {
        var engine = ReadString(arguments, "engine") ?? ReadString(result, "engine");
        var target = ReadString(arguments, "target") ?? ReadString(result, "target");
        var resource = Resource(tool, arguments);
        var timeout = ReadInt(arguments, "timeoutSeconds");
        return $"{Optional("engine", engine)}{Optional("target", target)}{Optional("resourceType", resource.Type)}{Optional("resourceId", resource.Id)}{Optional("timeoutSeconds", timeout?.ToString(CultureInfo.InvariantCulture))}";
    }

    private static (string? Type, string? Id) Resource(string tool, JsonElement arguments)
    {
        if (tool.StartsWith("container_", StringComparison.Ordinal))
        {
            return ("container", Short(ReadString(arguments, "idOrName") ?? ReadString(arguments, "container")));
        }

        if (tool.StartsWith("image_", StringComparison.Ordinal))
        {
            return ("image", Short(ReadString(arguments, "imageIdOrName") ?? ReadString(arguments, "image") ?? ReadString(arguments, "source")));
        }

        if (tool.StartsWith("volume_", StringComparison.Ordinal))
        {
            return ("volume", Short(ReadString(arguments, "name")));
        }

        if (tool.StartsWith("network_", StringComparison.Ordinal))
        {
            return ("network", Short(ReadString(arguments, "idOrName") ?? ReadString(arguments, "network") ?? ReadString(arguments, "name")));
        }

        return (null, null);
    }

    private static string? ReadString(JsonElement arguments, string propertyName) =>
        arguments.ValueKind == JsonValueKind.Object
        && arguments.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string? ReadString(JsonObject? obj, string propertyName) =>
        obj?[propertyName]?.GetValue<string>();

    private static int? ReadInt(JsonElement arguments, string propertyName) =>
        arguments.ValueKind == JsonValueKind.Object
        && arguments.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.Number
        && property.TryGetInt32(out var value)
            ? value
            : null;

    private static string? Short(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= 12 ? value : value[..12];

    private static string Optional(string name, string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : $" {name}={Escape(value)}";

    private static string TokenField(string? tokenId) =>
        string.IsNullOrWhiteSpace(tokenId) ? string.Empty : $" tokenId={Escape(tokenId)}";

    private static long ElapsedMilliseconds(long startTimestamp) =>
        Stopwatch.GetElapsedTime(startTimestamp).Ticks / TimeSpan.TicksPerMillisecond;

    private static string Escape(string value) =>
        value.Replace("\r", string.Empty).Replace("\n", string.Empty).Replace(" ", "_");

    private static string EscapeMessage(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");

    private static string LogErrorCode(string value) =>
        Escape(value.ToLowerInvariant());
}
