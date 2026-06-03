using System.Text;
using System.Text.Json;
using ContainerMcp.Configuration;
using ContainerMcp.ContainerRuntime;
using ContainerMcp.Models;
using ContainerMcp.Ports;
using ContainerMcp.Tools;

namespace ContainerMcp.Server.Tests;

public sealed class FakeDockerIntegrationTests
{
    [Fact]
    public async Task DockerDiagnose_ReportsAvailableFakeDockerEndpoint()
    {
        await using var server = await FakeDockerApiServer.StartAsync(Ok("OK"));
        using var environment = new EnvironmentScope().Set("DOCKER_HOST", server.Host);
        var options = ContainerMcpOptions.From(["--api-probe-timeout-seconds", "1"]);
        using var factory = new DockerApiClientFactory(options);
        var service = new DockerDiagnosticsService(options, factory);

        var result = await service.DiagnoseAsync(CancellationToken.None);

        Assert.True(result["dockerAvailable"]!.GetValue<bool>());
        Assert.Equal(server.Host, result["dockerEndpoint"]!["address"]!.GetValue<string>());
        Assert.Contains("GET /_ping HTTP/1.1", server.Requests[0]);
    }

    [Fact]
    public async Task ContainerList_UsesResolvedDockerEndpointAndReturnsItems()
    {
        await using var server = await FakeDockerApiServer.StartAsync(
            Ok("OK"),
            Json("""[{"Id":"abc","Image":"nginx"}]"""));
        using var environment = new EnvironmentScope().Set("DOCKER_HOST", server.Host);
        var service = CreateContainerService(["--api-probe-timeout-seconds", "1"]);
        using var document = JsonDocument.Parse("""{"all":true}""");

        var result = await service.ContainerListAsync(document.RootElement, CancellationToken.None);

        Assert.Equal("docker", result["engine"]!.GetValue<string>());
        Assert.Equal("abc", result["items"]![0]!["Id"]!.GetValue<string>());
        Assert.Contains("GET /containers/json?all=true HTTP/1.1", server.Requests[1]);
    }

    [Fact]
    public async Task ImagePull_ReadsProgressEventsFromFakeDocker()
    {
        await using var server = await FakeDockerApiServer.StartAsync(
            Ok("OK"),
            Stream("""{"status":"Pulling"}""" + "\n" + """{"status":"Done"}""" + "\n"));
        using var environment = new EnvironmentScope().Set("DOCKER_HOST", server.Host);
        var service = CreateImageService(["--api-probe-timeout-seconds", "1"]);
        using var document = JsonDocument.Parse("""{"image":"busybox:latest"}""");

        var result = await service.ImagePullAsync(document.RootElement, CancellationToken.None);

        Assert.Equal(2, result["items"]!["eventCount"]!.GetValue<int>());
        Assert.Equal("Done", result["items"]!["lastStatus"]!.GetValue<string>());
        Assert.Contains("POST /images/create?fromImage=busybox%3Alatest HTTP/1.1", server.Requests[1]);
    }

    [Fact]
    public async Task ContainerLogs_DecodesRawStreamFromFakeDocker()
    {
        var frame = DockerFrame(streamType: 1, "hello\n");
        await using var server = await FakeDockerApiServer.StartAsync(
            Ok("OK"),
            Binary(frame));
        using var environment = new EnvironmentScope().Set("DOCKER_HOST", server.Host);
        var service = CreateContainerService(["--api-probe-timeout-seconds", "1"]);
        using var document = JsonDocument.Parse("""{"idOrName":"web","maxBytes":1024}""");

        var result = await service.ContainerLogsAsync(document.RootElement, CancellationToken.None);

        Assert.Equal("hello\n", result["items"]!["stdout"]!.GetValue<string>());
        Assert.True(result["items"]!["framed"]!.GetValue<bool>());
        Assert.Contains("GET /containers/web/logs?stdout=true&stderr=true&follow=false HTTP/1.1", server.Requests[1]);
    }

    [Fact]
    public async Task ContainerInspect_MapsDockerNotFoundError()
    {
        await using var server = await FakeDockerApiServer.StartAsync(
            Ok("OK"),
            Json("""{"message":"No such container"}""", "404 Not Found"));
        using var environment = new EnvironmentScope().Set("DOCKER_HOST", server.Host);
        var service = CreateContainerService(["--api-probe-timeout-seconds", "1"]);
        using var document = JsonDocument.Parse("""{"idOrName":"missing"}""");

        var exception = await Assert.ThrowsAsync<ContainerMcpException>(
            () => service.ContainerInspectAsync(document.RootElement, CancellationToken.None));

        Assert.Equal(McpErrorCode.ContainerNotFound, exception.ErrorCode);
        Assert.Equal("No such container", exception.Message);
    }

    private static ContainerToolService CreateContainerService(string[] args)
    {
        var options = ContainerMcpOptions.From(args);
        var factory = new DockerApiClientFactory(options);
        var resolver = new EngineResolver(factory);
        var runtime = new RuntimeToolSupport(options, resolver);
        var api = new ContainerApiAdapter(factory, options);
        return new ContainerToolService(runtime, api, new ContainerCreateRequestBuilder(new VolumePolicy()));
    }

    private static ImageToolService CreateImageService(string[] args)
    {
        var options = ContainerMcpOptions.From(args);
        var factory = new DockerApiClientFactory(options);
        var resolver = new EngineResolver(factory);
        var runtime = new RuntimeToolSupport(options, resolver);
        var api = new ContainerApiAdapter(factory, options);
        return new ImageToolService(runtime, api);
    }

    private static string Ok(string body) => Response("200 OK", "text/plain", Encoding.ASCII.GetBytes(body));

    private static string Json(string body, string status = "200 OK") => Response(status, "application/json", Encoding.ASCII.GetBytes(body));

    private static string Stream(string body) => Response("200 OK", "application/json", Encoding.ASCII.GetBytes(body));

    private static string Binary(byte[] body) => Response("200 OK", "application/octet-stream", body);

    private static string Response(string status, string contentType, byte[] body) =>
        $"HTTP/1.1 {status}\r\nContent-Type: {contentType}\r\nContent-Length: {body.Length}\r\n\r\n" + Encoding.ASCII.GetString(body);

    private static byte[] DockerFrame(byte streamType, string text)
    {
        var payload = Encoding.UTF8.GetBytes(text);
        var frame = new byte[8 + payload.Length];
        frame[0] = streamType;
        frame[4] = (byte)((payload.Length >> 24) & 0xff);
        frame[5] = (byte)((payload.Length >> 16) & 0xff);
        frame[6] = (byte)((payload.Length >> 8) & 0xff);
        frame[7] = (byte)(payload.Length & 0xff);
        Buffer.BlockCopy(payload, 0, frame, 8, payload.Length);
        return frame;
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
