using ContainerMcp.Configuration;
using ContainerMcp.Mcp;
using ContainerMcp.Models;
using System.Text.Json.Nodes;

var options = ContainerMcpOptions.From(args);

if (options.Transport == TransportMode.Stdio)
{
    Console.Error.WriteLine(
        $"container-mcp started with stdio transport; waiting for JSON-RPC messages on stdin. " +
        $"defaultEngine={options.DefaultEngine.ToString().ToLowerInvariant()}, defaultTarget={options.DefaultTarget}, " +
        $"toolTimeout={options.ToolTimeout.TotalSeconds:0}s, apiTimeout={options.ApiTimeout.TotalSeconds:0}s, apiProbeTimeout={options.ApiProbeTimeout.TotalSeconds:0}s");
    await using var services = ServiceGraph.Create(options);
    var stdio = services.GetRequiredService<StdioMcpServer>();
    await stdio.RunAsync(CancellationToken.None);
    return;
}

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls(options.Urls);
builder.Services.AddContainerMcpServices(options);

var app = builder.Build();

Console.Error.WriteLine(
    $"container-mcp HTTP transport listening on {options.Urls}; MCP endpoint: {TrimTrailingSlash(options.Urls)}/mcp; " +
    $"defaultEngine={options.DefaultEngine.ToString().ToLowerInvariant()}, defaultTarget={options.DefaultTarget}, " +
    $"toolTimeout={options.ToolTimeout.TotalSeconds:0}s, apiTimeout={options.ApiTimeout.TotalSeconds:0}s, apiProbeTimeout={options.ApiProbeTimeout.TotalSeconds:0}s");

app.MapGet("/", () => JsonNodeExtensions.JsonResult(new JsonObject
{
    ["name"] = "container-mcp",
    ["transport"] = "http",
    ["endpoint"] = "/mcp"
}));

app.MapPost("/mcp", async (HttpContext httpContext, McpJsonRpcHandler handler) =>
{
    try
    {
        using var document = await JsonDocument.ParseAsync(httpContext.Request.Body, cancellationToken: httpContext.RequestAborted);
        var response = await handler.HandleAsync(document.RootElement, httpContext.RequestAborted);
        if (response is null)
        {
            return Results.NoContent();
        }

        return JsonNodeExtensions.JsonResult(response);
    }
    catch (JsonException ex)
    {
        return JsonNodeExtensions.JsonResult(McpJsonRpcHandler.Error(null, -32700, ex.Message), StatusCodes.Status400BadRequest);
    }
});

app.MapMethods("/mcp", ["GET", "DELETE", "PUT", "PATCH"], () =>
    JsonNodeExtensions.JsonResult(McpJsonRpcHandler.Error(null, -32600, "Unsupported MCP HTTP method."), StatusCodes.Status405MethodNotAllowed));

await app.RunAsync();

static string TrimTrailingSlash(string urls)
{
    var firstUrl = urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? urls;
    return firstUrl.TrimEnd('/');
}

internal sealed class ServiceGraph : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private ServiceGraph(ServiceProvider provider) => _provider = provider;

    public static ServiceGraph Create(ContainerMcpOptions options)
    {
        var services = new ServiceCollection();
        services.AddContainerMcpServices(options);
        return new ServiceGraph(services.BuildServiceProvider());
    }

    public T GetRequiredService<T>() where T : notnull => _provider.GetRequiredService<T>();

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
