using System.Text.Json.Nodes;
using ContainerMcp.ContainerRuntime;
using ContainerMcp.Models;

namespace ContainerMcp.Tools;

internal sealed class ImageToolService
{
    private const int MaxPullEvents = 500;

    private readonly RuntimeToolSupport _runtime;
    private readonly ContainerApiAdapter _api;

    public ImageToolService(RuntimeToolSupport runtime, ContainerApiAdapter api)
    {
        _runtime = runtime;
        _api = api;
    }

    public async Task<JsonObject> ImageListAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var result = await _api.GetAsync(engine, "/images/json", cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ImagePullAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var image = ToolArgumentReader.RequireString(args, "image");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var path = "/images/create?fromImage=" + Uri.EscapeDataString(image);
        var result = await _api.PostJsonMessageStreamAsync(engine, path, null, MaxPullEvents, cancellationToken);
        if (result.TryGetPropertyValue("lastError", out var errorNode)
            && errorNode is JsonValue errorValue
            && errorValue.TryGetValue<string>(out var error)
            && !string.IsNullOrWhiteSpace(error))
        {
            throw new ContainerMcpException(McpErrorCode.OperationFailed, error, StatusCodes.Status500InternalServerError, engine.Endpoint.ToString());
        }

        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ImageRemoveAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var image = ToolArgumentReader.RequireString(args, "imageIdOrName");
        var force = ToolArgumentReader.OptionalBool(args, "force");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var path = $"/images/{Uri.EscapeDataString(image)}?force={force.ToString().ToLowerInvariant()}";
        var result = await _api.DeleteAsync(engine, path, cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }
}
