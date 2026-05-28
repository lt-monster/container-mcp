using ContainerMcp.Configuration;
using ContainerMcp.Models;

namespace ContainerMcp.ContainerRuntime;

internal sealed class EngineResolver
{
    private readonly DockerApiClientFactory _factory;

    public EngineResolver(DockerApiClientFactory factory) => _factory = factory;

    public async Task<ResolvedEngine> ResolveAsync(ContainerEngine requested, string target, CancellationToken cancellationToken)
    {
        if (!string.Equals(target, "local", StringComparison.OrdinalIgnoreCase))
        {
            throw new ContainerMcpException(McpErrorCode.UnsupportedTarget, "v1 supports only target=local.", StatusCodes.Status400BadRequest);
        }

        if (OperatingSystem.IsWindows() && requested == ContainerEngine.Podman)
        {
            throw new ContainerMcpException(
                McpErrorCode.EngineNotFound,
                "Podman is not implemented for Windows v1.",
                StatusCodes.Status501NotImplemented);
        }

        if (requested == ContainerEngine.Docker || requested == ContainerEngine.Podman)
        {
            var endpoint = _factory.GetEndpoint(requested);
            if (endpoint is null)
            {
                throw new ContainerMcpException(McpErrorCode.EngineNotFound, $"{requested} API endpoint is not available on this platform.");
            }

            if (!await _factory.CanConnectAsync(endpoint, cancellationToken))
            {
                throw new ContainerMcpException(
                    McpErrorCode.EngineUnavailable,
                    BuildUnavailableMessage(requested, endpoint),
                    StatusCodes.Status503ServiceUnavailable,
                    endpoint.ToString());
            }

            return new ResolvedEngine(requested, endpoint);
        }

        foreach (var engine in new[] { ContainerEngine.Docker, ContainerEngine.Podman })
        {
            if (OperatingSystem.IsWindows() && engine == ContainerEngine.Podman)
            {
                continue;
            }

            var endpoint = _factory.GetEndpoint(engine);
            if (endpoint is null)
            {
                continue;
            }

            if (await _factory.CanConnectAsync(endpoint, cancellationToken))
            {
                return new ResolvedEngine(engine, endpoint);
            }
        }

        var dockerEndpoint = _factory.GetEndpoint(ContainerEngine.Docker);
        throw new ContainerMcpException(
            McpErrorCode.EngineUnavailable,
            dockerEndpoint is null
                ? "Docker API endpoint is not available on this platform."
                : BuildUnavailableMessage(ContainerEngine.Docker, dockerEndpoint),
            StatusCodes.Status503ServiceUnavailable,
            dockerEndpoint?.ToString());
    }

    private string BuildUnavailableMessage(ContainerEngine engine, RuntimeEndpoint endpoint)
    {
        var lastFailure = _factory.GetLastProbeFailure(endpoint);
        return string.IsNullOrWhiteSpace(lastFailure)
            ? $"{engine.ToString().ToLowerInvariant()} API is unavailable at {endpoint}."
            : $"{engine.ToString().ToLowerInvariant()} API is unavailable at {endpoint}: {lastFailure}";
    }
}

internal sealed record ResolvedEngine(ContainerEngine Engine, RuntimeEndpoint Endpoint);
