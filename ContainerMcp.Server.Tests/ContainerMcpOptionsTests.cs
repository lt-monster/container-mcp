using ContainerMcp.Configuration;

namespace ContainerMcp.Server.Tests;

public sealed class ContainerMcpOptionsTests
{
    [Fact]
    public void Options_DoNotExposeUnusedApiFirstConfiguration()
    {
        Assert.Null(typeof(ContainerMcpOptions).GetProperty("ApiFirst"));
    }

    [Theory]
    [InlineData("http", "Http")]
    [InlineData("streamable-http", "Http")]
    [InlineData("stdio", "Stdio")]
    [InlineData("unknown", "Stdio")]
    public void From_ParsesTransportModes(string value, string expected)
    {
        var options = ContainerMcpOptions.From(["--transport", value]);

        Assert.Equal(expected, options.Transport.ToString());
    }

    [Fact]
    public void From_PrefersCommandLineOverEnvironment()
    {
        using var environment = new EnvironmentScope()
            .Set("CONTAINER_MCP_TRANSPORT", "stdio")
            .Set("CONTAINER_MCP_HTTP_URLS", "http://127.0.0.1:9000")
            .Set("ASPNETCORE_URLS", "http://127.0.0.1:9001")
            .Set("CONTAINER_MCP_DEFAULT_ENGINE", "podman")
            .Set("CONTAINER_MCP_DEFAULT_TARGET", "remote");

        var options = ContainerMcpOptions.From(
            [
                "--transport=http",
                "--urls", "http://127.0.0.1:7011",
                "--default-engine", "docker",
                "--default-target=local"
            ]);

        Assert.Equal(TransportMode.Http, options.Transport);
        Assert.Equal("http://127.0.0.1:7011", options.Urls);
        Assert.Equal(ContainerEngine.Docker, options.DefaultEngine);
        Assert.Equal("local", options.DefaultTarget);
    }

    [Fact]
    public void From_UsesEnvironmentFallbacks()
    {
        using var environment = new EnvironmentScope()
            .Set("CONTAINER_MCP_TRANSPORT", "stdio")
            .Set("CONTAINER_MCP_HTTP_URLS", "http://127.0.0.1:8000")
            .Set("ASPNETCORE_URLS", "http://127.0.0.1:8001")
            .Set("CONTAINER_MCP_DEFAULT_ENGINE", "docker")
            .Set("CONTAINER_MCP_DEFAULT_TARGET", "local");

        var options = ContainerMcpOptions.From([]);

        Assert.Equal(TransportMode.Stdio, options.Transport);
        Assert.Equal("http://127.0.0.1:8000", options.Urls);
        Assert.Equal(ContainerEngine.Docker, options.DefaultEngine);
        Assert.Equal("local", options.DefaultTarget);
    }

    [Fact]
    public void From_UsesAspNetCoreUrlsWhenContainerUrlsIsUnset()
    {
        using var environment = new EnvironmentScope()
            .Set("CONTAINER_MCP_HTTP_URLS", null)
            .Set("ASPNETCORE_URLS", "http://127.0.0.1:8010");

        var options = ContainerMcpOptions.From([]);

        Assert.Equal("http://127.0.0.1:8010", options.Urls);
    }

    [Fact]
    public void From_ClampsTimeoutsAndNormalizesOrdering()
    {
        var options = ContainerMcpOptions.From(
            [
                "--api-timeout-seconds", "700",
                "--api-probe-timeout-seconds", "700",
                "--tool-timeout-seconds", "5"
            ]);

        Assert.Equal(TimeSpan.FromSeconds(5), options.ToolTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), options.ApiTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), options.ApiProbeTimeout);
    }

    [Fact]
    public void From_ClampsTimeoutsToMinimum()
    {
        var options = ContainerMcpOptions.From(
            [
                "--api-timeout-seconds", "0",
                "--api-probe-timeout-seconds", "-1",
                "--tool-timeout-seconds", "0"
            ]);

        Assert.Equal(TimeSpan.FromSeconds(1), options.ToolTimeout);
        Assert.Equal(TimeSpan.FromSeconds(1), options.ApiTimeout);
        Assert.Equal(TimeSpan.FromSeconds(1), options.ApiProbeTimeout);
    }

    [Fact]
    public void ResolveRequestedEngine_UsesDefaultWhenMissing()
    {
        var options = ContainerMcpOptions.From(["--default-engine", "docker"]);

        Assert.Equal(ContainerEngine.Docker, options.ResolveRequestedEngine(null));
        Assert.Equal(ContainerEngine.Docker, options.ResolveRequestedEngine(""));
        Assert.Equal(ContainerEngine.Auto, options.ResolveRequestedEngine("auto"));
    }

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> _original = [];

        public EnvironmentScope Set(string name, string? value)
        {
            if (!_original.ContainsKey(name))
            {
                _original[name] = Environment.GetEnvironmentVariable(name);
            }

            Environment.SetEnvironmentVariable(name, value);
            return this;
        }

        public void Dispose()
        {
            foreach (var pair in _original)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }
    }
}
