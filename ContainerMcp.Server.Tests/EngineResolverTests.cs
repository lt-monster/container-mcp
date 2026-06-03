using System.Net;
using System.Net.Sockets;
using System.Text;
using ContainerMcp.Configuration;
using ContainerMcp.ContainerRuntime;
using ContainerMcp.Models;

namespace ContainerMcp.Server.Tests;

public sealed class EngineResolverTests
{
    [Fact]
    public async Task ResolveAsync_RejectsNonLocalTarget()
    {
        using var factory = new DockerApiClientFactory(ContainerMcpOptions.From([]));
        var resolver = new EngineResolver(factory);

        var exception = await Assert.ThrowsAsync<ContainerMcpException>(
            () => resolver.ResolveAsync(ContainerEngine.Docker, "remote", CancellationToken.None));

        Assert.Equal(McpErrorCode.UnsupportedTarget, exception.ErrorCode);
        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsRequestedDockerWhenEndpointRespondsToPing()
    {
        await using var server = await PingServer.StartAsync(success: true);
        using var environment = new EnvironmentScope().Set("DOCKER_HOST", server.Host);
        using var factory = new DockerApiClientFactory(ContainerMcpOptions.From(["--api-probe-timeout-seconds", "1"]));
        var resolver = new EngineResolver(factory);

        var engine = await resolver.ResolveAsync(ContainerEngine.Docker, "local", CancellationToken.None);

        Assert.Equal(ContainerEngine.Docker, engine.Engine);
        Assert.Equal(RuntimeEndpointKind.Tcp, engine.Endpoint.Kind);
        Assert.Equal(server.Host, engine.Endpoint.Address);
        Assert.Contains("GET /_ping HTTP/1.1", server.RequestText);
    }

    [Fact]
    public async Task ResolveAsync_AutoSelectsDockerWhenDockerResponds()
    {
        await using var server = await PingServer.StartAsync(success: true);
        using var environment = new EnvironmentScope().Set("DOCKER_HOST", server.Host);
        using var factory = new DockerApiClientFactory(ContainerMcpOptions.From(["--api-probe-timeout-seconds", "1"]));
        var resolver = new EngineResolver(factory);

        var engine = await resolver.ResolveAsync(ContainerEngine.Auto, "local", CancellationToken.None);

        Assert.Equal(ContainerEngine.Docker, engine.Engine);
        Assert.Equal(server.Host, engine.Endpoint.Address);
    }

    [Fact]
    public async Task ResolveAsync_ReportsUnavailableDockerProbeFailure()
    {
        await using var server = await PingServer.StartAsync(success: false);
        using var environment = new EnvironmentScope().Set("DOCKER_HOST", server.Host);
        using var factory = new DockerApiClientFactory(ContainerMcpOptions.From(["--api-probe-timeout-seconds", "1"]));
        var resolver = new EngineResolver(factory);

        var exception = await Assert.ThrowsAsync<ContainerMcpException>(
            () => resolver.ResolveAsync(ContainerEngine.Docker, "local", CancellationToken.None));

        Assert.Equal(McpErrorCode.EngineUnavailable, exception.ErrorCode);
        Assert.Equal(503, exception.StatusCode);
        Assert.Contains(server.Host, exception.Endpoint);
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

    private sealed class PingServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _serverTask;
        private readonly bool _success;

        private PingServer(TcpListener listener, bool success)
        {
            _listener = listener;
            _success = success;
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Host = $"tcp://127.0.0.1:{port}";
            _serverTask = RunAsync();
        }

        public string Host { get; }
        public string RequestText { get; private set; } = string.Empty;

        public static async Task<PingServer> StartAsync(bool success)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var server = new PingServer(listener, success);
            await Task.Yield();
            return server;
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            await _serverTask;
        }

        private async Task RunAsync()
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync();
                await using var stream = client.GetStream();
                var buffer = new byte[4096];
                var read = await stream.ReadAsync(buffer);
                RequestText = Encoding.ASCII.GetString(buffer, 0, read);
                var status = _success ? "200 OK" : "503 Service Unavailable";
                var response = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Length: 2\r\n\r\nOK");
                await stream.WriteAsync(response);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
        }
    }
}
