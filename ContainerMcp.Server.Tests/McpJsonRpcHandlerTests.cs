using System.Text.Json;
using System.Text.Json.Nodes;
using ContainerMcp.Configuration;
using ContainerMcp.Mcp;

namespace ContainerMcp.Server.Tests;

public sealed class McpJsonRpcHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsNoResponseForNotificationWithoutId()
    {
        var handler = new McpJsonRpcHandler(new EmptyToolRegistry(), ContainerMcpOptions.From(["--transport", "stdio"]));
        using var document = JsonDocument.Parse(
            """{"jsonrpc":"2.0","method":"notifications/initialized","params":{}}""");

        var response = await handler.HandleAsync(document.RootElement, CancellationToken.None);

        Assert.Null(response);
    }

    private sealed class EmptyToolRegistry : IMcpToolRegistry
    {
        public JsonArray List() => [];

        public Task<JsonObject> CallAsync(string name, JsonElement arguments, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
