using System.Text.Json.Nodes;
using ContainerMcp.ContainerRuntime;

namespace ContainerMcp.Tools;

internal sealed class ContainerToolService
{
    private const int DefaultLogMaxBytes = 1024 * 1024;
    private const int HardLogMaxBytes = 4 * 1024 * 1024;

    private readonly RuntimeToolSupport _runtime;
    private readonly ContainerApiAdapter _api;
    private readonly ContainerCreateRequestBuilder _createRequestBuilder;

    public ContainerToolService(RuntimeToolSupport runtime, ContainerApiAdapter api, ContainerCreateRequestBuilder createRequestBuilder)
    {
        _runtime = runtime;
        _api = api;
        _createRequestBuilder = createRequestBuilder;
    }

    public async Task<JsonObject> ContainerListAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var all = ToolArgumentReader.OptionalBool(args, "all");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var result = await _api.GetAsync(engine, $"/containers/json?all={all.ToString().ToLowerInvariant()}", cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ContainerInspectAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var id = ToolArgumentReader.RequireString(args, "idOrName");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var result = await _api.GetAsync(engine, $"/containers/{Uri.EscapeDataString(id)}/json", cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ContainerCreateAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var image = ToolArgumentReader.RequireString(args, "image");
        var name = ToolArgumentReader.OptionalString(args, "name");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var body = _createRequestBuilder.Build(args, image);
        var path = string.IsNullOrWhiteSpace(name)
            ? "/containers/create"
            : "/containers/create?name=" + Uri.EscapeDataString(name);
        var result = await _api.PostAsync(engine, path, body, cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ContainerStartAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var id = ToolArgumentReader.RequireString(args, "idOrName");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var result = await _api.PostAsync(engine, $"/containers/{Uri.EscapeDataString(id)}/start", null, cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ContainerStopAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var id = ToolArgumentReader.RequireString(args, "idOrName");
        var timeout = ToolArgumentReader.OptionalInt(args, "timeoutSeconds");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var result = await _api.PostAsync(engine, ContainerToolRequests.BuildStopPath(id, timeout), null, cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ContainerRestartAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var id = ToolArgumentReader.RequireString(args, "idOrName");
        var timeout = ToolArgumentReader.OptionalInt(args, "timeoutSeconds");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var result = await _api.PostAsync(engine, ContainerToolRequests.BuildRestartPath(id, timeout), null, cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ContainerKillAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var id = ToolArgumentReader.RequireString(args, "idOrName");
        var signal = ToolArgumentReader.OptionalString(args, "signal");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var result = await _api.PostAsync(engine, ContainerToolRequests.BuildKillPath(id, signal), null, cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ContainerRemoveAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var id = ToolArgumentReader.RequireString(args, "idOrName");
        var force = ToolArgumentReader.OptionalBool(args, "force");
        var volumes = ToolArgumentReader.OptionalBool(args, "volumes");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var path = $"/containers/{Uri.EscapeDataString(id)}?force={force.ToString().ToLowerInvariant()}&v={volumes.ToString().ToLowerInvariant()}";
        var result = await _api.DeleteAsync(engine, path, cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ContainerLogsAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var id = ToolArgumentReader.RequireString(args, "idOrName");
        var maxBytes = Math.Clamp(ToolArgumentReader.OptionalInt(args, "maxBytes") ?? DefaultLogMaxBytes, 1, HardLogMaxBytes);
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var query = new List<string>
        {
            "stdout=true",
            "stderr=true",
            "follow=false"
        };

        if (ToolArgumentReader.OptionalString(args, "tail") is { } tail)
        {
            query.Add("tail=" + Uri.EscapeDataString(tail));
        }

        if (ToolArgumentReader.OptionalString(args, "since") is { } since)
        {
            query.Add("since=" + Uri.EscapeDataString(since));
        }

        if (ToolArgumentReader.OptionalBool(args, "timestamps"))
        {
            query.Add("timestamps=true");
        }

        var bytes = await _api.GetBytesAsync(engine, $"/containers/{Uri.EscapeDataString(id)}/logs?{string.Join('&', query)}", maxBytes + 8192, cancellationToken);
        var result = DockerRawStreamDecoder.Decode(bytes, maxBytes);
        return RuntimeToolSupport.Success(engine, result);
    }
}
