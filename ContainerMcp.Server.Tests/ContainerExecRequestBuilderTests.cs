using System.Text.Json;
using ContainerMcp.Tools;

namespace ContainerMcp.Server.Tests;

public sealed class ContainerExecRequestBuilderTests
{
    [Fact]
    public void BuildCreatePath_UsesEscapedContainerInPostPath()
    {
        Assert.Equal("/containers/web%2Fapi/exec", ContainerExecRequestBuilder.BuildCreatePath("web/api"));
    }

    [Fact]
    public void BuildStartPath_UsesEscapedExecIdInPostPath()
    {
        Assert.Equal("/exec/exec%2F1/start", ContainerExecRequestBuilder.BuildStartPath("exec/1"));
    }

    [Fact]
    public void BuildCreateBody_MapsStringCommandAndDefaults()
    {
        using var document = JsonDocument.Parse("""{"command":"date"}""");

        var body = ContainerExecRequestBuilder.BuildCreateBody(document.RootElement);

        Assert.Equal("date", body["Cmd"]![0]!.GetValue<string>());
        Assert.False(body["AttachStdin"]!.GetValue<bool>());
        Assert.True(body["AttachStdout"]!.GetValue<bool>());
        Assert.True(body["AttachStderr"]!.GetValue<bool>());
        Assert.False(body["Tty"]!.GetValue<bool>());
    }

    [Fact]
    public void BuildCreateBody_MapsArrayCommandAndOptionalFields()
    {
        using var document = JsonDocument.Parse(
            """{"command":["sh","-c","echo hi"],"env":{"A":"1","B":"two"},"user":"1000","workingDir":"/app","tty":true,"attachStdout":false,"attachStderr":false}""");

        var body = ContainerExecRequestBuilder.BuildCreateBody(document.RootElement);

        Assert.Equal("sh", body["Cmd"]![0]!.GetValue<string>());
        Assert.Equal("-c", body["Cmd"]![1]!.GetValue<string>());
        Assert.Equal("echo hi", body["Cmd"]![2]!.GetValue<string>());
        Assert.Equal("A=1", body["Env"]![0]!.GetValue<string>());
        Assert.Equal("B=two", body["Env"]![1]!.GetValue<string>());
        Assert.Equal("1000", body["User"]!.GetValue<string>());
        Assert.Equal("/app", body["WorkingDir"]!.GetValue<string>());
        Assert.True(body["Tty"]!.GetValue<bool>());
        Assert.False(body["AttachStdout"]!.GetValue<bool>());
        Assert.False(body["AttachStderr"]!.GetValue<bool>());
    }

    [Fact]
    public void BuildStartBody_MapsDetachAndTty()
    {
        var body = ContainerExecRequestBuilder.BuildStartBody(tty: true);

        Assert.False(body["Detach"]!.GetValue<bool>());
        Assert.True(body["Tty"]!.GetValue<bool>());
    }

    [Fact]
    public void NormalizeMaxBytes_UsesDefaultsAndHardLimit()
    {
        Assert.Equal(1024 * 1024, ContainerExecRequestBuilder.NormalizeMaxBytes(null));
        Assert.Equal(1, ContainerExecRequestBuilder.NormalizeMaxBytes(-10));
        Assert.Equal(4 * 1024 * 1024, ContainerExecRequestBuilder.NormalizeMaxBytes(int.MaxValue));
    }
}
