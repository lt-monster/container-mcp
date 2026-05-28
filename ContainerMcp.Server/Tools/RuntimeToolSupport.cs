using System.Text.Json.Nodes;
using ContainerMcp.Configuration;
using ContainerMcp.ContainerRuntime;
using ContainerMcp.Models;

namespace ContainerMcp.Tools;

internal sealed class RuntimeToolSupport
{
    private readonly ContainerMcpOptions _options;
    private readonly EngineResolver _resolver;

    public RuntimeToolSupport(ContainerMcpOptions options, EngineResolver resolver)
    {
        _options = options;
        _resolver = resolver;
    }

    public async Task<ResolvedEngine> ResolveAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var engine = _options.ResolveRequestedEngine(ToolArgumentReader.OptionalString(args, "engine"));
        var target = ToolArgumentReader.OptionalString(args, "target") ?? _options.DefaultTarget;
        return await _resolver.ResolveAsync(engine, target, cancellationToken);
    }

    public static JsonObject Success(ResolvedEngine engine, JsonElement value) => Success(engine, value.ToJsonNode());

    public static JsonObject Success(ResolvedEngine engine, string value) => Success(engine, JsonValue.Create(value));

    public static JsonObject Success(ResolvedEngine engine, JsonNode? value) => new()
    {
        ["engine"] = engine.Engine.ToString().ToLowerInvariant(),
        ["target"] = "local",
        ["items"] = value
    };
}
