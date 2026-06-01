using System.Text.Json;
using ContainerMcp.Configuration;
using ContainerMcp.ContainerRuntime;
using ContainerMcp.Mcp;
using ContainerMcp.Models;
using ContainerMcp.Ports;
using ContainerMcp.Tools;

namespace ContainerMcp.Server.Tests;

public sealed class McpToolRegistryValidationTests
{
    [Theory]
    [InlineData("image_inspect")]
    [InlineData("image_tag")]
    [InlineData("image_prune")]
    [InlineData("image_build")]
    [InlineData("image_push")]
    [InlineData("image_load")]
    [InlineData("image_save")]
    public void List_IncludesImageManagementTool(string toolName)
    {
        var registry = CreateRegistry();

        var tools = registry.List();

        Assert.Contains(tools, tool => tool!["name"]!.GetValue<string>() == toolName);
    }

    [Theory]
    [InlineData("container_restart")]
    [InlineData("container_kill")]
    [InlineData("container_pause")]
    [InlineData("container_unpause")]
    [InlineData("container_rename")]
    [InlineData("container_exec_create")]
    [InlineData("container_exec_start")]
    [InlineData("container_stats")]
    [InlineData("container_top")]
    [InlineData("container_wait")]
    public void List_IncludesContainerManagementTool(string toolName)
    {
        var registry = CreateRegistry();

        var tools = registry.List();

        Assert.Contains(tools, tool => tool!["name"]!.GetValue<string>() == toolName);
    }

    [Theory]
    [InlineData("image_list", """{"unexpected":true}""", "Unknown argument 'unexpected'.")]
    [InlineData("image_pull", """{}""", "Missing required argument 'image'.")]
    [InlineData("image_remove", """{"imageIdOrName":"nginx","force":"true"}""", "Argument 'force' must be a boolean.")]
    [InlineData("image_inspect", """{}""", "Missing required argument 'imageIdOrName'.")]
    [InlineData("image_tag", """{"source":"nginx"}""", "Missing required argument 'repo'.")]
    [InlineData("image_prune", """{"labels":[1]}""", "Argument 'labels[0]' must be a string.")]
    [InlineData("image_build", """{"tag":"app:dev"}""", "Missing required argument 'contextTarPath'.")]
    [InlineData("image_push", """{}""", "Missing required argument 'image'.")]
    [InlineData("image_load", """{"quiet":"false"}""", "Missing required argument 'tarPath'.")]
    [InlineData("image_save", """{"image":"nginx"}""", "Missing required argument 'outputPath'.")]
    [InlineData("container_list", """{"engine":"invalid"}""", "Argument 'engine' must be one of: auto, docker, podman.")]
    [InlineData("container_restart", """{}""", "Missing required argument 'idOrName'.")]
    [InlineData("container_restart", """{"idOrName":"web","timeoutSeconds":"10"}""", "Argument 'timeoutSeconds' must be an integer.")]
    [InlineData("container_stop", """{"idOrName":"web","timeoutSeconds":"10"}""", "Argument 'timeoutSeconds' must be an integer.")]
    [InlineData("container_kill", """{}""", "Missing required argument 'idOrName'.")]
    [InlineData("container_kill", """{"idOrName":"web","signal":9}""", "Argument 'signal' must be a string.")]
    [InlineData("container_pause", """{}""", "Missing required argument 'idOrName'.")]
    [InlineData("container_unpause", """{}""", "Missing required argument 'idOrName'.")]
    [InlineData("container_rename", """{}""", "Missing required argument 'idOrName'.")]
    [InlineData("container_rename", """{"idOrName":"web"}""", "Missing required argument 'name'.")]
    [InlineData("container_exec_create", """{}""", "Missing required argument 'idOrName'.")]
    [InlineData("container_exec_create", """{"idOrName":"web"}""", "Missing required argument 'command'.")]
    [InlineData("container_exec_create", """{"idOrName":"web","command":{"bad":true}}""", "Argument 'command' does not match any allowed schema.")]
    [InlineData("container_exec_create", """{"idOrName":"web","command":"date","tty":"true"}""", "Argument 'tty' must be a boolean.")]
    [InlineData("container_exec_start", """{}""", "Missing required argument 'execId'.")]
    [InlineData("container_exec_start", """{"execId":"abc","maxBytes":"1024"}""", "Argument 'maxBytes' must be an integer.")]
    [InlineData("container_exec_start", """{"execId":"abc","tty":"true"}""", "Argument 'tty' must be a boolean.")]
    [InlineData("container_stats", """{}""", "Missing required argument 'idOrName'.")]
    [InlineData("container_top", """{}""", "Missing required argument 'idOrName'.")]
    [InlineData("container_top", """{"idOrName":"web","psArgs":true}""", "Argument 'psArgs' must be a string.")]
    [InlineData("container_wait", """{}""", "Missing required argument 'idOrName'.")]
    [InlineData("container_wait", """{"idOrName":"web","condition":"running"}""", "Argument 'condition' must be one of: not-running, next-exit, removed.")]
    [InlineData("container_wait", """{"idOrName":"web","timeoutSeconds":"10"}""", "Argument 'timeoutSeconds' must be an integer.")]
    [InlineData("container_create", """{"image":"nginx","command":{"bad":true}}""", "Argument 'command' does not match any allowed schema.")]
    [InlineData("container_create", """{"image":"nginx","tty":"true"}""", "Argument 'tty' must be a boolean.")]
    [InlineData("container_create", """{"image":"nginx","entrypoint":{"bad":true}}""", "Argument 'entrypoint' does not match any allowed schema.")]
    [InlineData("container_create", """{"image":"nginx","healthcheck":{"test":"CMD"}}""", "Argument 'healthcheck.test' must be an array.")]
    [InlineData("container_create", """{"image":"nginx","healthcheck":{"unexpected":true}}""", "Unknown argument 'healthcheck.unexpected'.")]
    [InlineData("container_create", """{"image":"nginx","memoryBytes":"1024"}""", "Argument 'memoryBytes' must be an integer.")]
    [InlineData("volume_create", """{"name":"cache","labels":{"ttl":30}}""", "Argument 'labels.ttl' must be a string.")]
    public async Task CallAsync_RejectsInvalidArgumentsBeforeHandlerRuns(string toolName, string json, string expectedMessage)
    {
        var registry = CreateRegistry();
        using var document = JsonDocument.Parse(json);

        var exception = await Assert.ThrowsAsync<ContainerMcpException>(
            () => registry.CallAsync(toolName, document.RootElement, CancellationToken.None));

        Assert.Equal(McpErrorCode.InvalidArgument, exception.ErrorCode);
        Assert.Equal(400, exception.StatusCode);
        Assert.Equal(expectedMessage, exception.Message);
    }

    private static McpToolRegistry CreateRegistry()
    {
        var options = ContainerMcpOptions.From([]);
        var factory = new DockerApiClientFactory(options);
        var resolver = new EngineResolver(factory);
        var runtime = new RuntimeToolSupport(options, resolver);
        var api = new ContainerApiAdapter(factory, options);
        var volumePolicy = new VolumePolicy();
        var createRequestBuilder = new ContainerCreateRequestBuilder(volumePolicy);

        return new McpToolRegistry(
            new ImageToolService(runtime, api),
            new ContainerToolService(runtime, api, createRequestBuilder),
            new VolumeService(runtime, api),
            new PortDiscoveryService(),
            new DockerDiagnosticsService(options, factory));
    }
}
