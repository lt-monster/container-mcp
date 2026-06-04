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
    public async Task HandleHealth_ReturnsOkJsonMetadata()
    {
        var context = HttpContext();

        var result = McpHttpEndpoint.HandleHealth();
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.StartsWith("application/json", context.Response.ContentType);
        var root = await ReadResponseAsync(context);
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("container-mcp", root.GetProperty("service").GetString());
    }

    [Fact]
    public async Task HandleReady_ReturnsReadyJsonMetadata()
    {
        var context = HttpContext();
        var options = ContainerMcpOptions.From([]);

        var result = McpHttpEndpoint.HandleReady(options);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.StartsWith("application/json", context.Response.ContentType);
        var root = await ReadResponseAsync(context);
        Assert.Equal("ready", root.GetProperty("status").GetString());
        Assert.Equal("container-mcp", root.GetProperty("service").GetString());
        Assert.Equal(ServerVersion.Current, root.GetProperty("version").GetString());
        Assert.Equal("http", root.GetProperty("transport").GetString());
        Assert.Equal("/mcp", root.GetProperty("endpoint").GetString());
        Assert.Equal("auto", root.GetProperty("defaultEngine").GetString());
        Assert.Equal("local", root.GetProperty("defaultTarget").GetString());
        Assert.False(root.GetProperty("httpAuthRequired").GetBoolean());
    }

    [Fact]
    public async Task HandleReady_ReportsHttpAuthRequiredWhenTokensConfigured()
    {
        var context = HttpContext();
        var options = OptionsWithToken("cmcp_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMN");

        var result = McpHttpEndpoint.HandleReady(options);
        await result.ExecuteAsync(context);

        var root = await ReadResponseAsync(context);
        Assert.True(root.GetProperty("httpAuthRequired").GetBoolean());
    }

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
    public async Task HandlePostAsync_RejectsMissingBearerTokenWhenConfigured()
    {
        var context = HttpContextWithBody("""{"jsonrpc":"2.0","id":1,"method":"ping"}""");
        var options = OptionsWithToken("cmcp_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMN");
        var handler = new McpJsonRpcHandler(new EmptyToolRegistry(), options);

        var result = await McpHttpEndpoint.HandlePostAsync(context, handler, options);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task HandlePostAsync_AllowsMatchingBearerTokenWhenConfigured()
    {
        const string token = "cmcp_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMN";
        var context = HttpContextWithBody("""{"jsonrpc":"2.0","id":1,"method":"ping"}""");
        context.Request.Headers.Authorization = "Bearer " + token;
        var options = OptionsWithToken(token);
        var handler = new McpJsonRpcHandler(new EmptyToolRegistry(), options);

        var result = await McpHttpEndpoint.HandlePostAsync(context, handler, options);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task HandlePostAsync_LogsAcceptedAndFailedBearerAuthentication()
    {
        const string token = "cmcp_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMN";
        var options = OptionsWithToken(token);
        var handler = new McpJsonRpcHandler(new EmptyToolRegistry(), options);
        using var capture = ConsoleErrorCapture.Start();

        var rejected = HttpContextWithBody("""{"jsonrpc":"2.0","id":1,"method":"ping"}""");
        rejected.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        var rejectedResult = await McpHttpEndpoint.HandlePostAsync(rejected, handler, options);
        await rejectedResult.ExecuteAsync(rejected);

        var accepted = HttpContextWithBody("""{"jsonrpc":"2.0","id":2,"method":"ping"}""");
        accepted.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        accepted.Request.Headers.Authorization = "Bearer " + token;
        var acceptedResult = await McpHttpEndpoint.HandlePostAsync(accepted, handler, options);
        await acceptedResult.ExecuteAsync(accepted);

        var log = capture.Text;
        Assert.Contains("warn: mcp auth failed remote=127.0.0.1 reason=missing_bearer_token", log);
        Assert.Contains("info: mcp auth accepted remote=127.0.0.1 tokenId=default", log);
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
        var context = HttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return context;
    }

    private static DefaultHttpContext HttpContext()
    {
        var context = new DefaultHttpContext();
        context.RequestServices = TestServices();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonElement> ReadResponseAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement.Clone();
    }

    private static ServiceProvider TestServices() =>
        new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

    private static ContainerMcpOptions OptionsWithToken(string token) =>
        new(
            TransportMode.Http,
            "http://127.0.0.1:7010",
            ContainerEngine.Auto,
            "local",
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(15),
            ProgramSupport.MaxMcpHttpRequestBodyBytes,
            [new HttpToken("default", token, true, null, null)]);

    private sealed class EmptyToolRegistry : IMcpToolRegistry
    {
        public JsonArray List() => [];

        public Task<JsonObject> CallAsync(string name, JsonElement arguments, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
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
