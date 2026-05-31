using ContainerMcp.Configuration;

namespace ContainerMcp.Server.Tests;

public sealed class ContainerMcpOptionsTests
{
    [Fact]
    public void Options_DoNotExposeUnusedApiFirstConfiguration()
    {
        Assert.Null(typeof(ContainerMcpOptions).GetProperty("ApiFirst"));
    }
}
