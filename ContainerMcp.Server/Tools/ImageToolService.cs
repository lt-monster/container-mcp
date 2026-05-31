using System.Text.Json.Nodes;
using ContainerMcp.ContainerRuntime;
using ContainerMcp.Models;

namespace ContainerMcp.Tools;

internal sealed class ImageToolService
{
    private const int MaxPullEvents = 500;
    private const string AnonymousRegistryAuth = "e30=";

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

    public async Task<JsonObject> ImageInspectAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var image = ToolArgumentReader.RequireString(args, "imageIdOrName");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var result = await _api.GetAsync(engine, ImageToolRequests.BuildInspectPath(image), cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ImagePullAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var image = ToolArgumentReader.RequireString(args, "image");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var path = "/images/create?fromImage=" + Uri.EscapeDataString(image);
        var result = await _api.PostJsonMessageStreamAsync(engine, path, null, MaxPullEvents, cancellationToken);
        ThrowIfProgressError(result, engine);

        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ImageTagAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var source = ToolArgumentReader.RequireString(args, "source");
        var repo = ToolArgumentReader.RequireString(args, "repo");
        var tag = ToolArgumentReader.OptionalString(args, "tag");
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var result = await _api.PostAsync(engine, ImageToolRequests.BuildTagPath(source, repo, tag), null, cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ImagePruneAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var result = await _api.PostAsync(engine, ImageToolRequests.BuildPrunePath(args), null, cancellationToken);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ImageBuildAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var contextTarPath = RequireReadableFile(args, "contextTarPath");
        var maxEvents = ImageToolRequests.NormalizeMaxEvents(ToolArgumentReader.OptionalInt(args, "maxEvents"));
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        await using var tarStream = File.OpenRead(contextTarPath);
        var result = await _api.PostTarJsonMessageStreamAsync(engine, ImageToolRequests.BuildBuildPath(args), tarStream, maxEvents, cancellationToken);
        ThrowIfProgressError(result, engine);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ImagePushAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var image = ToolArgumentReader.RequireString(args, "image");
        var tag = ToolArgumentReader.OptionalString(args, "tag");
        var maxEvents = ImageToolRequests.NormalizeMaxEvents(ToolArgumentReader.OptionalInt(args, "maxEvents"));
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var result = await _api.PostJsonMessageStreamAsync(
            engine,
            ImageToolRequests.BuildPushPath(image, tag),
            null,
            new Dictionary<string, string> { ["X-Registry-Auth"] = AnonymousRegistryAuth },
            maxEvents,
            cancellationToken);
        ThrowIfProgressError(result, engine);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ImageLoadAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var tarPath = RequireReadableFile(args, "tarPath");
        var quiet = ToolArgumentReader.OptionalBool(args, "quiet");
        var maxEvents = ImageToolRequests.NormalizeMaxEvents(ToolArgumentReader.OptionalInt(args, "maxEvents"));
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        await using var tarStream = File.OpenRead(tarPath);
        var result = await _api.PostTarJsonMessageStreamAsync(engine, ImageToolRequests.BuildLoadPath(quiet), tarStream, maxEvents, cancellationToken);
        ThrowIfProgressError(result, engine);
        return RuntimeToolSupport.Success(engine, result);
    }

    public async Task<JsonObject> ImageSaveAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var image = ToolArgumentReader.RequireString(args, "image");
        var outputPath = RequireOutputPath(args);
        var maxBytes = ImageToolRequests.NormalizeMaxBytes(ToolArgumentReader.OptionalLong(args, "maxBytes"));
        var engine = await _runtime.ResolveAsync(args, cancellationToken);
        var result = await _api.GetToFileAsync(engine, ImageToolRequests.BuildSavePath(image), outputPath, maxBytes, cancellationToken);
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

    private static void ThrowIfProgressError(JsonObject result, ResolvedEngine engine)
    {
        if (result.TryGetPropertyValue("lastError", out var errorNode)
            && errorNode is JsonValue errorValue
            && errorValue.TryGetValue<string>(out var error)
            && !string.IsNullOrWhiteSpace(error))
        {
            throw new ContainerMcpException(McpErrorCode.OperationFailed, error, StatusCodes.Status500InternalServerError, engine.Endpoint.ToString());
        }
    }

    private static string RequireReadableFile(JsonElement args, string name)
    {
        var path = ToolArgumentReader.RequireString(args, name);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ContainerMcpException(McpErrorCode.InvalidArgument, $"Argument '{name}' must be an absolute path.", StatusCodes.Status400BadRequest);
        }

        if (!File.Exists(path) || File.GetAttributes(path).HasFlag(FileAttributes.Directory))
        {
            throw new ContainerMcpException(McpErrorCode.InvalidArgument, $"Argument '{name}' must reference an existing file.", StatusCodes.Status400BadRequest);
        }

        return path;
    }

    private static string RequireOutputPath(JsonElement args)
    {
        var outputPath = ToolArgumentReader.RequireString(args, "outputPath");
        if (!Path.IsPathFullyQualified(outputPath))
        {
            throw new ContainerMcpException(McpErrorCode.InvalidArgument, "Argument 'outputPath' must be an absolute path.", StatusCodes.Status400BadRequest);
        }

        var directory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new ContainerMcpException(McpErrorCode.InvalidArgument, "Argument 'outputPath' parent directory must exist.", StatusCodes.Status400BadRequest);
        }

        if (!ToolArgumentReader.OptionalBool(args, "overwrite") && File.Exists(outputPath))
        {
            throw new ContainerMcpException(McpErrorCode.InvalidArgument, "Argument 'outputPath' already exists. Set overwrite=true to replace it.", StatusCodes.Status400BadRequest);
        }

        return outputPath;
    }
}
