using System.Text.Json;
using ContainerMcp.ContainerRuntime;
using ContainerMcp.Models;
using ContainerMcp.Tools;

namespace ContainerMcp.Server.Tests;

public sealed class ContainerCreateRequestBuilderTests
{
    [Theory]
    [InlineData("""{"ports":["8080:80:tcp:extra"]}""", "Invalid port mapping '8080:80:tcp:extra'. Expected containerPort[/protocol], hostPort:containerPort[/protocol], or hostIp:hostPort:containerPort[/protocol].")]
    [InlineData("""{"ports":["not-a-port"]}""", "Invalid port mapping 'not-a-port'. Container port must be between 1 and 65535.")]
    [InlineData("""{"ports":["8080:80/sctp"]}""", "Invalid port mapping '8080:80/sctp'. Protocol must be tcp or udp.")]
    [InlineData("""{"ports":{"not-a-port":8080}}""", "Invalid port mapping 'not-a-port'. Container port must be between 1 and 65535.")]
    [InlineData("""{"ports":{"80/tcp":"not-a-port"}}""", "Invalid host port 'not-a-port'. Host port must be between 1 and 65535.")]
    public void Build_RejectsInvalidPortMappings(string json, string expectedMessage)
    {
        using var document = JsonDocument.Parse(json);
        var builder = new ContainerCreateRequestBuilder(new VolumePolicy());

        var exception = Assert.Throws<ContainerMcpException>(
            () => builder.Build(document.RootElement, "nginx"));

        Assert.Equal(McpErrorCode.InvalidArgument, exception.ErrorCode);
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData("no")]
    [InlineData("always")]
    [InlineData("unless-stopped")]
    [InlineData("on-failure")]
    public void Build_AcceptsSupportedRestartPolicies(string policy)
    {
        using var document = JsonDocument.Parse($$"""{"restartPolicy":"{{policy}}"}""");
        var builder = new ContainerCreateRequestBuilder(new VolumePolicy());

        var body = builder.Build(document.RootElement, "nginx");

        Assert.Equal(policy, body["HostConfig"]!["RestartPolicy"]!["Name"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("sometimes")]
    public void Build_RejectsUnsupportedRestartPolicies(string policy)
    {
        using var document = JsonDocument.Parse($$"""{"restartPolicy":"{{policy}}"}""");
        var builder = new ContainerCreateRequestBuilder(new VolumePolicy());

        var exception = Assert.Throws<ContainerMcpException>(
            () => builder.Build(document.RootElement, "nginx"));

        Assert.Equal(McpErrorCode.InvalidArgument, exception.ErrorCode);
        Assert.Equal("Argument 'restartPolicy' must be one of: no, always, unless-stopped, on-failure.", exception.Message);
    }
}
