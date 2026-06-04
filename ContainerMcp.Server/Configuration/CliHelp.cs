using ContainerMcp.Mcp;

namespace ContainerMcp.Configuration;

internal static class CliHelp
{
    public static bool IsHelpRequested(string[] args) =>
        args.Any(arg =>
            string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase));

    public static bool IsVersionRequested(string[] args) =>
        args.Any(arg => string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase));

    public static string BuildVersion() => $"container-mcp {ServerVersion.Current}";

    public static string BuildHelp() =>
        """
        container-mcp - MCP JSON-RPC server for local container runtime operations.

        Usage:
          container-mcp [options]
          container-mcp token generate [options]
          container-mcp --help
          container-mcp --version

        Transports:
          --transport <http|stdio>                 Transport mode. Default: http.
          --urls <url>                             HTTP listen URL. Default: http://127.0.0.1:7010.

        Runtime defaults:
          --default-engine <auto|docker|podman>    Default container engine. Default: auto.
          --default-target <target>                Default runtime target. Default: local.

        Timeouts:
          --tool-timeout-seconds <seconds>         MCP tool timeout. Default: 15.
          --api-timeout-seconds <seconds>          Runtime API timeout. Default: 10.
          --api-probe-timeout-seconds <seconds>    Runtime probe timeout. Default: 2.

        HTTP:
          --http-max-request-body-bytes <bytes>    Maximum HTTP MCP request body size.

        Configuration:
          --config <path>                          Configuration file path.

        Token generation:
          token generate                           Generate HTTP bearer token configuration.
          token generate --count <number>          Number of tokens to generate. Default: 1.
          token generate --id <id>                 Token identifier.
          token generate --description <text>      Token description.
          token generate --config <path>           Configuration file to update.

        Environment:
          CONTAINER_MCP_TRANSPORT
          CONTAINER_MCP_HTTP_URLS
          ASPNETCORE_URLS
          CONTAINER_MCP_DEFAULT_ENGINE
          CONTAINER_MCP_DEFAULT_TARGET
          CONTAINER_MCP_API_TIMEOUT_SECONDS
          CONTAINER_MCP_API_PROBE_TIMEOUT_SECONDS
          CONTAINER_MCP_TOOL_TIMEOUT_SECONDS
          CONTAINER_MCP_HTTP_MAX_REQUEST_BODY_BYTES
          CONTAINER_MCP_CONFIG
        """;
}
