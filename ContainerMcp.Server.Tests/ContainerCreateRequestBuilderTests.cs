using System.Text.Json;
using ContainerMcp.ContainerRuntime;
using ContainerMcp.Models;
using ContainerMcp.Tools;

namespace ContainerMcp.Server.Tests;

public sealed class ContainerCreateRequestBuilderTests
{
    [Fact]
    public void BuildCreatePath_MapsNameAndPlatformToQuery()
    {
        Assert.Equal("/containers/create", ContainerCreateRequestBuilder.BuildCreatePath(null, null));
        Assert.Equal("/containers/create?name=web%20one", ContainerCreateRequestBuilder.BuildCreatePath("web one", null));
        Assert.Equal("/containers/create?platform=linux%2Farm64", ContainerCreateRequestBuilder.BuildCreatePath(null, "linux/arm64"));
        Assert.Equal("/containers/create?name=web&platform=linux%2Famd64", ContainerCreateRequestBuilder.BuildCreatePath("web", "linux/amd64"));
    }

    [Fact]
    public void Build_MapsBasicCreateOptions()
    {
        using var document = JsonDocument.Parse(
            """{"workingDir":"/app","user":"1000:1000","hostname":"web-1","tty":true,"entrypoint":["/bin/sh","-c"]}""");
        var builder = new ContainerCreateRequestBuilder(new VolumePolicy());

        var body = builder.Build(document.RootElement, "nginx");

        Assert.Equal("/app", body["WorkingDir"]!.GetValue<string>());
        Assert.Equal("1000:1000", body["User"]!.GetValue<string>());
        Assert.Equal("web-1", body["Hostname"]!.GetValue<string>());
        Assert.True(body["Tty"]!.GetValue<bool>());
        Assert.Equal("/bin/sh", body["Entrypoint"]![0]!.GetValue<string>());
        Assert.Equal("-c", body["Entrypoint"]![1]!.GetValue<string>());
    }

    [Fact]
    public void Build_MapsStringEntrypointAsSingleElementArray()
    {
        using var document = JsonDocument.Parse("""{"entrypoint":"/entrypoint.sh"}""");
        var builder = new ContainerCreateRequestBuilder(new VolumePolicy());

        var body = builder.Build(document.RootElement, "nginx");

        Assert.Equal("/entrypoint.sh", body["Entrypoint"]![0]!.GetValue<string>());
    }

    [Fact]
    public void Build_MapsNetworkModeAndResourceLimits()
    {
        using var document = JsonDocument.Parse(
            """{"networkMode":"host","memoryBytes":268435456,"memorySwapBytes":-1,"memoryReservationBytes":134217728,"cpuShares":512,"cpuQuota":50000,"cpuPeriod":100000,"nanoCpus":1000000000,"pidsLimit":-1}""");
        var builder = new ContainerCreateRequestBuilder(new VolumePolicy());

        var body = builder.Build(document.RootElement, "nginx");
        var hostConfig = body["HostConfig"]!;

        Assert.Equal("host", hostConfig["NetworkMode"]!.GetValue<string>());
        Assert.Equal(268435456L, hostConfig["Memory"]!.GetValue<long>());
        Assert.Equal(-1L, hostConfig["MemorySwap"]!.GetValue<long>());
        Assert.Equal(134217728L, hostConfig["MemoryReservation"]!.GetValue<long>());
        Assert.Equal(512, hostConfig["CpuShares"]!.GetValue<int>());
        Assert.Equal(50000, hostConfig["CpuQuota"]!.GetValue<int>());
        Assert.Equal(100000, hostConfig["CpuPeriod"]!.GetValue<int>());
        Assert.Equal(1000000000L, hostConfig["NanoCpus"]!.GetValue<long>());
        Assert.Equal(-1L, hostConfig["PidsLimit"]!.GetValue<long>());
    }

    [Fact]
    public void Build_MapsHealthcheck()
    {
        using var document = JsonDocument.Parse(
            """{"healthcheck":{"test":["CMD-SHELL","curl -f http://localhost || exit 1"],"intervalNanoseconds":1000000000,"timeoutNanoseconds":2000000000,"startPeriodNanoseconds":3000000000,"retries":3}}""");
        var builder = new ContainerCreateRequestBuilder(new VolumePolicy());

        var body = builder.Build(document.RootElement, "nginx");
        var healthcheck = body["Healthcheck"]!;

        Assert.Equal("CMD-SHELL", healthcheck["Test"]![0]!.GetValue<string>());
        Assert.Equal("curl -f http://localhost || exit 1", healthcheck["Test"]![1]!.GetValue<string>());
        Assert.Equal(1000000000L, healthcheck["Interval"]!.GetValue<long>());
        Assert.Equal(2000000000L, healthcheck["Timeout"]!.GetValue<long>());
        Assert.Equal(3000000000L, healthcheck["StartPeriod"]!.GetValue<long>());
        Assert.Equal(3, healthcheck["Retries"]!.GetValue<int>());
    }

    [Fact]
    public void Build_MapsCommandEnvLabelsVolumesAndPorts()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "command": "echo hello",
              "env": {
                "ASPNETCORE_ENVIRONMENT": "Development"
              },
              "labels": {
                "app": "demo"
              },
              "volumes": [
                "cache:/data:ro"
              ],
              "ports": [
                "127.0.0.1:8080:80/tcp",
                "53/udp"
              ]
            }
            """);
        var builder = new ContainerCreateRequestBuilder(new VolumePolicy());

        var body = builder.Build(document.RootElement, "nginx");
        var hostConfig = body["HostConfig"]!;

        Assert.Equal("echo hello", body["Cmd"]![0]!.GetValue<string>());
        Assert.Equal("ASPNETCORE_ENVIRONMENT=Development", body["Env"]![0]!.GetValue<string>());
        Assert.Equal("demo", body["Labels"]!["app"]!.GetValue<string>());
        Assert.Equal("cache:/data:ro", hostConfig["Binds"]![0]!.GetValue<string>());
        Assert.Equal("127.0.0.1", hostConfig["PortBindings"]!["80/tcp"]![0]!["HostIp"]!.GetValue<string>());
        Assert.Equal("8080", hostConfig["PortBindings"]!["80/tcp"]![0]!["HostPort"]!.GetValue<string>());
        Assert.NotNull(hostConfig["PortBindings"]!["53/udp"]![0]);
        Assert.NotNull(body["ExposedPorts"]!["80/tcp"]);
        Assert.NotNull(body["ExposedPorts"]!["53/udp"]);
    }

    [Fact]
    public void Build_MapsObjectPortBindings()
    {
        using var document = JsonDocument.Parse("""{"ports":{"80/tcp":8080,"443":"8443"}}""");
        var builder = new ContainerCreateRequestBuilder(new VolumePolicy());

        var body = builder.Build(document.RootElement, "nginx");
        var portBindings = body["HostConfig"]!["PortBindings"]!;

        Assert.Equal("8080", portBindings["80/tcp"]![0]!["HostPort"]!.GetValue<string>());
        Assert.Equal("8443", portBindings["443/tcp"]![0]!["HostPort"]!.GetValue<string>());
        Assert.NotNull(body["ExposedPorts"]!["443/tcp"]);
    }

    [Fact]
    public void Build_OmitsEmptyHealthcheck()
    {
        using var document = JsonDocument.Parse("""{"healthcheck":{}}""");
        var builder = new ContainerCreateRequestBuilder(new VolumePolicy());

        var body = builder.Build(document.RootElement, "nginx");

        Assert.Null(body["Healthcheck"]);
    }

    [Fact]
    public void Build_RejectsHostBindMounts()
    {
        using var document = JsonDocument.Parse("""{"volumes":["C:\\host:/data"]}""");
        var builder = new ContainerCreateRequestBuilder(new VolumePolicy());

        var exception = Assert.Throws<ContainerMcpException>(
            () => builder.Build(document.RootElement, "nginx"));

        Assert.Equal(McpErrorCode.UnsupportedVolumeMount, exception.ErrorCode);
    }

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
    [InlineData("""{"memoryBytes":-1}""", "Argument 'memoryBytes' must be greater than or equal to 0.")]
    [InlineData("""{"cpuShares":-1}""", "Argument 'cpuShares' must be greater than or equal to 0.")]
    [InlineData("""{"healthcheck":{"intervalNanoseconds":999999}}""", "Argument 'healthcheck.intervalNanoseconds' must be 0 or at least 1000000.")]
    [InlineData("""{"healthcheck":{"retries":-1}}""", "Argument 'healthcheck.retries' must be greater than or equal to 0.")]
    public void Build_RejectsInvalidExtendedOptions(string json, string expectedMessage)
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
