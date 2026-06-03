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
    public void From_UsesExplicitConfigFile()
    {
        using var directory = TemporaryDirectory.Create();
        var configPath = directory.WriteConfig(
            """
            {
              "version": 1,
              "transport": "stdio",
              "urls": "http://127.0.0.1:8123",
              "defaultEngine": "docker",
              "defaultTarget": "local",
              "timeouts": {
                "toolSeconds": 31,
                "apiSeconds": 21,
                "apiProbeSeconds": 3
              },
              "http": {
                "maxRequestBodyBytes": 2048,
                "tokens": [
                  {
                    "id": "enabled",
                    "value": "cmcp_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMN",
                    "enabled": true,
                    "createdAt": "2026-06-03T00:00:00Z",
                    "description": "enabled token"
                  },
                  {
                    "id": "disabled",
                    "value": "cmcp_DISABLEDabcdefghijklmnopqrstuvwxyzABCDEF",
                    "enabled": false
                  }
                ]
              }
            }
            """);

        var options = ContainerMcpOptions.From(["--config", configPath], appBaseDirectory: directory.Path);

        Assert.Equal(TransportMode.Stdio, options.Transport);
        Assert.Equal("http://127.0.0.1:8123", options.Urls);
        Assert.Equal(ContainerEngine.Docker, options.DefaultEngine);
        Assert.Equal(TimeSpan.FromSeconds(31), options.ToolTimeout);
        Assert.Equal(TimeSpan.FromSeconds(21), options.ApiTimeout);
        Assert.Equal(TimeSpan.FromSeconds(3), options.ApiProbeTimeout);
        Assert.Equal(2048, options.MaxHttpRequestBodyBytes);
        Assert.Single(options.HttpTokens);
        Assert.Equal("enabled", options.HttpTokens[0].Id);
    }

    [Fact]
    public void From_UsesDefaultConfigFileFromAppBaseDirectory()
    {
        using var directory = TemporaryDirectory.Create();
        directory.WriteDefaultConfig("""{"version":1,"urls":"http://127.0.0.1:8124","defaultEngine":"docker"}""");

        var options = ContainerMcpOptions.From([], appBaseDirectory: directory.Path);

        Assert.Equal("http://127.0.0.1:8124", options.Urls);
        Assert.Equal(ContainerEngine.Docker, options.DefaultEngine);
    }

    [Fact]
    public void From_UsesConfigPathFromEnvironment()
    {
        using var directory = TemporaryDirectory.Create();
        var configPath = directory.WriteConfig("""{"version":1,"urls":"http://127.0.0.1:8127"}""");
        using var environment = new EnvironmentScope()
            .Set("CONTAINER_MCP_CONFIG", configPath);

        var options = ContainerMcpOptions.From([], appBaseDirectory: directory.Path);

        Assert.Equal("http://127.0.0.1:8127", options.Urls);
    }

    [Fact]
    public void From_ThrowsForInvalidEnabledHttpToken()
    {
        using var directory = TemporaryDirectory.Create();
        var configPath = directory.WriteConfig(
            """
            {
              "version": 1,
              "http": {
                "tokens": [
                  {
                    "id": "weak",
                    "value": "changeme",
                    "enabled": true
                  }
                ]
              }
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(
            () => ContainerMcpOptions.From(["--config", configPath], appBaseDirectory: directory.Path));

        Assert.Contains("Invalid HTTP bearer token", exception.Message);
        Assert.Contains("weak", exception.Message);
    }

    [Fact]
    public void From_IgnoresMissingDefaultConfigFile()
    {
        using var directory = TemporaryDirectory.Create();

        var options = ContainerMcpOptions.From([], appBaseDirectory: directory.Path);

        Assert.Equal("http://127.0.0.1:7010", options.Urls);
    }

    [Fact]
    public void From_ThrowsForMissingExplicitConfigFile()
    {
        using var directory = TemporaryDirectory.Create();
        var missingPath = Path.Combine(directory.Path, "missing.json");

        var exception = Assert.Throws<InvalidOperationException>(
            () => ContainerMcpOptions.From(["--config", missingPath], appBaseDirectory: directory.Path));

        Assert.Contains("Configuration file not found", exception.Message);
    }

    [Fact]
    public void From_UsesCliThenEnvironmentThenConfigThenDefaults()
    {
        using var directory = TemporaryDirectory.Create();
        var configPath = directory.WriteConfig(
            """
            {
              "version": 1,
              "transport": "stdio",
              "urls": "http://127.0.0.1:8125",
              "defaultEngine": "podman",
              "defaultTarget": "config-target",
              "timeouts": {
                "toolSeconds": 44,
                "apiSeconds": 33,
                "apiProbeSeconds": 22
              }
            }
            """);
        using var environment = new EnvironmentScope()
            .Set("CONTAINER_MCP_TRANSPORT", "http")
            .Set("CONTAINER_MCP_DEFAULT_ENGINE", "docker")
            .Set("CONTAINER_MCP_API_TIMEOUT_SECONDS", "11");

        var options = ContainerMcpOptions.From(
            ["--config", configPath, "--urls", "http://127.0.0.1:8126"],
            appBaseDirectory: directory.Path);

        Assert.Equal(TransportMode.Http, options.Transport);
        Assert.Equal("http://127.0.0.1:8126", options.Urls);
        Assert.Equal(ContainerEngine.Docker, options.DefaultEngine);
        Assert.Equal("config-target", options.DefaultTarget);
        Assert.Equal(TimeSpan.FromSeconds(11), options.ApiTimeout);
        Assert.Equal(TimeSpan.FromSeconds(11), options.ApiProbeTimeout);
        Assert.Equal(TimeSpan.FromSeconds(44), options.ToolTimeout);
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

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create() =>
            new(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "container-mcp-tests-" + Guid.NewGuid().ToString("N")));

        public string WriteConfig(string json)
        {
            Directory.CreateDirectory(Path);
            var configPath = System.IO.Path.Combine(Path, "config.json");
            File.WriteAllText(configPath, json);
            return configPath;
        }

        public string WriteDefaultConfig(string json)
        {
            Directory.CreateDirectory(Path);
            var configPath = System.IO.Path.Combine(Path, "container-mcp.config.json");
            File.WriteAllText(configPath, json);
            return configPath;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
