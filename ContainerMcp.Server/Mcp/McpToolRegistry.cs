using ContainerMcp.Ports;
using ContainerMcp.Tools;
using ContainerMcp.Models;
using System.Text.Json.Nodes;

namespace ContainerMcp.Mcp;

internal interface IMcpToolRegistry
{
    JsonArray List();
    Task<JsonObject> CallAsync(string name, JsonElement arguments, CancellationToken cancellationToken);
}

internal sealed class McpToolRegistry : IMcpToolRegistry
{
    private readonly ImageToolService _images;
    private readonly ContainerToolService _containers;
    private readonly VolumeService _volumes;
    private readonly PortDiscoveryService _ports;
    private readonly DockerDiagnosticsService _diagnostics;
    private readonly Dictionary<string, ToolEntry> _tools;

    public McpToolRegistry(ImageToolService images, ContainerToolService containers, VolumeService volumes, PortDiscoveryService ports, DockerDiagnosticsService diagnostics)
    {
        _images = images;
        _containers = containers;
        _volumes = volumes;
        _ports = ports;
        _diagnostics = diagnostics;
        _tools = BuildTools();
    }

    public JsonArray List()
    {
        var tools = new JsonArray();
        foreach (var tool in _tools.Values)
        {
            tools.AddNode(new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["inputSchema"] = tool.InputSchema.DeepClone()
            });
        }

        return tools;
    }

    public async Task<JsonObject> CallAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!_tools.TryGetValue(name, out var tool))
        {
            throw new ContainerMcpException(Models.McpErrorCode.InvalidArgument, $"Unknown tool '{name}'.", StatusCodes.Status400BadRequest);
        }

        McpInputSchemaValidator.Validate(arguments, tool.InputSchema);
        return await tool.Handler(arguments, cancellationToken);
    }

    private Dictionary<string, ToolEntry> BuildTools()
    {
        var engineTarget = new Dictionary<string, JsonNode?>
        {
            ["engine"] = EnumSchema("auto", "docker", "podman"),
            ["target"] = new JsonObject { ["type"] = "string", ["enum"] = JsonNodeExtensions.Array(JsonValue.Create("local")) }
        };

        ToolEntry T(string name, string description, JsonObject properties, string[]? required, ToolHandler handler)
        {
            foreach (var property in engineTarget)
            {
                if (!properties.ContainsKey(property.Key))
                {
                    properties[property.Key] = property.Value?.DeepClone();
                }
            }

            return new ToolEntry(name, description, ObjectSchema(properties, required), handler);
        }

        var tools = new[]
        {
            T("image_list", "List local container images.", new JsonObject(), null, _images.ImageListAsync),
            T("image_inspect", "Inspect a local container image.", new JsonObject { ["imageIdOrName"] = StringSchema("Image ID or name.") }, ["imageIdOrName"], _images.ImageInspectAsync),
            T("image_pull", "Pull a container image.", new JsonObject { ["image"] = StringSchema("Image reference, for example nginx:latest.") }, ["image"], _images.ImagePullAsync),
            T("image_tag", "Tag a local container image.", new JsonObject { ["source"] = StringSchema("Source image ID or name."), ["repo"] = StringSchema("Target repository."), ["tag"] = StringSchema("Target tag.") }, ["source", "repo"], _images.ImageTagAsync),
            T("image_prune", "Prune unused local container images.", new JsonObject { ["dangling"] = BoolSchema(), ["until"] = StringSchema("Prune images created before this timestamp or duration."), ["labels"] = StringArraySchema("Label filters."), ["labelNe"] = StringArraySchema("Negative label filters.") }, null, _images.ImagePruneAsync),
            T("image_build", "Build an image from a tar build context.", new JsonObject { ["contextTarPath"] = StringSchema("Absolute path to a tar build context."), ["tag"] = StringSchema("Image tag to apply."), ["dockerfile"] = StringSchema("Dockerfile path inside the context."), ["noCache"] = BoolSchema(), ["pull"] = BoolSchema(), ["removeIntermediate"] = BoolSchema(), ["forceRemoveIntermediate"] = BoolSchema(), ["maxEvents"] = IntSchema() }, ["contextTarPath", "tag"], _images.ImageBuildAsync),
            T("image_push", "Push a container image.", new JsonObject { ["image"] = StringSchema("Image reference to push."), ["tag"] = StringSchema("Tag to push."), ["maxEvents"] = IntSchema() }, ["image"], _images.ImagePushAsync),
            T("image_load", "Load images from a tar archive.", new JsonObject { ["tarPath"] = StringSchema("Absolute path to an image tar archive."), ["quiet"] = BoolSchema(), ["maxEvents"] = IntSchema() }, ["tarPath"], _images.ImageLoadAsync),
            T("image_save", "Save a local image to a tar archive.", new JsonObject { ["image"] = StringSchema("Image ID or name."), ["outputPath"] = StringSchema("Absolute output tar path."), ["maxBytes"] = IntSchema(), ["overwrite"] = BoolSchema() }, ["image", "outputPath"], _images.ImageSaveAsync),
            T("image_remove", "Remove a local container image.", new JsonObject { ["imageIdOrName"] = StringSchema("Image ID or name."), ["force"] = BoolSchema() }, ["imageIdOrName"], _images.ImageRemoveAsync),
            T("container_list", "List containers.", new JsonObject { ["all"] = BoolSchema() }, null, _containers.ContainerListAsync),
            T("container_inspect", "Inspect a container.", new JsonObject { ["idOrName"] = StringSchema("Container ID or name.") }, ["idOrName"], _containers.ContainerInspectAsync),
            T("container_create", "Create a container.", new()
            {
                ["image"] = StringSchema("Container image."),
                ["name"] = StringSchema("Optional container name."),
                ["ports"] = PortMappingsSchema(),
                ["env"] = ObjectMapSchema(),
                ["volumes"] = StringArraySchema("Named volume mounts using source:target[:mode]. Bind mounts are rejected in v1."),
                ["command"] = CommandSchema(),
                ["restartPolicy"] = StringSchema("Restart policy name."),
                ["labels"] = ObjectMapSchema()
            }, ["image"], _containers.ContainerCreateAsync),
            T("container_start", "Start a container.", new JsonObject { ["idOrName"] = StringSchema("Container ID or name.") }, ["idOrName"], _containers.ContainerStartAsync),
            T("container_pause", "Pause a container.", new JsonObject { ["idOrName"] = StringSchema("Container ID or name.") }, ["idOrName"], _containers.ContainerPauseAsync),
            T("container_unpause", "Unpause a container.", new JsonObject { ["idOrName"] = StringSchema("Container ID or name.") }, ["idOrName"], _containers.ContainerUnpauseAsync),
            T("container_rename", "Rename a container.", new JsonObject { ["idOrName"] = StringSchema("Container ID or name."), ["name"] = StringSchema("New container name.") }, ["idOrName", "name"], _containers.ContainerRenameAsync),
            T("container_exec_create", "Create an exec instance in a container.", new JsonObject { ["idOrName"] = StringSchema("Container ID or name."), ["command"] = CommandSchema(), ["env"] = ObjectMapSchema(), ["user"] = StringSchema("User to run as inside the container."), ["workingDir"] = StringSchema("Working directory inside the container."), ["tty"] = BoolSchema(), ["attachStdout"] = BoolSchema(), ["attachStderr"] = BoolSchema() }, ["idOrName", "command"], _containers.ContainerExecCreateAsync),
            T("container_exec_start", "Start an exec instance and read bounded output.", new JsonObject { ["execId"] = StringSchema("Exec instance ID."), ["tty"] = BoolSchema(), ["maxBytes"] = IntSchema() }, ["execId"], _containers.ContainerExecStartAsync),
            T("container_stats", "Read a bounded container stats snapshot.", new JsonObject { ["idOrName"] = StringSchema("Container ID or name.") }, ["idOrName"], _containers.ContainerStatsAsync),
            T("container_top", "List processes running in a container.", new JsonObject { ["idOrName"] = StringSchema("Container ID or name."), ["psArgs"] = StringSchema("Arguments passed to ps, for example aux.") }, ["idOrName"], _containers.ContainerTopAsync),
            T("container_wait", "Wait for a container condition and return its exit status.", new JsonObject { ["idOrName"] = StringSchema("Container ID or name."), ["condition"] = EnumSchema("not-running", "next-exit", "removed"), ["timeoutSeconds"] = IntSchema() }, ["idOrName"], _containers.ContainerWaitAsync),
            T("container_stop", "Stop a container.", new JsonObject { ["idOrName"] = StringSchema("Container ID or name."), ["timeoutSeconds"] = IntSchema() }, ["idOrName"], _containers.ContainerStopAsync),
            T("container_restart", "Restart a container.", new JsonObject { ["idOrName"] = StringSchema("Container ID or name."), ["timeoutSeconds"] = IntSchema() }, ["idOrName"], _containers.ContainerRestartAsync),
            T("container_kill", "Kill a container.", new JsonObject { ["idOrName"] = StringSchema("Container ID or name."), ["signal"] = StringSchema("Signal to send, for example SIGKILL or SIGTERM.") }, ["idOrName"], _containers.ContainerKillAsync),
            T("container_remove", "Remove a container.", new JsonObject { ["idOrName"] = StringSchema("Container ID or name."), ["force"] = BoolSchema(), ["volumes"] = BoolSchema() }, ["idOrName"], _containers.ContainerRemoveAsync),
            T("container_logs", "Read container logs.", new JsonObject { ["idOrName"] = StringSchema("Container ID or name."), ["tail"] = StringSchema("Number of lines or all."), ["since"] = StringSchema("Timestamp or duration accepted by the runtime."), ["timestamps"] = BoolSchema(), ["maxBytes"] = IntSchema() }, ["idOrName"], _containers.ContainerLogsAsync),
            T("volume_list", "List container volumes.", new JsonObject(), null, _volumes.VolumeListAsync),
            T("volume_create", "Create a named volume.", new JsonObject { ["name"] = StringSchema("Volume name."), ["labels"] = ObjectMapSchema() }, ["name"], _volumes.VolumeCreateAsync),
            T("volume_remove", "Remove a volume.", new JsonObject { ["name"] = StringSchema("Volume name."), ["force"] = BoolSchema() }, ["name"], _volumes.VolumeRemoveAsync),
            T("docker_diagnose", "Diagnose Docker Desktop endpoint resolution and probe status on Windows.", new JsonObject(), null, (_, cancellationToken) => _diagnostics.DiagnoseAsync(cancellationToken)),
            T("port_find_free", "Find free local host ports.", new()
            {
                ["host"] = StringSchema("Host address. Defaults to 127.0.0.1."),
                ["start"] = IntSchema(),
                ["end"] = IntSchema(),
                ["count"] = IntSchema(),
                ["protocol"] = EnumSchema("tcp", "udp")
            }, null, (args, _) => Task.FromResult(_ports.FindFree(args)))
        };

        return tools.ToDictionary(tool => tool.Name, StringComparer.Ordinal);
    }

    private static JsonObject ObjectSchema(JsonObject properties, string[]? required)
    {
        var requiredArray = new JsonArray();
        foreach (var name in required ?? [])
        {
            requiredArray.AddNode(JsonValue.Create(name));
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = requiredArray,
            ["additionalProperties"] = false
        };
    }

    private static JsonObject StringSchema(string? description = null) => new()
    {
        ["type"] = "string",
        ["description"] = description
    };

    private static JsonObject BoolSchema() => new() { ["type"] = "boolean" };
    private static JsonObject IntSchema() => new() { ["type"] = "integer" };
    private static JsonObject EnumSchema(params string[] values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.AddNode(JsonValue.Create(value));
        }

        return new JsonObject { ["type"] = "string", ["enum"] = array };
    }

    private static JsonObject StringArraySchema(string description) => new()
    {
        ["type"] = "array",
        ["items"] = new JsonObject { ["type"] = "string" },
        ["description"] = description
    };

    private static JsonObject ObjectMapSchema() => new()
    {
        ["type"] = "object",
        ["additionalProperties"] = new JsonObject { ["type"] = "string" }
    };

    private static JsonObject CommandSchema() => new()
    {
        ["description"] = "Command string or string array.",
        ["oneOf"] = JsonNodeExtensions.Array(
            new JsonObject { ["type"] = "string" },
            new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" }
            })
    };

    private static JsonObject PortMappingsSchema() => new()
    {
        ["description"] = "Port mappings as strings like 8080:80 or an object mapping container port to host port.",
        ["oneOf"] = JsonNodeExtensions.Array(
            new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" }
            },
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = new JsonObject
                {
                    ["oneOf"] = JsonNodeExtensions.Array(
                        new JsonObject { ["type"] = "string" },
                        new JsonObject { ["type"] = "integer" })
                }
            })
    };
}

internal delegate Task<JsonObject> ToolHandler(JsonElement arguments, CancellationToken cancellationToken);

internal sealed record ToolEntry(
    string Name,
    string Description,
    JsonObject InputSchema,
    ToolHandler Handler);
