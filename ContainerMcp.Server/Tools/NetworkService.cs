using System.Text.Json.Nodes;
using ContainerMcp.ContainerRuntime;

namespace ContainerMcp.Tools;

internal sealed class NetworkService
{
    private readonly RuntimeToolSupport _runtime;
    private readonly ContainerApiAdapter _api;

    public NetworkService(RuntimeToolSupport runtime, ContainerApiAdapter api)
    {
        _runtime = runtime;
        _api = api;
    }

    public async Task<JsonObject> NetworkListAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var engine = await ResolveAsync(args, cancellationToken);
        var result = await _api.GetAsync(engine, "/networks", cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> NetworkInspectAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var idOrName = ToolArgumentReader.RequireString(args, "idOrName");
        var engine = await ResolveAsync(args, cancellationToken);
        var result = await _api.GetAsync(engine, NetworkToolRequests.BuildInspectPath(idOrName), cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> NetworkCreateAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var name = ToolArgumentReader.RequireString(args, "name");
        var engine = await ResolveAsync(args, cancellationToken);
        var result = await _api.PostAsync(engine, "/networks/create", NetworkToolRequests.BuildCreateBody(args, name), cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> NetworkRemoveAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var idOrName = ToolArgumentReader.RequireString(args, "idOrName");
        var engine = await ResolveAsync(args, cancellationToken);
        var result = await _api.DeleteAsync(engine, NetworkToolRequests.BuildRemovePath(idOrName), cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> NetworkConnectAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var network = ToolArgumentReader.RequireString(args, "network");
        var container = ToolArgumentReader.RequireString(args, "container");
        var engine = await ResolveAsync(args, cancellationToken);
        var result = await _api.PostAsync(engine, NetworkToolRequests.BuildConnectPath(network), NetworkToolRequests.BuildConnectBody(args, container), cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> NetworkDisconnectAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var network = ToolArgumentReader.RequireString(args, "network");
        var container = ToolArgumentReader.RequireString(args, "container");
        var engine = await ResolveAsync(args, cancellationToken);
        var result = await _api.PostAsync(engine, NetworkToolRequests.BuildDisconnectPath(network), NetworkToolRequests.BuildDisconnectBody(args, container), cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> NetworkPruneAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var engine = await ResolveAsync(args, cancellationToken);
        var result = await _api.PostAsync(engine, NetworkToolRequests.BuildPrunePath(args), null, cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    private async Task<ResolvedEngine> ResolveAsync(JsonElement args, CancellationToken cancellationToken) =>
        await _runtime.ResolveAsync(args, cancellationToken);
}
