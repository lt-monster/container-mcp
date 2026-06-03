using System.Reflection;

namespace ContainerMcp.Mcp;

internal static class ServerVersion
{
    public static string Current { get; } = Normalize(
        typeof(ServerVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? typeof(ServerVersion).Assembly.GetName().Version?.ToString(3)
        ?? "1.0.0");

    private static string Normalize(string value)
    {
        var metadataIndex = value.IndexOf('+', StringComparison.Ordinal);
        return metadataIndex < 0 ? value : value[..metadataIndex];
    }
}
