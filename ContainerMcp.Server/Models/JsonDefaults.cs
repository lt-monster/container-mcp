using System.Text.Json.Nodes;

namespace ContainerMcp.Models;

internal static class JsonDefaults
{
    public static JsonSerializerOptions Options => ContainerMcpJsonContext.Default.Options;
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
[JsonSerializable(typeof(JsonNode))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(JsonArray))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(bool))]
internal partial class ContainerMcpJsonContext : JsonSerializerContext;
