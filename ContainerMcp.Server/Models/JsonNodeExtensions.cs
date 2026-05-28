using System.Text;
using System.Text.Json.Nodes;

namespace ContainerMcp.Models;

internal static class JsonNodeExtensions
{
    public static string ToCompactJson(this JsonNode? node) =>
        node?.ToJsonString(JsonDefaults.Options) ?? "null";

    public static JsonNode? CloneNode(this JsonNode? node) =>
        node?.DeepClone();

    public static JsonNode? ToJsonNode(this JsonElement element)
    {
        return JsonNode.Parse(element.GetRawText());
    }

    public static JsonObject Object(params (string Name, JsonNode? Value)[] properties)
    {
        var obj = new JsonObject();
        foreach (var (name, value) in properties)
        {
            obj[name] = value;
        }

        return obj;
    }

    public static JsonArray Array(params JsonNode?[] items)
    {
        var array = new JsonArray();
        foreach (var item in items)
        {
            array.AddNode(item);
        }

        return array;
    }

    public static void AddNode(this JsonArray array, JsonNode? item) =>
        ((IList<JsonNode?>)array).Add(item);

    public static IResult JsonResult(JsonNode node, int? statusCode = null) =>
        Results.Text(node.ToCompactJson(), "application/json", Encoding.UTF8, statusCode);

    public static JsonObject StringMapNode(IReadOnlyDictionary<string, string> values)
    {
        var obj = new JsonObject();
        foreach (var pair in values)
        {
            obj[pair.Key] = pair.Value;
        }

        return obj;
    }
}
