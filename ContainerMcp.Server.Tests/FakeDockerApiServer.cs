using System.Net;
using System.Net.Sockets;
using System.Text;
using ContainerMcp.Configuration;
using ContainerMcp.ContainerRuntime;

namespace ContainerMcp.Server.Tests;

internal sealed class FakeDockerApiServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly Queue<Func<string, string>> _responses;
    private readonly List<string> _requests = [];
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _serverTask;

    private FakeDockerApiServer(TcpListener listener, IEnumerable<Func<string, string>> responses)
    {
        _listener = listener;
        _responses = new Queue<Func<string, string>>(responses);
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Host = $"tcp://127.0.0.1:{port}";
        Engine = new ResolvedEngine(ContainerEngine.Docker, new RuntimeEndpoint(ContainerEngine.Docker, RuntimeEndpointKind.Tcp, Host));
        _serverTask = RunAsync();
    }

    public string Host { get; }
    public ResolvedEngine Engine { get; }
    public IReadOnlyList<string> Requests => _requests;

    public static async Task<FakeDockerApiServer> StartAsync(params string[] responses)
    {
        return await StartAsync(responses.Select<string, Func<string, string>>(response => _ => response).ToArray());
    }

    public static async Task<FakeDockerApiServer> StartAsync(params Func<string, string>[] responses)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var server = new FakeDockerApiServer(listener, responses);
        await Task.Yield();
        return server;
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        try
        {
            await _serverTask;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _stop.Dispose();
        }
    }

    private async Task RunAsync()
    {
        while (!_stop.IsCancellationRequested && _responses.Count > 0)
        {
            using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
            await using var stream = client.GetStream();
            var request = await ReadRequestAsync(stream, _stop.Token);
            _requests.Add(request);
            var response = Encoding.ASCII.GetBytes(_responses.Dequeue()(request));
            await stream.WriteAsync(response, _stop.Token);
        }
    }

    private static async Task<string> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var builder = new StringBuilder();
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            builder.Append(Encoding.ASCII.GetString(buffer, 0, read));
            var text = builder.ToString();
            var headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0)
            {
                continue;
            }

            var contentLength = ReadContentLength(text);
            var totalLength = headerEnd + 4 + contentLength;
            if (Encoding.ASCII.GetByteCount(text) >= totalLength)
            {
                break;
            }
        }

        return builder.ToString();
    }

    private static int ReadContentLength(string request)
    {
        foreach (var line in request.Split("\r\n"))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(line["Content-Length:".Length..].Trim(), out var value))
            {
                return value;
            }
        }

        return 0;
    }
}
