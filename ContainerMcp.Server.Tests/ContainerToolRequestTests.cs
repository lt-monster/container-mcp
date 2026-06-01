using ContainerMcp.Tools;

namespace ContainerMcp.Server.Tests;

public sealed class ContainerToolRequestTests
{
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
}
