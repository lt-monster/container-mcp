using System.Security.Cryptography;
using System.Text;

namespace ContainerMcp.Configuration;

internal static class HttpTokenValidator
{
    private static readonly string[] WeakValues = ["changeme", "password", "token", "secret"];

    public static IReadOnlyList<HttpToken> ValidTokens(IReadOnlyList<HttpTokenConfiguration> tokens)
    {
        var result = new List<HttpToken>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            if (token.Enabled == false)
            {
                continue;
            }

            if (!IsValidValue(token.Value))
            {
                var id = string.IsNullOrWhiteSpace(token.Id) ? "<unnamed>" : token.Id;
                throw new InvalidOperationException($"Invalid HTTP bearer token in configuration: {id}.");
            }

            var value = token.Value!;
            if (!seen.Add(value))
            {
                continue;
            }

            result.Add(new HttpToken(
                token.Id ?? string.Empty,
                value,
                Enabled: true,
                token.CreatedAt,
                token.Description));
        }

        return result;
    }

    public static void ValidateForStartup(ContainerMcpOptions options)
    {
        if (options.Transport != TransportMode.Http)
        {
            return;
        }

        if (ProgramSupport.HasNonLoopbackBinding(options.Urls) && options.HttpTokens.Count == 0)
        {
            throw new InvalidOperationException(
                "HTTP transport bound to a non-loopback address requires at least one HTTP bearer token.");
        }
    }

    public static bool IsAuthorized(ContainerMcpOptions options, string? authorizationHeader)
    {
        if (options.HttpTokens.Count == 0)
        {
            return true;
        }

        const string bearerPrefix = "Bearer ";
        if (authorizationHeader is null
            || !authorizationHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var provided = authorizationHeader[bearerPrefix.Length..].Trim();
        if (!IsValidValue(provided))
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(provided);
        foreach (var token in options.HttpTokens)
        {
            if (!token.Enabled)
            {
                continue;
            }

            var expectedBytes = Encoding.UTF8.GetBytes(token.Value);
            if (providedBytes.Length == expectedBytes.Length
                && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsValidValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 40 || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        return !WeakValues.Any(weak => value.Equals(weak, StringComparison.OrdinalIgnoreCase));
    }
}
