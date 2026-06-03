using System.Text.Json;
using ContainerMcp.Tools;

namespace ContainerMcp.Server.Tests;

public sealed class VolumeToolRequestTests
{
    [Fact]
    public void BuildInspectPath_UsesEscapedVolumeName()
    {
        Assert.Equal("/volumes/cache%2Fdata", VolumeToolRequests.BuildInspectPath("cache/data"));
    }

    [Fact]
    public void BuildPrunePath_EncodesFilters()
    {
        using var document = JsonDocument.Parse("""{"labels":["stage=dev"],"labelNe":["keep=true"]}""");

        var path = VolumeToolRequests.BuildPrunePath(document.RootElement);

        Assert.StartsWith("/volumes/prune?filters=", path);
        var filters = Uri.UnescapeDataString(path["/volumes/prune?filters=".Length..]);
        Assert.Contains("\"label\":[\"stage=dev\"]", filters);
        Assert.Contains("\"label!\":[\"keep=true\"]", filters);
    }

    [Fact]
    public void BuildPrunePath_OmitsFiltersWhenNoneAreProvided()
    {
        using var document = JsonDocument.Parse("{}");

        Assert.Equal("/volumes/prune", VolumeToolRequests.BuildPrunePath(document.RootElement));
    }

    [Fact]
    public void BuildCreateBody_IncludesDriverOptionsAndLabels()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "name": "cache",
              "driver": "local",
              "driverOptions": {
                "type": "tmpfs",
                "device": "tmpfs"
              },
              "labels": {
                "stage": "dev"
              }
            }
            """);

        var body = VolumeToolRequests.BuildCreateBody(document.RootElement, "cache");

        Assert.Equal("cache", body["Name"]!.GetValue<string>());
        Assert.Equal("local", body["Driver"]!.GetValue<string>());
        Assert.Equal("tmpfs", body["DriverOpts"]!["type"]!.GetValue<string>());
        Assert.Equal("tmpfs", body["DriverOpts"]!["device"]!.GetValue<string>());
        Assert.Equal("dev", body["Labels"]!["stage"]!.GetValue<string>());
    }
}
