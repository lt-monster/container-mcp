using ContainerMcp.Configuration;
using ContainerMcp.ContainerRuntime;
using System.Text.Json.Nodes;

namespace ContainerMcp.Tools;

internal sealed class DockerDiagnosticsService
{
    private readonly ContainerMcpOptions _options;
    private readonly DockerApiClientFactory _factory;

    public DockerDiagnosticsService(ContainerMcpOptions options, DockerApiClientFactory factory)
    {
        _options = options;
        _factory = factory;
    }

    public async Task<JsonObject> DiagnoseAsync(CancellationToken cancellationToken)
    {
        var dockerEndpoint = _factory.GetEndpoint(ContainerEngine.Docker);
        var endpointNode = dockerEndpoint is null
            ? null
            : new JsonObject
            {
                ["engine"] = "docker",
                ["kind"] = dockerEndpoint.Kind.ToString(),
                ["address"] = dockerEndpoint.Address,
                ["display"] = dockerEndpoint.ToString()
            };

        var available = false;
        string? lastFailure = null;
        if (dockerEndpoint is not null)
        {
            available = await _factory.CanConnectAsync(dockerEndpoint, cancellationToken);
            lastFailure = _factory.GetLastProbeFailure(dockerEndpoint);
        }

        return new JsonObject
        {
            ["engineStrategy"] = _options.DefaultEngine.ToString().ToLowerInvariant(),
            ["targetStrategy"] = _options.DefaultTarget,
            ["transport"] = _options.Transport.ToString().ToLowerInvariant(),
            ["dockerEndpoint"] = endpointNode,
            ["dockerAvailable"] = available,
            ["lastProbeFailure"] = lastFailure,
            ["timeouts"] = new JsonObject
            {
                ["toolTimeoutSeconds"] = (int)_options.ToolTimeout.TotalSeconds,
                ["apiTimeoutSeconds"] = (int)_options.ApiTimeout.TotalSeconds,
                ["apiProbeTimeoutSeconds"] = (int)_options.ApiProbeTimeout.TotalSeconds
            }
        };
    }
}
