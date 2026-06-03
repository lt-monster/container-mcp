using System.Text.Json.Nodes;
using ContainerMcp.ContainerRuntime;
using ContainerMcp.Models;

namespace ContainerMcp.Tools;

internal sealed class VolumeService
{
    private readonly RuntimeToolSupport _runtime;
    private readonly ContainerApiAdapter _api;

    public VolumeService(RuntimeToolSupport runtime, ContainerApiAdapter api)
    {
        _runtime = runtime;
        _api = api;
    }

    public async Task<JsonObject> VolumeListAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var engine = await ResolveAsync(args, cancellationToken);
        var result = await _api.GetAsync(engine, "/volumes", cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> VolumeCreateAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var name = ToolArgumentReader.RequireString(args, "name");
        var engine = await ResolveAsync(args, cancellationToken);
        var result = await _api.PostAsync(engine, "/volumes/create", VolumeToolRequests.BuildCreateBody(args, name), cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> VolumeInspectAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var name = ToolArgumentReader.RequireString(args, "name");
        var engine = await ResolveAsync(args, cancellationToken);
        var result = await _api.GetAsync(engine, VolumeToolRequests.BuildInspectPath(name), cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> VolumePruneAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var engine = await ResolveAsync(args, cancellationToken);
        var result = await _api.PostAsync(engine, VolumeToolRequests.BuildPrunePath(args), null, cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> VolumeRemoveAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var name = ToolArgumentReader.RequireString(args, "name");
        var force = ToolArgumentReader.OptionalBool(args, "force");
        var engine = await ResolveAsync(args, cancellationToken);
        var result = await _api.DeleteAsync(engine, $"/volumes/{Uri.EscapeDataString(name)}?force={force.ToString().ToLowerInvariant()}", cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    private async Task<ResolvedEngine> ResolveAsync(JsonElement args, CancellationToken cancellationToken)
    {
        return await _runtime.ResolveAsync(args, cancellationToken);
    }
}
