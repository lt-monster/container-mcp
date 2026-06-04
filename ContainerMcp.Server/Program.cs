using ContainerMcp.Configuration;
using ContainerMcp.Mcp;
using ContainerMcp.Models;
using ContainerMcp;
using System.Text.Json.Nodes;

if (IsTokenGenerateCommand(args))
{
    var tokenResult = HttpTokenGenerator.Generate(ReadTokenGenerateOptions(args, AppContext.BaseDirectory));
    foreach (var token in tokenResult.Tokens)
    {
        Console.WriteLine(token);
    }

    Console.Error.WriteLine($"Updated token configuration: {tokenResult.ConfigPath}; httpTokens={tokenResult.TotalTokenCount}");
    return;
}

var options = ContainerMcpOptions.From(args);
HttpTokenValidator.ValidateForStartup(options);

if (options.Transport == TransportMode.Stdio)
{
    Console.Error.WriteLine(
        $"container-mcp started with stdio transport; waiting for JSON-RPC messages on stdin. " +
        $"defaultEngine={options.DefaultEngine.ToString().ToLowerInvariant()}, defaultTarget={options.DefaultTarget}, " +
        $"toolTimeout={options.ToolTimeout.TotalSeconds:0}s, apiTimeout={options.ApiTimeout.TotalSeconds:0}s, apiProbeTimeout={options.ApiProbeTimeout.TotalSeconds:0}s, " +
        $"httpTokens={options.HttpTokens.Count}");
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
    $"toolTimeout={options.ToolTimeout.TotalSeconds:0}s, apiTimeout={options.ApiTimeout.TotalSeconds:0}s, apiProbeTimeout={options.ApiProbeTimeout.TotalSeconds:0}s, " +
    $"httpTokens={options.HttpTokens.Count}");
if (ProgramSupport.HasNonLoopbackBinding(options.Urls))
{
    Console.Error.WriteLine(ProgramSupport.BuildNonLoopbackWarning(options));
}

app.MapGet("/", () => JsonNodeExtensions.JsonResult(new JsonObject
{
    ["name"] = "container-mcp",
    ["transport"] = "http",
    ["endpoint"] = "/mcp",
    ["healthEndpoint"] = "/health",
    ["readinessEndpoint"] = "/ready"
}));

app.MapGet("/health", McpHttpEndpoint.HandleHealth);
app.MapGet("/ready", (ContainerMcpOptions options) => McpHttpEndpoint.HandleReady(options));
app.MapPost("/mcp", (HttpContext httpContext, McpJsonRpcHandler handler, ContainerMcpOptions options) =>
    McpHttpEndpoint.HandlePostAsync(httpContext, handler, options));
app.MapGet("/mcp", McpHttpEndpoint.HandleGet);
app.MapMethods("/mcp", ["DELETE", "PUT", "PATCH"], McpHttpEndpoint.HandleUnsupportedMethod);

await app.RunAsync();

static string TrimTrailingSlash(string urls)
{
    var firstUrl = urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? urls;
    return firstUrl.TrimEnd('/');
}

static bool IsTokenGenerateCommand(string[] args) =>
    args.Length >= 2
    && string.Equals(args[0], "token", StringComparison.OrdinalIgnoreCase)
    && string.Equals(args[1], "generate", StringComparison.OrdinalIgnoreCase);

static HttpTokenGenerateOptions ReadTokenGenerateOptions(string[] args, string appBaseDirectory)
{
    var configPath = ContainerMcpConfigurationLoader.ReadOption(args, "--config")
        ?? HttpTokenGenerator.DefaultConfigPath(appBaseDirectory);
    var count = int.TryParse(ContainerMcpConfigurationLoader.ReadOption(args, "--count"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCount)
        ? parsedCount
        : 1;
    return new HttpTokenGenerateOptions(
        configPath,
        count,
        ContainerMcpConfigurationLoader.ReadOption(args, "--id"),
        ContainerMcpConfigurationLoader.ReadOption(args, "--description"));
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
