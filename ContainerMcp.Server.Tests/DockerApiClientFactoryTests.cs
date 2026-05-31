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

    private static DockerApiClientFactory CreateFactory() =>
        new(ContainerMcpOptions.From([]));

    private static RuntimeEndpoint DockerEndpoint(string pipeName) =>
        new(ContainerEngine.Docker, RuntimeEndpointKind.NamedPipe, pipeName);
}
