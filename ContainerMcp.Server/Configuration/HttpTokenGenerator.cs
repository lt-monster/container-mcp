using ContainerMcp.Models;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ContainerMcp.Configuration;

internal sealed record HttpTokenGenerateOptions(
    string ConfigPath,
    int Count,
    string? Id,
    string? Description);

internal sealed record HttpTokenGenerateResult(
    string ConfigPath,
    IReadOnlyList<string> Tokens,
    int TotalTokenCount);

internal static class HttpTokenGenerator
{
    public static HttpTokenGenerateResult Generate(HttpTokenGenerateOptions options)
    {
        var count = Math.Clamp(options.Count, 1, 100);
        var configPath = Path.GetFullPath(options.ConfigPath);
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        var root = LoadOrCreate(configPath);
        var http = EnsureObject(root, "http");
        var tokens = EnsureArray(http, "tokens");
        var existing = tokens
            .Select(node => node?["value"]?.GetValue<string>())
            .Where(value => value is not null)
            .ToHashSet(StringComparer.Ordinal);
        var generated = new List<string>();
        for (var i = 0; i < count; i++)
        {
            string value;
            do
            {
                value = GenerateTokenValue();
            }
            while (!existing.Add(value));

            generated.Add(value);
            tokens.Add(new JsonObject
            {
                ["id"] = TokenId(options.Id, i, count),
                ["value"] = value,
                ["enabled"] = true,
                ["createdAt"] = DateTimeOffset.UtcNow.ToString("O"),
                ["description"] = options.Description
            });
        }

        File.WriteAllText(configPath, root.ToJsonString(new JsonSerializerOptions(JsonDefaults.Options) { WriteIndented = true }));
        return new HttpTokenGenerateResult(configPath, generated, tokens.Count);
    }

    public static string DefaultConfigPath(string appBaseDirectory) =>
        Path.Combine(appBaseDirectory, ContainerMcpConfigurationLoader.DefaultConfigurationFileName);

    private static JsonObject LoadOrCreate(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return new JsonObject
            {
                ["version"] = 1,
                ["transport"] = "http",
                ["urls"] = "http://127.0.0.1:7010"
            };
        }

        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        return JsonNode.Parse(document.RootElement.GetRawText()) as JsonObject
            ?? throw new InvalidOperationException("Configuration root must be a JSON object.");
    }

    private static JsonObject EnsureObject(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        parent[propertyName] = created;
        return created;
    }

    private static JsonArray EnsureArray(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonArray existing)
        {
            return existing;
        }

        var created = new JsonArray();
        parent[propertyName] = created;
        return created;
    }

    private static string GenerateTokenValue()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return "cmcp_" + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string TokenId(string? id, int index, int count)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return count == 1 ? "default" : $"default-{index + 1}";
        }

        return count == 1 ? id : $"{id}-{index + 1}";
    }
}
