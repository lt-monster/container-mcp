using ContainerMcp.Models;

namespace ContainerMcp.Tools;

internal static class ToolArgumentReader
{
    public static string RequireString(JsonElement args, string name)
    {
        var value = OptionalString(args, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ContainerMcpException(McpErrorCode.InvalidArgument, $"Missing required argument '{name}'.", StatusCodes.Status400BadRequest);
        }

        return value;
    }

    public static string? OptionalString(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    public static bool OptionalBool(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => property.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true,
            _ => false
        };
    }

    public static int? OptionalInt(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
        {
            return value;
        }

        return int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    public static string[]? OptionalStringArray(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return [property.GetString()!];
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToArray();
    }

    public static Dictionary<string, string>? OptionalStringDictionary(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return property.EnumerateObject()
            .ToDictionary(item => item.Name, item => item.Value.ValueKind == JsonValueKind.String ? item.Value.GetString() ?? string.Empty : item.Value.GetRawText());
    }
}
