using ContainerMcp.Models;
using System.Text.Json.Nodes;

namespace ContainerMcp.Mcp;

internal static class McpInputSchemaValidator
{
    public static void Validate(JsonElement arguments, JsonObject schema)
    {
        if (!TryValidate(arguments, schema, path: null, out var error))
        {
            throw new ContainerMcpException(McpErrorCode.InvalidArgument, error, StatusCodes.Status400BadRequest);
        }
    }

    private static bool TryValidate(JsonElement value, JsonObject schema, string? path, out string error)
    {
        if (schema.TryGetPropertyValue("oneOf", out var oneOfNode) && oneOfNode is JsonArray oneOf)
        {
            foreach (var option in oneOf.OfType<JsonObject>())
            {
                if (TryValidate(value, option, path, out _))
                {
                    error = string.Empty;
                    return true;
                }
            }

            error = $"{Argument(path)} does not match any allowed schema.";
            return false;
        }

        if (ReadString(schema, "type") is { } type && !ValidateType(value, type, path, out error))
        {
            return false;
        }

        if (schema.TryGetPropertyValue("enum", out var enumNode) && enumNode is JsonArray enumValues)
        {
            if (!ValidateEnum(value, enumValues, path, out error))
            {
                return false;
            }
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            return ValidateObject(value, schema, path, out error);
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            return ValidateArray(value, schema, path, out error);
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateObject(JsonElement value, JsonObject schema, string? path, out string error)
    {
        var properties = schema.TryGetPropertyValue("properties", out var propertiesNode) && propertiesNode is JsonObject propertiesObject
            ? propertiesObject
            : null;

        if (schema.TryGetPropertyValue("required", out var requiredNode) && requiredNode is JsonArray required)
        {
            foreach (var item in required)
            {
                if (item is not JsonValue jsonValue || !jsonValue.TryGetValue<string>(out var name))
                {
                    continue;
                }

                if (!value.TryGetProperty(name, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    error = $"Missing required argument '{Join(path, name)}'.";
                    return false;
                }
            }
        }

        var additionalProperties = schema.TryGetPropertyValue("additionalProperties", out var additionalNode)
            ? additionalNode
            : null;

        foreach (var property in value.EnumerateObject())
        {
            var propertyPath = Join(path, property.Name);
            if (properties is not null && properties.TryGetPropertyValue(property.Name, out var propertySchema) && propertySchema is JsonObject propertySchemaObject)
            {
                if (!TryValidate(property.Value, propertySchemaObject, propertyPath, out error))
                {
                    return false;
                }

                continue;
            }

            if (additionalProperties is JsonValue additionalValue
                && additionalValue.TryGetValue<bool>(out var allowAdditional)
                && !allowAdditional)
            {
                error = $"Unknown argument '{propertyPath}'.";
                return false;
            }

            if (additionalProperties is JsonObject additionalSchema
                && !TryValidate(property.Value, additionalSchema, propertyPath, out error))
            {
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateArray(JsonElement value, JsonObject schema, string? path, out string error)
    {
        if (!schema.TryGetPropertyValue("items", out var itemsNode) || itemsNode is not JsonObject itemsSchema)
        {
            error = string.Empty;
            return true;
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (!TryValidate(item, itemsSchema, $"{path ?? "argument"}[{index}]", out error))
            {
                return false;
            }

            index++;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateType(JsonElement value, string type, string? path, out string error)
    {
        var valid = type switch
        {
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "string" => value.ValueKind == JsonValueKind.String,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            _ => true
        };

        if (valid)
        {
            error = string.Empty;
            return true;
        }

        error = $"{Argument(path)} must be {Article(type)} {type}.";
        return false;
    }

    private static bool ValidateEnum(JsonElement value, JsonArray enumValues, string? path, out string error)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            error = $"{Argument(path)} must be a string.";
            return false;
        }

        var text = value.GetString();
        var allowed = enumValues
            .OfType<JsonValue>()
            .Select(item => item.TryGetValue<string>(out var allowedValue) ? allowedValue : null)
            .Where(item => item is not null)
            .Cast<string>()
            .ToArray();

        if (allowed.Contains(text, StringComparer.Ordinal))
        {
            error = string.Empty;
            return true;
        }

        error = $"{Argument(path)} must be one of: {string.Join(", ", allowed)}.";
        return false;
    }

    private static string? ReadString(JsonObject obj, string name) =>
        obj.TryGetPropertyValue(name, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;

    private static string Argument(string? path) =>
        path is null ? "Arguments" : $"Argument '{path}'";

    private static string Join(string? path, string name) =>
        path is null ? name : $"{path}.{name}";

    private static string Article(string type) =>
        type is "integer" or "object" or "array" ? "an" : "a";
}
