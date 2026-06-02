using System.Net;
using System.Net.Sockets;
using System.Text;
using ContainerMcp.Configuration;
using ContainerMcp.ContainerRuntime;
using ContainerMcp.Models;

namespace ContainerMcp.Server.Tests;

public sealed class ContainerApiAdapterTests
{
    [Fact]
    public async Task PostBytesAsync_ReadsBoundedResponseBytes()
    {
        await using var server = await FakeHttpServer.StartAsync("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello");
        var adapter = new ContainerApiAdapter(new DockerApiClientFactory(ContainerMcpOptions.From([])), ContainerMcpOptions.From([]));

        var bytes = await adapter.PostBytesAsync(server.Engine, "/exec/abc/start", null, maxBytes: 3, CancellationToken.None);

        Assert.Equal("hel", Encoding.UTF8.GetString(bytes));
        Assert.Contains("POST /exec/abc/start HTTP/1.1", server.RequestText);
    }

    [Fact]
    public async Task PostBytesAsync_MapsNonSuccessResponse()
    {
        await using var server = await FakeHttpServer.StartAsync("HTTP/1.1 404 Not Found\r\nContent-Length: 31\r\n\r\n{\"message\":\"No such container\"}");
        var adapter = new ContainerApiAdapter(new DockerApiClientFactory(ContainerMcpOptions.From([])), ContainerMcpOptions.From([]));

        var exception = await Assert.ThrowsAsync<ContainerMcpException>(
            () => adapter.PostBytesAsync(server.Engine, "/containers/missing/exec", null, maxBytes: 1024, CancellationToken.None));

        Assert.Equal(McpErrorCode.ContainerNotFound, exception.ErrorCode);
        Assert.Equal("No such container", exception.Message);
    }

    [Fact]
    public async Task GetBytesForDurationAsync_ReturnsBytesWhenDurationElapses()
    {
        await using var server = await FakeHttpServer.StartStreamingAsync(async stream =>
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n\r\nhello"));
            await stream.FlushAsync();
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        });
        var adapter = new ContainerApiAdapter(new DockerApiClientFactory(ContainerMcpOptions.From([])), ContainerMcpOptions.From([]));

        var result = await adapter.GetBytesForDurationAsync(server.Engine, "/containers/web/logs?follow=true", maxBytes: 1024, TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.Equal("hello", Encoding.UTF8.GetString(result.Bytes));
        Assert.Equal("duration", result.CompletedBy);
        Assert.Contains("GET /containers/web/logs?follow=true HTTP/1.1", server.RequestText);
    }

    [Fact]
    public async Task GetBytesForDurationAsync_StopsAtMaxBytes()
    {
        await using var server = await FakeHttpServer.StartAsync("HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\nabcdef");
        var adapter = new ContainerApiAdapter(new DockerApiClientFactory(ContainerMcpOptions.From([])), ContainerMcpOptions.From([]));

        var result = await adapter.GetBytesForDurationAsync(server.Engine, "/containers/web/logs?follow=true", maxBytes: 3, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal("abc", Encoding.UTF8.GetString(result.Bytes));
        Assert.Equal("maxBytes", result.CompletedBy);
    }

    private sealed class FakeHttpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private Task _serverTask;

        private FakeHttpServer(TcpListener listener, Task serverTask, ResolvedEngine engine)
        {
            _listener = listener;
            _serverTask = serverTask;
            Engine = engine;
        }

        public ResolvedEngine Engine { get; }
        public string RequestText { get; private set; } = string.Empty;

        public static async Task<FakeHttpServer> StartAsync(string response)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var server = new FakeHttpServer(
                listener,
                Task.CompletedTask,
                new ResolvedEngine(ContainerEngine.Docker, new RuntimeEndpoint(ContainerEngine.Docker, RuntimeEndpointKind.Tcp, $"tcp://127.0.0.1:{port}")));

            server._serverTask = server.RunAsync(response);
            await Task.Yield();
            return server;
        }

        public static async Task<FakeHttpServer> StartStreamingAsync(Func<Stream, Task> writeResponseAsync)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var server = new FakeHttpServer(
                listener,
                Task.CompletedTask,
                new ResolvedEngine(ContainerEngine.Docker, new RuntimeEndpoint(ContainerEngine.Docker, RuntimeEndpointKind.Tcp, $"tcp://127.0.0.1:{port}")));

            server._serverTask = server.RunStreamingAsync(writeResponseAsync);
            await Task.Yield();
            return server;
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            await _serverTask;
        }

        private async Task RunAsync(string response)
        {
            using var client = await _listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var buffer = new byte[4096];
            var read = await stream.ReadAsync(buffer);
            RequestText = Encoding.ASCII.GetString(buffer, 0, read);
            var responseBytes = Encoding.ASCII.GetBytes(response);
            await stream.WriteAsync(responseBytes);
        }

        private async Task RunStreamingAsync(Func<Stream, Task> writeResponseAsync)
        {
            using var client = await _listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var buffer = new byte[4096];
            var read = await stream.ReadAsync(buffer);
            RequestText = Encoding.ASCII.GetString(buffer, 0, read);
            try
            {
                await writeResponseAsync(stream);
            }
            catch (IOException)
            {
            }
        }
    }
}
