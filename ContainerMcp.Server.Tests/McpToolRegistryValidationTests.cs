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
    [InlineData("image_list", """{"unexpected":true}""", "Unknown argument 'unexpected'.")]
    [InlineData("image_pull", """{}""", "Missing required argument 'image'.")]
    [InlineData("image_remove", """{"imageIdOrName":"nginx","force":"true"}""", "Argument 'force' must be a boolean.")]
    [InlineData("container_list", """{"engine":"invalid"}""", "Argument 'engine' must be one of: auto, docker, podman.")]
    [InlineData("container_create", """{"image":"nginx","command":{"bad":true}}""", "Argument 'command' does not match any allowed schema.")]
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
