using System.Text.Json;
using System.Text.Json.Nodes;
using ContainerMcp.Configuration;
using ContainerMcp.Mcp;
using ContainerMcp.Models;
using Microsoft.AspNetCore.Http;

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

    [Theory]
    [InlineData(McpErrorCode.InvalidArgument, -32602)]
    [InlineData(McpErrorCode.UnsupportedTarget, -32602)]
    [InlineData(McpErrorCode.UnsupportedVolumeMount, -32602)]
    [InlineData(McpErrorCode.EngineNotFound, -32001)]
    [InlineData(McpErrorCode.EngineUnavailable, -32002)]
    [InlineData(McpErrorCode.ApiUnavailable, -32003)]
    [InlineData(McpErrorCode.ContainerNotFound, -32004)]
    [InlineData(McpErrorCode.ImageNotFound, -32005)]
    [InlineData(McpErrorCode.VolumeNotFound, -32006)]
    [InlineData(McpErrorCode.PortRangeExhausted, -32007)]
    [InlineData(McpErrorCode.OperationFailed, -32000)]
    public async Task HandleAsync_MapsContainerErrorsToPreciseJsonRpcCodes(string errorCode, int expectedJsonRpcCode)
    {
        var handler = new McpJsonRpcHandler(new ThrowingToolRegistry(errorCode), ContainerMcpOptions.From([]));
        using var document = JsonDocument.Parse(
            """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"test_tool","arguments":{}}}""");

        var response = await handler.HandleAsync(document.RootElement, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(expectedJsonRpcCode, response["error"]!["code"]!.GetValue<int>());
        Assert.Equal(errorCode, response["error"]!["data"]!["errorCode"]!.GetValue<string>());
    }

    private sealed class EmptyToolRegistry : IMcpToolRegistry
    {
        public JsonArray List() => [];

        public Task<JsonObject> CallAsync(string name, JsonElement arguments, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingToolRegistry(string errorCode) : IMcpToolRegistry
    {
        public JsonArray List() => [];

        public Task<JsonObject> CallAsync(string name, JsonElement arguments, CancellationToken cancellationToken) =>
            throw new ContainerMcpException(errorCode, "Tool failed.", StatusCodes.Status400BadRequest);
    }
}
