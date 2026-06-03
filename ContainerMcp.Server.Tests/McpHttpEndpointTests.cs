using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ContainerMcp.Configuration;
using ContainerMcp.Mcp;
using ContainerMcp.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerMcp.Server.Tests;

public sealed class McpHttpEndpointTests
{
    [Fact]
    public async Task HandlePostAsync_ReturnsAcceptedForNotificationOnlyMessage()
    {
        var context = HttpContextWithBody("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        var handler = new McpJsonRpcHandler(new EmptyToolRegistry(), ContainerMcpOptions.From([]));

        var result = await McpHttpEndpoint.HandlePostAsync(context, handler);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task HandlePostAsync_ReturnsJsonForBatchWithResponses()
    {
        var context = HttpContextWithBody(
            """
            [
              {"jsonrpc":"2.0","id":1,"method":"ping"},
              {"jsonrpc":"2.0","method":"notifications/initialized"}
            ]
            """);
        var handler = new McpJsonRpcHandler(new EmptyToolRegistry(), ContainerMcpOptions.From([]));

        var result = await McpHttpEndpoint.HandlePostAsync(context, handler);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Single(document.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task HandleGetAsync_ReturnsMethodNotAllowedWhenSseIsUnavailable()
    {
        var context = new DefaultHttpContext();
        context.RequestServices = TestServices();
        context.Response.Body = new MemoryStream();

        var result = McpHttpEndpoint.HandleGet();
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status405MethodNotAllowed, context.Response.StatusCode);
    }

    [Fact]
    public async Task HandlePostAsync_ReturnsParseErrorForInvalidJson()
    {
        var context = HttpContextWithBody("{");
        var handler = new McpJsonRpcHandler(new EmptyToolRegistry(), ContainerMcpOptions.From([]));

        var result = await McpHttpEndpoint.HandlePostAsync(context, handler);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(-32700, document.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task HandlePostAsync_RejectsRequestsOverMaxBodySize()
    {
        var context = HttpContextWithBody("""{"jsonrpc":"2.0","id":1,"method":"ping"}""");
        var handler = new McpJsonRpcHandler(new EmptyToolRegistry(), ContainerMcpOptions.From([]));

        var result = await McpHttpEndpoint.HandlePostAsync(context, handler, maxRequestBodyBytes: 8);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(-32600, document.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task HandleUnsupportedMethod_ReturnsMethodNotAllowed()
    {
        var context = new DefaultHttpContext();
        context.RequestServices = TestServices();
        context.Response.Body = new MemoryStream();

        var result = McpHttpEndpoint.HandleUnsupportedMethod();
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status405MethodNotAllowed, context.Response.StatusCode);
    }

    private static DefaultHttpContext HttpContextWithBody(string json)
    {
        var context = new DefaultHttpContext();
        context.RequestServices = TestServices();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static ServiceProvider TestServices() =>
        new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

    private sealed class EmptyToolRegistry : IMcpToolRegistry
    {
        public JsonArray List() => [];

        public Task<JsonObject> CallAsync(string name, JsonElement arguments, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
