using ContainerMcp.Tools;
using System.Text.Json;

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
    public void BuildRenamePath_UsesEscapedContainerAndName()
    {
        Assert.Equal("/containers/web%2Fapi/rename?name=new%20web", ContainerToolRequests.BuildRenamePath("web/api", "new web"));
    }

    [Fact]
    public void BuildStatsPath_UsesStreamFalse()
    {
        Assert.Equal("/containers/web%2Fapi/stats?stream=false", ContainerToolRequests.BuildStatsPath("web/api"));
    }

    [Fact]
    public void BuildTopPath_MapsOptionalPsArgs()
    {
        Assert.Equal("/containers/web%2Fapi/top", ContainerToolRequests.BuildTopPath("web/api", null));
        Assert.Equal("/containers/web/top?ps_args=-ef%20wide", ContainerToolRequests.BuildTopPath("web", "-ef wide"));
    }

    [Theory]
    [InlineData(null, "/containers/web%2Fapi/wait?condition=not-running")]
    [InlineData("next-exit", "/containers/web%2Fapi/wait?condition=next-exit")]
    [InlineData("removed", "/containers/web%2Fapi/wait?condition=removed")]
    public void BuildWaitPath_MapsCondition(string? condition, string expected)
    {
        Assert.Equal(expected, ContainerToolRequests.BuildWaitPath("web/api", condition));
    }

    [Fact]
    public void BuildWaitPath_RejectsUnsupportedCondition()
    {
        var exception = Assert.Throws<ContainerMcp.Models.ContainerMcpException>(
            () => ContainerToolRequests.BuildWaitPath("web", "running"));

        Assert.Equal("Argument 'condition' must be one of: not-running, next-exit, removed.", exception.Message);
    }

    [Fact]
    public void NormalizeWaitTimeout_RejectsNegativeTimeout()
    {
        var exception = Assert.Throws<ContainerMcp.Models.ContainerMcpException>(
            () => ContainerToolRequests.NormalizeWaitTimeout(-1));

        Assert.Equal("Argument 'timeoutSeconds' must be greater than or equal to 0.", exception.Message);
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

    [Fact]
    public void BuildPrunePath_MapsFiltersToDockerQuery()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "until": "24h",
              "labels": ["app=web", "stage=dev"],
              "labelNe": ["keep=true"]
            }
            """);

        var path = ContainerToolRequests.BuildPrunePath(document.RootElement);

        Assert.StartsWith("/containers/prune?filters=", path);
        Assert.Contains("%22until%22%3A%5B%2224h%22%5D", path);
        Assert.Contains("%22label%22%3A%5B%22app%3Dweb%22%2C%22stage%3Ddev%22%5D", path);
        Assert.Contains("%22label%21%22%3A%5B%22keep%3Dtrue%22%5D", path);
    }

    [Fact]
    public void BuildPrunePath_OmitsFiltersWhenNoneAreProvided()
    {
        using var document = JsonDocument.Parse("{}");

        Assert.Equal("/containers/prune", ContainerToolRequests.BuildPrunePath(document.RootElement));
    }

    [Fact]
    public void BuildLogsPath_MapsFollowTailAndTimestamps()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "tail": "last 10",
              "timestamps": true
            }
            """);

        var path = ContainerToolRequests.BuildLogsPath("web/api", follow: true, document.RootElement);

        Assert.Equal("/containers/web%2Fapi/logs?stdout=true&stderr=true&follow=true&tail=last%2010&timestamps=true", path);
    }

    [Fact]
    public void BuildLogsPath_UsesFollowFalseForSnapshotLogs()
    {
        using var document = JsonDocument.Parse("{}");

        var path = ContainerToolRequests.BuildLogsPath("web", follow: false, document.RootElement);

        Assert.Equal("/containers/web/logs?stdout=true&stderr=true&follow=false", path);
    }
}
