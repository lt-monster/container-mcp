using System.Text.Json;
using ContainerMcp.Configuration;

namespace ContainerMcp.Server.Tests;

public sealed class HttpTokenGeneratorTests
{
    [Fact]
    public void Generate_AppendsTokenToNewConfigFile()
    {
        using var directory = TemporaryDirectory.Create();
        var configPath = Path.Combine(directory.Path, "container-mcp.config.json");

        var result = HttpTokenGenerator.Generate(new HttpTokenGenerateOptions(configPath, Count: 1, Id: "local-admin", Description: "Local admin"));

        Assert.Single(result.Tokens);
        Assert.StartsWith("cmcp_", result.Tokens[0]);
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        var token = document.RootElement.GetProperty("http").GetProperty("tokens")[0];
        Assert.Equal("local-admin", token.GetProperty("id").GetString());
        Assert.Equal(result.Tokens[0], token.GetProperty("value").GetString());
        Assert.True(token.GetProperty("enabled").GetBoolean());
        Assert.Equal("Local admin", token.GetProperty("description").GetString());
    }

    [Fact]
    public void Generate_PreservesExistingConfigAndAppendsMultipleTokens()
    {
        using var directory = TemporaryDirectory.Create();
        var configPath = Path.Combine(directory.Path, "container-mcp.config.json");
        File.WriteAllText(
            configPath,
            """
            {
              "version": 1,
              "transport": "stdio",
              "urls": "http://127.0.0.1:8123",
              "http": {
                "tokens": [
                  {
                    "id": "existing",
                    "value": "cmcp_existingabcdefghijklmnopqrstuvwxyzABCD",
                    "enabled": true
                  }
                ]
              }
            }
            """);

        var result = HttpTokenGenerator.Generate(new HttpTokenGenerateOptions(configPath, Count: 3, Id: "generated", Description: null));

        Assert.Equal(3, result.Tokens.Count);
        Assert.Equal(3, result.Tokens.Distinct(StringComparer.Ordinal).Count());
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.Equal("stdio", document.RootElement.GetProperty("transport").GetString());
        Assert.Equal("http://127.0.0.1:8123", document.RootElement.GetProperty("urls").GetString());
        Assert.Equal(4, document.RootElement.GetProperty("http").GetProperty("tokens").GetArrayLength());
    }

    [Fact]
    public void DefaultConfigPath_UsesAppBaseDirectory()
    {
        using var directory = TemporaryDirectory.Create();

        var path = HttpTokenGenerator.DefaultConfigPath(directory.Path);

        Assert.Equal(Path.Combine(directory.Path, "container-mcp.config.json"), path);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "container-mcp-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
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
