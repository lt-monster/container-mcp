using System.Text.Json.Nodes;
using ContainerMcp.ContainerRuntime;

namespace ContainerMcp.Tools;

internal sealed class ContainerToolService
{
    private const int DefaultLogMaxBytes = 1024 * 1024;
    private const int HardLogMaxBytes = 4 * 1024 * 1024;
    private const int DefaultLogFollowDurationSeconds = 10;
    private const int HardLogFollowDurationSeconds = 60;

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
        var platform = ToolArgumentReader.OptionalString(args, "platform");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var body = _createRequestBuilder.Build(args, image);
        var path = ContainerCreateRequestBuilder.BuildCreatePath(name, platform);
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

    public async Task<JsonObject> ContainerPauseAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var id = ToolArgumentReader.RequireString(args, "idOrName");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var result = await _api.PostAsync(engine, ContainerToolRequests.BuildPausePath(id), null, cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ContainerUnpauseAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var id = ToolArgumentReader.RequireString(args, "idOrName");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var result = await _api.PostAsync(engine, ContainerToolRequests.BuildUnpausePath(id), null, cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ContainerRenameAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var id = ToolArgumentReader.RequireString(args, "idOrName");
        var name = ToolArgumentReader.RequireString(args, "name");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var result = await _api.PostAsync(engine, ContainerToolRequests.BuildRenamePath(id, name), null, cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ContainerExecCreateAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var id = ToolArgumentReader.RequireString(args, "idOrName");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var result = await _api.PostAsync(engine, ContainerExecRequestBuilder.BuildCreatePath(id), ContainerExecRequestBuilder.BuildCreateBody(args), cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ContainerExecStartAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var execId = ToolArgumentReader.RequireString(args, "execId");
        var tty = ToolArgumentReader.OptionalBool(args, "tty");
        var maxBytes = ContainerExecRequestBuilder.NormalizeMaxBytes(ToolArgumentReader.OptionalInt(args, "maxBytes"));
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var bytes = await _api.PostBytesAsync(
            engine,
            ContainerExecRequestBuilder.BuildStartPath(execId),
            ContainerExecRequestBuilder.BuildStartBody(tty),
            maxBytes + 8192,
            cancellationToken);
        var result = DockerRawStreamDecoder.Decode(bytes, maxBytes);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ContainerStatsAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var id = ToolArgumentReader.RequireString(args, "idOrName");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var result = await _api.GetAsync(engine, ContainerToolRequests.BuildStatsPath(id), cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ContainerTopAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var id = ToolArgumentReader.RequireString(args, "idOrName");
        var psArgs = ToolArgumentReader.OptionalString(args, "psArgs");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var result = await _api.GetAsync(engine, ContainerToolRequests.BuildTopPath(id, psArgs), cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ContainerWaitAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var id = ToolArgumentReader.RequireString(args, "idOrName");
        var condition = ToolArgumentReader.OptionalString(args, "condition");
        var timeout = ContainerToolRequests.NormalizeWaitTimeout(ToolArgumentReader.OptionalInt(args, "timeoutSeconds"));
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        using var timeoutSource = timeout is null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeoutSource is not null)
        {
            timeoutSource.CancelAfter(timeout.GetValueOrDefault());
        }

        var result = await _api.PostAsync(engine, ContainerToolRequests.BuildWaitPath(id, condition), null, timeoutSource?.Token ?? cancellationToken);
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

    public async Task<JsonObject> ContainerPruneAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var result = await _api.PostAsync(engine, ContainerToolRequests.BuildPrunePath(args), null, cancellationToken);
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
        var maxBytes = NormalizeLogMaxBytes(args);
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var bytes = await _api.GetBytesAsync(engine, ContainerToolRequests.BuildLogsPath(id, follow: false, args), maxBytes + 8192, cancellationToken);
        var result = DockerRawStreamDecoder.Decode(bytes, maxBytes);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ContainerLogsFollowAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var id = ToolArgumentReader.RequireString(args, "idOrName");
        var maxBytes = NormalizeLogMaxBytes(args);
        var durationSeconds = NormalizeLogFollowDurationSeconds(args);
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var read = await _api.GetBytesForDurationAsync(
            engine,
            ContainerToolRequests.BuildLogsPath(id, follow: true, args),
            maxBytes,
            TimeSpan.FromSeconds(durationSeconds),
            cancellationToken);

        var result = DockerRawStreamDecoder.Decode(read.Bytes, maxBytes);
        if (read.CompletedBy == "maxBytes")
        {
            result["truncated"] = true;
        }

        result["durationSeconds"] = durationSeconds;
        result["completedBy"] = read.CompletedBy;
        return RuntimeToolSupport.Success(engine, result);
    }

    private static int NormalizeLogMaxBytes(JsonElement args) =>
        Math.Clamp(ToolArgumentReader.OptionalInt(args, "maxBytes") ?? DefaultLogMaxBytes, 1, HardLogMaxBytes);

    private static int NormalizeLogFollowDurationSeconds(JsonElement args) =>
        Math.Clamp(ToolArgumentReader.OptionalInt(args, "durationSeconds") ?? DefaultLogFollowDurationSeconds, 1, HardLogFollowDurationSeconds);
}
