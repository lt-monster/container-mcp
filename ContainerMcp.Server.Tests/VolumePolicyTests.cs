using ContainerMcp.ContainerRuntime;
using ContainerMcp.Models;

namespace ContainerMcp.Server.Tests;

public sealed class VolumePolicyTests
{
    [Theory]
    [InlineData("cache:/data")]
    [InlineData("cache:/data:ro")]
    [InlineData("named-volume:/var/lib/app:rw")]
    public void ValidateContainerCreateVolumes_AcceptsNamedVolumes(string volume)
    {
        var policy = new VolumePolicy();

        var result = policy.ValidateContainerCreateVolumes([volume]);

        Assert.Equal(volume, result[0]);
    }

    [Theory]
    [InlineData("/host:/container")]
    [InlineData("~/host:/container")]
    [InlineData("./host:/container")]
    [InlineData("../host:/container")]
    [InlineData("C:\\host:/container")]
    [InlineData(@"\\server\share:/container")]
    [InlineData(":/container")]
    [InlineData(".:/container")]
    public void ValidateContainerCreateVolumes_RejectsHostPaths(string volume)
    {
        var policy = new VolumePolicy();

        var exception = Assert.Throws<ContainerMcpException>(
            () => policy.ValidateContainerCreateVolumes([volume]));

        Assert.Equal(McpErrorCode.UnsupportedVolumeMount, exception.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("cache")]
    public void ValidateContainerCreateVolumes_RejectsInvalidMountSyntax(string volume)
    {
        var policy = new VolumePolicy();

        var exception = Assert.Throws<ContainerMcpException>(
            () => policy.ValidateContainerCreateVolumes([volume]));

        Assert.Equal(McpErrorCode.InvalidArgument, exception.ErrorCode);
        Assert.Equal("Volume mounts must use source:target[:mode].", exception.Message);
    }
}
