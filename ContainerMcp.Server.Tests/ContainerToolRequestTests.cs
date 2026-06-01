using ContainerMcp.Tools;

namespace ContainerMcp.Server.Tests;

public sealed class ContainerToolRequestTests
{
    [Fact]
    public void BuildPausePath_UsesEscapedContainerInPostPath()
    {
        Assert.Equal("/containers/web%2Fapi/pause", ContainerToolRequests.BuildPausePath("web/api"));
    }

    [Fact]
    public void BuildUnpausePath_UsesEscapedContainerInPostPath()
    {
        Assert.Equal("/containers/web%2Fapi/unpause", ContainerToolRequests.BuildUnpausePath("web/api"));
    }

    [Fact]
    public void BuildStopPath_RejectsNegativeTimeout()
    {
        var exception = Assert.Throws<ContainerMcp.Models.ContainerMcpException>(
            () => ContainerToolRequests.BuildStopPath("web", -1));

        Assert.Equal("Argument 'timeoutSeconds' must be greater than or equal to 0.", exception.Message);
    }

    [Fact]
    public void BuildRestartPath_UsesEscapedContainerInPostPath()
    {
        Assert.Equal("/containers/web%2Fapi/restart", ContainerToolRequests.BuildRestartPath("web/api", null));
    }

    [Fact]
    public void BuildRestartPath_MapsTimeoutToDockerQuery()
    {
        Assert.Equal("/containers/web/restart?t=10", ContainerToolRequests.BuildRestartPath("web", 10));
    }

    [Fact]
    public void BuildRestartPath_RejectsNegativeTimeout()
    {
        var exception = Assert.Throws<ContainerMcp.Models.ContainerMcpException>(
            () => ContainerToolRequests.BuildRestartPath("web", -1));

        Assert.Equal("Argument 'timeoutSeconds' must be greater than or equal to 0.", exception.Message);
    }

    [Fact]
    public void BuildKillPath_UsesEscapedContainerInPostPath()
    {
        Assert.Equal("/containers/web%2Fapi/kill", ContainerToolRequests.BuildKillPath("web/api", null));
    }

    [Fact]
    public void BuildKillPath_MapsSignalToDockerQuery()
    {
        Assert.Equal("/containers/web/kill?signal=SIGTERM", ContainerToolRequests.BuildKillPath("web", "SIGTERM"));
    }

    [Fact]
    public void BuildKillPath_EscapesSignal()
    {
        Assert.Equal("/containers/web/kill?signal=SIG%20TERM", ContainerToolRequests.BuildKillPath("web", "SIG TERM"));
    }
}
