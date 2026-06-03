using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using ContainerMcp.Models;
using ContainerMcp.Ports;

namespace ContainerMcp.Server.Tests;

public sealed class PortDiscoveryServiceTests
{
    [Fact]
    public void FindFree_ReturnsAvailableTcpPort()
    {
        var port = ReserveAndReleaseTcpPort();
        using var document = JsonDocument.Parse($$"""{"host":"127.0.0.1","start":{{port}},"end":{{port}},"protocol":"tcp"}""");

        var result = new PortDiscoveryService().FindFree(document.RootElement);

        var item = Assert.Single(result["items"]!.AsArray());
        Assert.Equal(port, item!["port"]!.GetValue<int>());
        Assert.Equal("tcp", item["protocol"]!.GetValue<string>());
    }

    [Fact]
    public void FindFree_ReturnsAvailableUdpPort()
    {
        var port = ReserveAndReleaseUdpPort();
        using var document = JsonDocument.Parse($$"""{"host":"127.0.0.1","start":{{port}},"end":{{port}},"protocol":"udp"}""");

        var result = new PortDiscoveryService().FindFree(document.RootElement);

        var item = Assert.Single(result["items"]!.AsArray());
        Assert.Equal(port, item!["port"]!.GetValue<int>());
        Assert.Equal("udp", item["protocol"]!.GetValue<string>());
    }

    [Fact]
    public void FindFree_RejectsCountLargerThanPortRangeCapacity()
    {
        using var document = JsonDocument.Parse("""{"start":41000,"end":41001,"count":3}""");

        var exception = Assert.Throws<ContainerMcpException>(() => new PortDiscoveryService().FindFree(document.RootElement));

        Assert.Equal(McpErrorCode.InvalidArgument, exception.ErrorCode);
    }

    [Fact]
    public void FindFree_IncludesDiagnosticsWhenRangeIsExhausted()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var document = JsonDocument.Parse($$"""{"host":"127.0.0.1","start":{{port}},"end":{{port}},"protocol":"tcp"}""");

        var exception = Assert.Throws<ContainerMcpException>(() => new PortDiscoveryService().FindFree(document.RootElement));

        Assert.Equal(McpErrorCode.PortRangeExhausted, exception.ErrorCode);
        Assert.NotNull(exception.Details);
        Assert.Equal("tcp", exception.Details!["protocol"]!.GetValue<string>());
        Assert.Equal(1, exception.Details["scanned"]!.GetValue<int>());
        Assert.Equal(0, exception.Details["found"]!.GetValue<int>());
        Assert.Contains(port, exception.Details["busyPorts"]!.AsArray().Select(node => node!.GetValue<int>()));
    }

    private static int ReserveAndReleaseTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static int ReserveAndReleaseUdpPort()
    {
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)client.Client.LocalEndPoint!).Port;
    }
}
