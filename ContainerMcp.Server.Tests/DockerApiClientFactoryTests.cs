using ContainerMcp.Configuration;
using ContainerMcp.ContainerRuntime;

namespace ContainerMcp.Server.Tests;

public sealed class DockerApiClientFactoryTests
{
    [Fact]
    public void GetClient_ReturnsSameInstanceForSameEndpoint()
    {
        using var factory = CreateFactory();
        var endpoint = DockerEndpoint(@"\\.\pipe\docker_engine");

        var first = factory.GetClient(endpoint);
        var second = factory.GetClient(endpoint);

        Assert.Same(first, second);
    }

    [Fact]
    public void GetClient_ReturnsDifferentInstancesForDifferentEndpoints()
    {
        using var factory = CreateFactory();

        var first = factory.GetClient(DockerEndpoint(@"\\.\pipe\docker_engine"));
        var second = factory.GetClient(DockerEndpoint(@"\\.\pipe\alternate_docker_engine"));

        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task GetClient_ReturnsSingleInstanceForConcurrentRequests()
    {
        using var factory = CreateFactory();
        var endpoint = DockerEndpoint(@"\\.\pipe\docker_engine");

        var clients = await Task.WhenAll(
            Enumerable.Range(0, 64).Select(_ => Task.Run(() => factory.GetClient(endpoint))));

        Assert.Single(clients.Distinct(ReferenceEqualityComparer.Instance));
    }

    [Fact]
    public void Dispose_DisposesCachedClients()
    {
        var factory = CreateFactory();
        var client = factory.GetClient(DockerEndpoint(@"\\.\pipe\docker_engine"));

        factory.Dispose();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/_ping");
        Assert.Throws<ObjectDisposedException>(() => client.Send(request));
    }

    [Theory]
    [InlineData("tcp://127.0.0.1:2375", "Tcp", "tcp://127.0.0.1:2375")]
    [InlineData("http://127.0.0.1:2375", "Tcp", "http://127.0.0.1:2375")]
    [InlineData("npipe://./pipe/docker_engine", "NamedPipe", @"\\.\pipe\docker_engine")]
    [InlineData("npipe:////./pipe/docker_engine", "NamedPipe", @"\\.\pipe\docker_engine")]
    public void TryParseHost_ParsesDockerDesktopEndpointValues(string host, string expectedKind, string expectedAddress)
    {
        var parsed = DockerApiClientFactory.TryParseHost(ContainerEngine.Docker, host, out var endpoint);

        Assert.True(parsed);
        Assert.Equal(ContainerEngine.Docker, endpoint.Engine);
        Assert.Equal(expectedKind, endpoint.Kind.ToString());
        Assert.Equal(expectedAddress, endpoint.Address);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ssh://docker-host")]
    [InlineData("127.0.0.1:2375")]
    public void TryParseHost_RejectsUnsupportedEndpointValues(string? host)
    {
        var parsed = DockerApiClientFactory.TryParseHost(ContainerEngine.Docker, host, out var endpoint);

        Assert.False(parsed);
        Assert.Null(endpoint);
    }

    [Fact]
    public void GetEndpoint_UsesDockerHostEnvironment()
    {
        using var environment = new EnvironmentScope().Set("DOCKER_HOST", "tcp://127.0.0.1:2375");
        using var factory = CreateFactory();

        var endpoint = factory.GetEndpoint(ContainerEngine.Docker);

        Assert.NotNull(endpoint);
        Assert.Equal(RuntimeEndpointKind.Tcp, endpoint.Kind);
        Assert.Equal("tcp://127.0.0.1:2375", endpoint.Address);
    }

    private static DockerApiClientFactory CreateFactory() =>
        new(ContainerMcpOptions.From([]));

    private static RuntimeEndpoint DockerEndpoint(string pipeName) =>
        new(ContainerEngine.Docker, RuntimeEndpointKind.NamedPipe, pipeName);

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
