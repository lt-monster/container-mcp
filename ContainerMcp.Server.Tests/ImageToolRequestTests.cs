using System.Text.Json;
using ContainerMcp.Tools;

namespace ContainerMcp.Server.Tests;

public sealed class ImageToolRequestTests
{
    [Fact]
    public void BuildInspectPath_UsesEscapedImageInGetPath()
    {
        Assert.Equal("/images/nginx%3Alatest/json", ImageToolRequests.BuildInspectPath("nginx:latest"));
    }

    [Fact]
    public void BuildTagPath_UsesEscapedPathAndQuery()
    {
        var path = ImageToolRequests.BuildTagPath("sha256:abc/def", "example.com/my image", "dev tag");

        Assert.Equal("/images/sha256%3Aabc%2Fdef/tag?repo=example.com%2Fmy%20image&tag=dev%20tag", path);
    }

    [Fact]
    public void BuildPrunePath_EncodesFilters()
    {
        using var document = JsonDocument.Parse(
            """{"dangling":false,"until":"24h","labels":["stage=dev"],"labelNe":["keep=true"]}""");

        var path = ImageToolRequests.BuildPrunePath(document.RootElement);

        Assert.StartsWith("/images/prune?filters=", path);
        var filters = Uri.UnescapeDataString(path["/images/prune?filters=".Length..]);
        Assert.Contains("\"dangling\":[\"false\"]", filters);
        Assert.Contains("\"until\":[\"24h\"]", filters);
        Assert.Contains("\"label\":[\"stage=dev\"]", filters);
        Assert.Contains("\"label!\":[\"keep=true\"]", filters);
    }

    [Fact]
    public void NormalizeMaxEvents_UsesDefaultsAndHardLimit()
    {
        Assert.Equal(500, ImageToolRequests.NormalizeMaxEvents(null));
        Assert.Equal(1, ImageToolRequests.NormalizeMaxEvents(-10));
        Assert.Equal(2000, ImageToolRequests.NormalizeMaxEvents(5000));
    }

    [Fact]
    public void BuildBuildPath_MapsArgumentsToDockerQuery()
    {
        using var document = JsonDocument.Parse(
            """{"tag":"app:dev","dockerfile":"docker/Dockerfile","noCache":true,"pull":true,"removeIntermediate":false,"forceRemoveIntermediate":true}""");

        var path = ImageToolRequests.BuildBuildPath(document.RootElement);

        Assert.Equal("/build?t=app%3Adev&rm=false&dockerfile=docker%2FDockerfile&nocache=true&pull=true&forcerm=true", path);
    }

    [Fact]
    public void BuildPushPath_MapsOptionalTagToQuery()
    {
        Assert.Equal("/images/registry.example.com%2Fapp/push?tag=dev", ImageToolRequests.BuildPushPath("registry.example.com/app", "dev"));
        Assert.Equal("/images/registry.example.com%2Fapp/push", ImageToolRequests.BuildPushPath("registry.example.com/app", null));
    }

    [Fact]
    public void BuildLoadPath_MapsQuietToQuery()
    {
        Assert.Equal("/images/load?quiet=true", ImageToolRequests.BuildLoadPath(true));
        Assert.Equal("/images/load?quiet=false", ImageToolRequests.BuildLoadPath(false));
    }

    [Fact]
    public void BuildSavePath_UsesEscapedImageInGetPath()
    {
        Assert.Equal("/images/nginx%3Alatest/get", ImageToolRequests.BuildSavePath("nginx:latest"));
    }

    [Fact]
    public void NormalizeMaxBytes_UsesDefaultsAndHardLimit()
    {
        Assert.Equal(1024L * 1024 * 1024, ImageToolRequests.NormalizeMaxBytes(null));
        Assert.Equal(1, ImageToolRequests.NormalizeMaxBytes(-10));
        Assert.Equal(8L * 1024 * 1024 * 1024, ImageToolRequests.NormalizeMaxBytes(long.MaxValue));
    }
}
