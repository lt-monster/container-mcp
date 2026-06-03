using ContainerMcp.ContainerRuntime;
using ContainerMcp.Mcp;
using ContainerMcp.Ports;
using ContainerMcp.Tools;

namespace ContainerMcp.Configuration;

internal static class ContainerMcpServiceCollectionExtensions
{
    public static IServiceCollection AddContainerMcpServices(this IServiceCollection services, ContainerMcpOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<EngineResolver>();
        services.AddSingleton<DockerApiClientFactory>();
        services.AddSingleton<ContainerApiAdapter>();
        services.AddSingleton<RuntimeToolSupport>();
        services.AddSingleton<VolumePolicy>();
        services.AddSingleton<ContainerCreateRequestBuilder>();
        services.AddSingleton<ImageToolService>();
        services.AddSingleton<ContainerToolService>();
        services.AddSingleton<VolumeService>();
        services.AddSingleton<NetworkService>();
        services.AddSingleton<PortDiscoveryService>();
        services.AddSingleton<DockerDiagnosticsService>();
        services.AddSingleton<McpToolRegistry>();
        services.AddSingleton<IMcpToolRegistry>(provider => provider.GetRequiredService<McpToolRegistry>());
        services.AddSingleton<McpJsonRpcHandler>();
        services.AddSingleton<StdioMcpServer>();
        return services;
    }
}
