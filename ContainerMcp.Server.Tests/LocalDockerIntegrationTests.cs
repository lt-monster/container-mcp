using System.Text.Json;
using ContainerMcp.Configuration;
using ContainerMcp.ContainerRuntime;
using ContainerMcp.Tools;

namespace ContainerMcp.Server.Tests;

public sealed class LocalDockerIntegrationTests
{
    [Fact]
    public async Task DockerDesktop_CanRunContainerLogsLifecycle()
    {
        if (Environment.GetEnvironmentVariable("CONTAINER_MCP_RUN_DOCKER_TESTS") != "1")
        {
            return;
        }

        var name = "container-mcp-test-" + Guid.NewGuid().ToString("N");
        var image = Environment.GetEnvironmentVariable("CONTAINER_MCP_DOCKER_TEST_IMAGE") ?? "busybox:latest";
        var services = CreateServices();
        try
        {
            var diagnose = await services.Diagnostics.DiagnoseAsync(CancellationToken.None);
            Assert.True(diagnose["dockerAvailable"]!.GetValue<bool>());

            using (var pullArgs = JsonDocument.Parse($$"""{"image":"{{image}}"}"""))
            {
                await services.Images.ImagePullAsync(pullArgs.RootElement, CancellationToken.None);
            }

            using (var createArgs = JsonDocument.Parse($$"""{"image":"{{image}}","name":"{{name}}","command":["sh","-c","echo container-mcp-ok && sleep 30"]}"""))
            {
                var create = await services.Containers.ContainerCreateAsync(createArgs.RootElement, CancellationToken.None);
                Assert.NotNull(create["items"]!["Id"]);
            }

            using (var startArgs = JsonDocument.Parse($$"""{"idOrName":"{{name}}"}"""))
            {
                await services.Containers.ContainerStartAsync(startArgs.RootElement, CancellationToken.None);
            }

            await AssertLogsContainAsync(services.Containers, name, "container-mcp-ok");

            using (var stopArgs = JsonDocument.Parse($$"""{"idOrName":"{{name}}","timeoutSeconds":5}"""))
            {
                await services.Containers.ContainerStopAsync(stopArgs.RootElement, CancellationToken.None);
            }
        }
        finally
        {
            using var removeArgs = JsonDocument.Parse($$"""{"idOrName":"{{name}}","force":true,"volumes":true}""");
            try
            {
                await services.Containers.ContainerRemoveAsync(removeArgs.RootElement, CancellationToken.None);
            }
            catch
            {
            }
        }
    }

    private static TestServices CreateServices()
    {
        var options = ContainerMcpOptions.From(["--default-engine", "docker", "--api-timeout-seconds", "30", "--tool-timeout-seconds", "60"]);
        var factory = new DockerApiClientFactory(options);
        var resolver = new EngineResolver(factory);
        var runtime = new RuntimeToolSupport(options, resolver);
        var api = new ContainerApiAdapter(factory, options);
        return new TestServices(
            new ImageToolService(runtime, api),
            new ContainerToolService(runtime, api, new ContainerCreateRequestBuilder(new VolumePolicy())),
            new DockerDiagnosticsService(options, factory));
    }

    private static async Task AssertLogsContainAsync(ContainerToolService containers, string name, string expected)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            using var logsArgs = JsonDocument.Parse($$"""{"idOrName":"{{name}}","maxBytes":4096}""");
            var logs = await containers.ContainerLogsAsync(logsArgs.RootElement, CancellationToken.None);
            if (logs["items"]!["text"]!.GetValue<string>().Contains(expected, StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        Assert.Fail($"Container logs did not contain '{expected}'.");
    }

    private sealed record TestServices(
        ImageToolService Images,
        ContainerToolService Containers,
        DockerDiagnosticsService Diagnostics);
}
