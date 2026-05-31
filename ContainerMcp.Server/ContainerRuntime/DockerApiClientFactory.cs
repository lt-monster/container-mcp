using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using ContainerMcp.Configuration;

namespace ContainerMcp.ContainerRuntime;

internal enum RuntimeEndpointKind
{
    UnixSocket,
    NamedPipe,
    Tcp
}

internal sealed record RuntimeEndpoint(ContainerEngine Engine, RuntimeEndpointKind Kind, string Address)
{
    public string BaseUri => "http://localhost";
    public override string ToString() => $"{Kind}:{Address}";
}

internal sealed class DockerApiClientFactory : IDisposable
{
    private readonly ContainerMcpOptions _options;
    private readonly ConcurrentDictionary<string, Lazy<HttpClient>> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _probeLock = new();
    private readonly Dictionary<string, EndpointProbeCacheEntry> _probeCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public DockerApiClientFactory(ContainerMcpOptions options) => _options = options;

    public RuntimeEndpoint? GetEndpoint(ContainerEngine engine)
    {
        if (engine == ContainerEngine.Docker)
        {
            return GetDockerEndpoint();
        }

        if (engine == ContainerEngine.Podman)
        {
            return GetPodmanEndpoint();
        }

        return null;
    }

    public HttpClient GetClient(RuntimeEndpoint endpoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _clients.GetOrAdd(
            endpoint.ToString(),
            _ => new Lazy<HttpClient>(() => CreateClient(endpoint), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private HttpClient CreateClient(RuntimeEndpoint endpoint)
    {
        var timeout = Min(_options.ApiProbeTimeout, _options.ApiTimeout);
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                _ = context;
                return endpoint.Kind switch
                {
                    RuntimeEndpointKind.NamedPipe => await ConnectNamedPipeAsync(endpoint.Address, timeout, cancellationToken),
                    RuntimeEndpointKind.UnixSocket => await ConnectUnixSocketAsync(endpoint.Address, timeout, cancellationToken),
                    RuntimeEndpointKind.Tcp => await ConnectTcpAsync(endpoint.Address, timeout, cancellationToken),
                    _ => throw new NotSupportedException("Unsupported runtime endpoint.")
                };
            }
        };

        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri(endpoint.BaseUri),
            Timeout = _options.ApiTimeout
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var client in _clients.Values)
        {
            if (client.IsValueCreated)
            {
                client.Value.Dispose();
            }
        }

        _clients.Clear();
    }

    public async Task<bool> CanConnectAsync(RuntimeEndpoint endpoint, CancellationToken cancellationToken)
    {
        var cacheKey = endpoint.ToString();
        if (TryGetCachedProbe(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            var client = GetClient(endpoint);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.ApiProbeTimeout);
            using var response = await client.GetAsync("/_ping", timeout.Token).WaitAsync(_options.ApiProbeTimeout, cancellationToken);
            SetProbeCache(cacheKey, response.IsSuccessStatusCode, null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            SetProbeCache(cacheKey, false, ex.Message);
            return false;
        }
    }

    public string? GetLastProbeFailure(RuntimeEndpoint endpoint)
    {
        var cacheKey = endpoint.ToString();
        lock (_probeLock)
        {
            return _probeCache.TryGetValue(cacheKey, out var entry) && entry.ExpiresAtUtc >= DateTimeOffset.UtcNow
                ? entry.Error
                : null;
        }
    }

    private static RuntimeEndpoint? GetDockerEndpoint()
    {
        var host = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (TryParseHost(ContainerEngine.Docker, host, out var endpoint))
        {
            return endpoint;
        }

        if (OperatingSystem.IsWindows())
        {
            return new RuntimeEndpoint(ContainerEngine.Docker, RuntimeEndpointKind.NamedPipe, @"\\.\pipe\docker_engine");
        }

        return File.Exists("/var/run/docker.sock")
            ? new RuntimeEndpoint(ContainerEngine.Docker, RuntimeEndpointKind.UnixSocket, "/var/run/docker.sock")
            : null;
    }

    private static RuntimeEndpoint? GetPodmanEndpoint()
    {
        var host = Environment.GetEnvironmentVariable("CONTAINER_MCP_PODMAN_HOST")
            ?? Environment.GetEnvironmentVariable("PODMAN_HOST");
        if (TryParseHost(ContainerEngine.Podman, host, out var endpoint))
        {
            return endpoint;
        }

        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        var uid = Environment.GetEnvironmentVariable("UID");
        var candidates = string.IsNullOrWhiteSpace(uid)
            ? new[] { "/run/podman/podman.sock" }
            : new[] { $"/run/user/{uid}/podman/podman.sock", "/run/podman/podman.sock" };

        return candidates.FirstOrDefault(File.Exists) is { } socket
            ? new RuntimeEndpoint(ContainerEngine.Podman, RuntimeEndpointKind.UnixSocket, socket)
            : null;
    }

    private static bool TryParseHost(ContainerEngine engine, string? host, out RuntimeEndpoint endpoint)
    {
        endpoint = null!;
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (host.StartsWith("unix://", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = new RuntimeEndpoint(engine, RuntimeEndpointKind.UnixSocket, host["unix://".Length..]);
            return true;
        }

        if (host.StartsWith("npipe://", StringComparison.OrdinalIgnoreCase))
        {
            var pipe = host["npipe://".Length..].Replace('/', '\\');
            if (!pipe.StartsWith(@"\\", StringComparison.Ordinal))
            {
                pipe = @"\\" + pipe.TrimStart('\\');
            }

            endpoint = new RuntimeEndpoint(engine, RuntimeEndpointKind.NamedPipe, pipe);
            return true;
        }

        if (host.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase) || host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = new RuntimeEndpoint(engine, RuntimeEndpointKind.Tcp, host);
            return true;
        }

        return false;
    }

    private static async ValueTask<Stream> ConnectNamedPipeAsync(string pipePath, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var pipeName = pipePath.Replace(@"\\.\pipe\", string.Empty, StringComparison.OrdinalIgnoreCase);
        var stream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await stream.ConnectAsync(ToTimeoutMilliseconds(timeout), cancellationToken);
            return stream;
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    private static async ValueTask<Stream> ConnectUnixSocketAsync(string socketPath, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken).AsTask().WaitAsync(timeout, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static async ValueTask<Stream> ConnectTcpAsync(string host, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var uri = new Uri(host.Replace("tcp://", "http://", StringComparison.OrdinalIgnoreCase));
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(uri.Host, uri.Port, cancellationToken).AsTask().WaitAsync(timeout, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static int ToTimeoutMilliseconds(TimeSpan timeout) =>
        Math.Clamp((int)Math.Ceiling(timeout.TotalMilliseconds), 1, int.MaxValue);

    private static TimeSpan Min(TimeSpan left, TimeSpan right) =>
        left <= right ? left : right;

    private bool TryGetCachedProbe(string cacheKey, out bool available)
    {
        lock (_probeLock)
        {
            if (_probeCache.TryGetValue(cacheKey, out var entry) && entry.ExpiresAtUtc >= DateTimeOffset.UtcNow)
            {
                available = entry.Available;
                return true;
            }
        }

        available = false;
        return false;
    }

    private void SetProbeCache(string cacheKey, bool available, string? error)
    {
        lock (_probeLock)
        {
            _probeCache[cacheKey] = new EndpointProbeCacheEntry(
                available,
                error,
                DateTimeOffset.UtcNow.AddSeconds(available ? 2 : 10));
        }
    }
}

internal sealed record EndpointProbeCacheEntry(bool Available, string? Error, DateTimeOffset ExpiresAtUtc);
