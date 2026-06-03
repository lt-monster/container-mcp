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
    public async Task HandleMessageAsync_ReturnsBatchResponsesAndSkipsNotifications()
    {
        var handler = new McpJsonRpcHandler(new EmptyToolRegistry(), ContainerMcpOptions.From([]));
        using var document = JsonDocument.Parse(
            """
            [
              {"jsonrpc":"2.0","id":1,"method":"ping"},
              {"jsonrpc":"2.0","method":"notifications/initialized"},
              {"jsonrpc":"2.0","id":2,"method":"tools/list"}
            ]
            """);

        var response = await handler.HandleMessageAsync(document.RootElement, CancellationToken.None);

        Assert.NotNull(response);
        var array = Assert.IsType<JsonArray>(response);
        Assert.Equal(2, array.Count);
        Assert.Equal(1, array[0]!["id"]!.GetValue<long>());
        Assert.Equal(2, array[1]!["id"]!.GetValue<long>());
    }

    [Fact]
    public async Task HandleMessageAsync_ReturnsInvalidRequestForEmptyBatch()
    {
        var handler = new McpJsonRpcHandler(new EmptyToolRegistry(), ContainerMcpOptions.From([]));
        using var document = JsonDocument.Parse("[]");

        var response = await handler.HandleMessageAsync(document.RootElement, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(-32600, response["error"]!["code"]!.GetValue<int>());
    }

    [Fact]
    public async Task HandleMessageAsync_AcceptsJsonRpcResponseWithoutOutput()
    {
        var handler = new McpJsonRpcHandler(new EmptyToolRegistry(), ContainerMcpOptions.From([]));
        using var document = JsonDocument.Parse("""{"jsonrpc":"2.0","id":1,"result":{}}""");

        var response = await handler.HandleMessageAsync(document.RootElement, CancellationToken.None);

        Assert.Null(response);
    }

    [Fact]
    public async Task HandleMessageAsync_ReturnsInvalidRequestForScalarMessage()
    {
        var handler = new McpJsonRpcHandler(new EmptyToolRegistry(), ContainerMcpOptions.From([]));
        using var document = JsonDocument.Parse("true");

        var response = await handler.HandleMessageAsync(document.RootElement, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(-32600, response["error"]!["code"]!.GetValue<int>());
    }

    [Fact]
    public async Task HandleMessageAsync_ReturnsBatchErrorForInvalidItem()
    {
        var handler = new McpJsonRpcHandler(new EmptyToolRegistry(), ContainerMcpOptions.From([]));
        using var document = JsonDocument.Parse("""[true,{"jsonrpc":"2.0","id":1,"method":"ping"}]""");

        var response = await handler.HandleMessageAsync(document.RootElement, CancellationToken.None);

        var array = Assert.IsType<JsonArray>(response);
        Assert.Equal(2, array.Count);
        Assert.Equal(-32600, array[0]!["error"]!["code"]!.GetValue<int>());
        Assert.Equal(1, array[1]!["id"]!.GetValue<long>());
    }

    [Fact]
    public async Task HandleAsync_ReturnsNoResponseForNotificationWithoutId()
    {
        var handler = new McpJsonRpcHandler(new EmptyToolRegistry(), ContainerMcpOptions.From(["--transport", "stdio"]));
        using var document = JsonDocument.Parse(
            """{"jsonrpc":"2.0","method":"notifications/initialized","params":{}}""");

        var response = await handler.HandleAsync(document.RootElement, CancellationToken.None);

        Assert.Null(response);
    }

    [Fact]
    public async Task HandleAsync_ReturnsMethodNotFoundForUnknownMethod()
    {
        var handler = new McpJsonRpcHandler(new EmptyToolRegistry(), ContainerMcpOptions.From([]));
        using var document = JsonDocument.Parse("""{"jsonrpc":"2.0","id":"abc","method":"missing"}""");

        var response = await handler.HandleAsync(document.RootElement, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("abc", response["id"]!.GetValue<string>());
        Assert.Equal(-32601, response["error"]!["code"]!.GetValue<int>());
    }

    [Fact]
    public async Task HandleAsync_ReturnsInvalidParamsWhenToolNameIsMissing()
    {
        var handler = new McpJsonRpcHandler(new EmptyToolRegistry(), ContainerMcpOptions.From([]));
        using var document = JsonDocument.Parse("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"arguments":{}}}""");

        var response = await handler.HandleAsync(document.RootElement, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(-32602, response["error"]!["code"]!.GetValue<int>());
        Assert.Equal("tools/call requires a tool name.", response["error"]!["message"]!.GetValue<string>());
    }

    [Fact]
    public async Task HandleAsync_ReturnsTimeoutWhenToolExceedsToolTimeout()
    {
        var handler = new McpJsonRpcHandler(new SlowToolRegistry(), ContainerMcpOptions.From(["--tool-timeout-seconds", "1"]));
        using var document = JsonDocument.Parse("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"slow","arguments":{}}}""");

        var response = await handler.HandleAsync(document.RootElement, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(-32002, response["error"]!["code"]!.GetValue<int>());
        Assert.Contains("timed out", response["error"]!["message"]!.GetValue<string>());
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

    [Fact]
    public async Task HandleAsync_IncludesContainerErrorDetailsInData()
    {
        var details = new JsonObject
        {
            ["protocol"] = "tcp",
            ["scanned"] = 1
        };
        var handler = new McpJsonRpcHandler(new ThrowingToolRegistry(McpErrorCode.PortRangeExhausted, details), ContainerMcpOptions.From([]));
        using var document = JsonDocument.Parse(
            """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"test_tool","arguments":{}}}""");

        var response = await handler.HandleAsync(document.RootElement, CancellationToken.None);

        Assert.NotNull(response);
        var data = response["error"]!["data"]!;
        Assert.Equal("tcp", data["details"]!["protocol"]!.GetValue<string>());
        Assert.Equal(1, data["details"]!["scanned"]!.GetValue<int>());
    }

    [Fact]
    public async Task HandleAsync_LogsToolStartAndSuccessWithResourceMetadata()
    {
        var handler = new McpJsonRpcHandler(new ReturningToolRegistry(), ContainerMcpOptions.From([]));
        using var document = JsonDocument.Parse(
            """
            {"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"container_stop","arguments":{"idOrName":"f1537f3881bf3f72024a3e618eb62f2c555ad4e91bdddf6dcd162ed7688345f4","timeoutSeconds":10,"engine":"docker","target":"local"}}}
            """);
        using var capture = ConsoleErrorCapture.Start();

        await handler.HandleAsync(
            document.RootElement,
            CancellationToken.None,
            new McpRequestLogContext("127.0.0.1", "local-admin"));

        var log = capture.Text;
        Assert.Contains("info: mcp tool start", log);
        Assert.Contains("requestId=1", log);
        Assert.Contains("tool=container_stop", log);
        Assert.Contains("engine=docker", log);
        Assert.Contains("target=local", log);
        Assert.Contains("resourceType=container", log);
        Assert.Contains("resourceId=f1537f3881bf", log);
        Assert.Contains("timeoutSeconds=10", log);
        Assert.Contains("remote=127.0.0.1", log);
        Assert.Contains("tokenId=local-admin", log);
        Assert.Contains("info: mcp tool success", log);
        Assert.Contains("status=ok", log);
        Assert.Contains("durationMs=", log);
    }

    [Fact]
    public async Task HandleAsync_LogsToolErrorsWithMcpErrorCode()
    {
        var handler = new McpJsonRpcHandler(new ThrowingToolRegistry(McpErrorCode.ContainerNotFound), ContainerMcpOptions.From([]));
        using var document = JsonDocument.Parse(
            """{"jsonrpc":"2.0","id":"abc","method":"tools/call","params":{"name":"container_remove","arguments":{"idOrName":"missing","engine":"docker"}}}""");
        using var capture = ConsoleErrorCapture.Start();

        await handler.HandleAsync(document.RootElement, CancellationToken.None);

        var log = capture.Text;
        Assert.Contains("error: mcp tool error", log);
        Assert.Contains("requestId=abc", log);
        Assert.Contains("tool=container_remove", log);
        Assert.Contains("resourceType=container", log);
        Assert.Contains("resourceId=missing", log);
        Assert.Contains("errorCode=container_not_found", log);
        Assert.Contains("statusCode=400", log);
        Assert.Contains("message=\"Tool failed.\"", log);
    }

    private sealed class EmptyToolRegistry : IMcpToolRegistry
    {
        public JsonArray List() => [];

        public Task<JsonObject> CallAsync(string name, JsonElement arguments, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ReturningToolRegistry : IMcpToolRegistry
    {
        public JsonArray List() => [];

        public Task<JsonObject> CallAsync(string name, JsonElement arguments, CancellationToken cancellationToken) =>
            Task.FromResult(new JsonObject
            {
                ["engine"] = "docker",
                ["target"] = "local"
            });
    }

    private sealed class ThrowingToolRegistry(string errorCode, JsonObject? details = null) : IMcpToolRegistry
    {
        public JsonArray List() => [];

        public Task<JsonObject> CallAsync(string name, JsonElement arguments, CancellationToken cancellationToken) =>
            throw new ContainerMcpException(errorCode, "Tool failed.", StatusCodes.Status400BadRequest, details: details);
    }

    private sealed class SlowToolRegistry : IMcpToolRegistry
    {
        public JsonArray List() => [];

        public async Task<JsonObject> CallAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new JsonObject();
        }
    }

    private sealed class ConsoleErrorCapture : IDisposable
    {
        private readonly TextWriter _original;
        private readonly StringWriter _writer = new();

        private ConsoleErrorCapture()
        {
            _original = Console.Error;
            Console.SetError(_writer);
        }

        public string Text => _writer.ToString();

        public static ConsoleErrorCapture Start() => new();

        public void Dispose()
        {
            Console.SetError(_original);
            _writer.Dispose();
        }
    }
}
