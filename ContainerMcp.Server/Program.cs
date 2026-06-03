using ContainerMcp.Configuration;
using ContainerMcp.Mcp;
using ContainerMcp.Models;
using ContainerMcp;
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
if (ProgramSupport.HasNonLoopbackBinding(options.Urls))
{
    Console.Error.WriteLine(ProgramSupport.BuildNonLoopbackWarning(options));
}

app.MapGet("/", () => JsonNodeExtensions.JsonResult(new JsonObject
{
    ["name"] = "container-mcp",
    ["transport"] = "http",
    ["endpoint"] = "/mcp"
}));

app.MapPost("/mcp", (HttpContext httpContext, McpJsonRpcHandler handler) =>
    McpHttpEndpoint.HandlePostAsync(httpContext, handler));
app.MapGet("/mcp", McpHttpEndpoint.HandleGet);
app.MapMethods("/mcp", ["DELETE", "PUT", "PATCH"], McpHttpEndpoint.HandleUnsupportedMethod);

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
