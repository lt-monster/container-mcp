using ContainerMcp.Configuration;

namespace ContainerMcp.Server.Tests;

public sealed class ProgramSupportTests
{
    [Theory]
    [InlineData("http://0.0.0.0:7010", true)]
    [InlineData("http://*:7010", true)]
    [InlineData("http://+:7010", true)]
    [InlineData("http://192.168.1.10:7010", true)]
    [InlineData("http://127.0.0.1:7010", false)]
    [InlineData("http://localhost:7010", false)]
    [InlineData("http://[::1]:7010", false)]
    [InlineData("http://127.0.0.1:7010;http://0.0.0.0:7011", true)]
    public void ProgramSupport_DetectsNonLoopbackHttpBindings(string urls, bool expected)
    {
        Assert.Equal(expected, ProgramSupport.HasNonLoopbackBinding(urls));
    }

    [Fact]
    public void ProgramSupport_BuildsWarningForNonLoopbackHttpBinding()
    {
        var options = ContainerMcpOptions.From(["--urls", "http://0.0.0.0:7010"]);

        var warning = ProgramSupport.BuildNonLoopbackWarning(options);

        Assert.Contains("non-loopback", warning);
        Assert.Contains("http://0.0.0.0:7010", warning);
    }
}
