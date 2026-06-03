using System.Text.Json;
using ContainerMcp.Tools;

namespace ContainerMcp.Server.Tests;

public sealed class NetworkToolRequestTests
{
    [Fact]
    public void BuildInspectPath_UsesEscapedNetworkName()
    {
        Assert.Equal("/networks/app%2Fnet", NetworkToolRequests.BuildInspectPath("app/net"));
    }

    [Fact]
    public void BuildRemovePath_UsesEscapedNetworkName()
    {
        Assert.Equal("/networks/app%2Fnet", NetworkToolRequests.BuildRemovePath("app/net"));
    }

    [Fact]
    public void BuildConnectPath_UsesEscapedNetworkName()
    {
        Assert.Equal("/networks/app%2Fnet/connect", NetworkToolRequests.BuildConnectPath("app/net"));
    }

    [Fact]
    public void BuildDisconnectPath_UsesEscapedNetworkName()
    {
        Assert.Equal("/networks/app%2Fnet/disconnect", NetworkToolRequests.BuildDisconnectPath("app/net"));
    }

    [Fact]
    public void BuildCreateBody_MapsSupportedFields()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "name": "app-net",
              "driver": "bridge",
              "internal": true,
              "attachable": true,
              "enableIPv6": true,
              "options": {
                "com.docker.network.bridge.name": "br-app"
              },
              "labels": {
                "stage": "dev"
              }
            }
            """);

        var body = NetworkToolRequests.BuildCreateBody(document.RootElement, "app-net");

        Assert.Equal("app-net", body["Name"]!.GetValue<string>());
        Assert.Equal("bridge", body["Driver"]!.GetValue<string>());
        Assert.True(body["Internal"]!.GetValue<bool>());
        Assert.True(body["Attachable"]!.GetValue<bool>());
        Assert.True(body["EnableIPv6"]!.GetValue<bool>());
        Assert.Equal("br-app", body["Options"]!["com.docker.network.bridge.name"]!.GetValue<string>());
        Assert.Equal("dev", body["Labels"]!["stage"]!.GetValue<string>());
    }

    [Fact]
    public void BuildConnectBody_MapsAliasesAndIpamConfig()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "container": "web",
              "aliases": ["web", "api"],
              "ipv4Address": "172.20.0.10",
              "ipv6Address": "fd00::10"
            }
            """);

        var body = NetworkToolRequests.BuildConnectBody(document.RootElement, "web");

        Assert.Equal("web", body["Container"]!.GetValue<string>());
        Assert.Equal("web", body["EndpointConfig"]!["Aliases"]![0]!.GetValue<string>());
        Assert.Equal("api", body["EndpointConfig"]!["Aliases"]![1]!.GetValue<string>());
        Assert.Equal("172.20.0.10", body["EndpointConfig"]!["IPAMConfig"]!["IPv4Address"]!.GetValue<string>());
        Assert.Equal("fd00::10", body["EndpointConfig"]!["IPAMConfig"]!["IPv6Address"]!.GetValue<string>());
    }

    [Fact]
    public void BuildDisconnectBody_MapsContainerAndForce()
    {
        using var document = JsonDocument.Parse("""{"container":"web","force":true}""");

        var body = NetworkToolRequests.BuildDisconnectBody(document.RootElement, "web");

        Assert.Equal("web", body["Container"]!.GetValue<string>());
        Assert.True(body["Force"]!.GetValue<bool>());
    }

    [Fact]
    public void BuildPrunePath_EncodesFilters()
    {
        using var document = JsonDocument.Parse("""{"until":"24h","labels":["stage=dev"],"labelNe":["keep=true"]}""");

        var path = NetworkToolRequests.BuildPrunePath(document.RootElement);

        Assert.StartsWith("/networks/prune?filters=", path);
        var filters = Uri.UnescapeDataString(path["/networks/prune?filters=".Length..]);
        Assert.Contains("\"until\":[\"24h\"]", filters);
        Assert.Contains("\"label\":[\"stage=dev\"]", filters);
        Assert.Contains("\"label!\":[\"keep=true\"]", filters);
    }

    [Fact]
    public void BuildPrunePath_OmitsFiltersWhenNoneAreProvided()
    {
        using var document = JsonDocument.Parse("{}");

        Assert.Equal("/networks/prune", NetworkToolRequests.BuildPrunePath(document.RootElement));
    }
}
