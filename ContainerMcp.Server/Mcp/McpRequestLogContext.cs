namespace ContainerMcp.Mcp;

internal sealed record McpRequestLogContext(
    string Remote,
    string? TokenId)
{
    public static McpRequestLogContext Stdio { get; } = new("stdio", null);
}
