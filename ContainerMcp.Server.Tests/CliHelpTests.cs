using ContainerMcp.Configuration;
using ContainerMcp.Mcp;

namespace ContainerMcp.Server.Tests;

public sealed class CliHelpTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("token", "generate", "--help")]
    public void IsHelpRequested_DetectsHelpArguments(params string[] args)
    {
        Assert.True(CliHelp.IsHelpRequested(args));
    }

    [Fact]
    public void IsVersionRequested_DetectsVersionArgument()
    {
        Assert.True(CliHelp.IsVersionRequested(["--version"]));
    }

    [Fact]
    public void BuildHelp_IncludesTransportsTokenGenerateAndCommonOptions()
    {
        var help = CliHelp.BuildHelp();

        Assert.Contains("container-mcp", help);
        Assert.Contains("--transport <http|stdio>", help);
        Assert.Contains("--urls <url>", help);
        Assert.Contains("token generate", help);
        Assert.Contains("--config <path>", help);
        Assert.Contains("--default-engine <auto|docker|podman>", help);
        Assert.Contains("--default-target <target>", help);
        Assert.Contains("--api-timeout-seconds <seconds>", help);
        Assert.Contains("--api-probe-timeout-seconds <seconds>", help);
        Assert.Contains("--tool-timeout-seconds <seconds>", help);
        Assert.Contains("--http-max-request-body-bytes <bytes>", help);
    }

    [Fact]
    public void BuildVersion_ReturnsCurrentServerVersion()
    {
        Assert.Equal($"container-mcp {ServerVersion.Current}", CliHelp.BuildVersion());
    }

    [Fact]
    public void CurrentServerVersion_MatchesReleaseVersion()
    {
        Assert.Equal("1.0.1", ServerVersion.Current);
    }
}
