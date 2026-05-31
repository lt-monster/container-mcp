using System.Text.Json.Nodes;
using ContainerMcp.Models;

namespace ContainerMcp.ContainerRuntime;

internal static class DockerJsonMessageStream
{
    public static JsonObject Parse(string text, int maxEvents)
    {
        using var reader = new StringReader(text);
        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lines.Add(line);
        }

        return ParseLines(lines, maxEvents);
    }

    public static async Task<JsonObject> ParseAsync(Stream stream, int maxEvents, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            lines.Add(line);
        }

        return ParseLines(lines, maxEvents);
    }

    private static JsonObject ParseLines(IEnumerable<string> lines, int maxEvents)
    {
        var events = new JsonArray();
        var eventCount = 0;
        var truncated = false;
        string? lastStatus = null;
        string? lastError = null;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            eventCount++;
            JsonNode? node;
            try
            {
                node = JsonNode.Parse(line);
            }
            catch (JsonException)
            {
                node = new JsonObject { ["raw"] = line };
            }

            if (node is JsonObject obj)
            {
                lastStatus = ReadString(obj, "status") ?? lastStatus;
                lastError = ReadString(obj, "error") ?? lastError;
            }

            if (events.Count < maxEvents)
            {
                events.AddNode(node);
            }
            else
            {
                truncated = true;
            }
        }

        return new JsonObject
        {
            ["eventCount"] = eventCount,
            ["truncated"] = truncated,
            ["lastStatus"] = lastStatus,
            ["lastError"] = lastError,
            ["events"] = events
        };
    }

    private static string? ReadString(JsonObject obj, string name) =>
        obj.TryGetPropertyValue(name, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;
}
