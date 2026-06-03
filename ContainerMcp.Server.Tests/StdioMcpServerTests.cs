using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ContainerMcp.Configuration;
using ContainerMcp.Mcp;

namespace ContainerMcp.Server.Tests;

public sealed class StdioMcpServerTests
{
    [Fact]
    public async Task RunAsync_WritesOnlyJsonRpcResponsesToOutput()
    {
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(
            """
            {"jsonrpc":"2.0","method":"notifications/initialized"}
            {"jsonrpc":"2.0","id":1,"method":"ping"}
            
            """.Replace("\r\n", "\n")));
        await using var output = new MemoryStream();
        var server = new StdioMcpServer(new McpJsonRpcHandler(new EmptyToolRegistry(), ContainerMcpOptions.From(["--transport", "stdio"])));

        await server.RunAsync(input, output, CancellationToken.None);

        output.Position = 0;
        var text = Encoding.UTF8.GetString(output.ToArray());
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        using var document = JsonDocument.Parse(lines[0]);
        Assert.Equal(1, document.RootElement.GetProperty("id").GetInt64());
        Assert.DoesNotContain("container-mcp started", text);
    }

    private sealed class EmptyToolRegistry : IMcpToolRegistry
    {
        public JsonArray List() => [];

        public Task<JsonObject> CallAsync(string name, JsonElement arguments, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
