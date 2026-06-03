using ContainerMcp.Configuration;
using System.Net;

namespace ContainerMcp;

internal static class ProgramSupport
{
    public const long MaxMcpHttpRequestBodyBytes = 1024 * 1024;

    public static bool HasNonLoopbackBinding(string urls)
    {
        foreach (var rawUrl in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Uri.TryCreate(rawUrl.Replace("*", "0.0.0.0").Replace("+", "0.0.0.0"), UriKind.Absolute, out var uri))
            {
                continue;
            }

            var host = uri.Host.Trim('[', ']');
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address)))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    public static string BuildNonLoopbackWarning(ContainerMcpOptions options) =>
        $"Warning: container-mcp HTTP transport is bound to a non-loopback address ({options.Urls}). " +
        "Only expose this endpoint on trusted local networks and protect /mcp with bearer tokens.";
}
