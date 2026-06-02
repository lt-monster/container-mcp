# container-mcp Agent Guide

## Project Overview

This repository contains the `container-mcp` ASP.NET Core server in `ContainerMcp.Server/`.
The application exposes local container runtime operations through MCP JSON-RPC.

Supported transports:

- HTTP: `POST /mcp` handles MCP requests, and `GET /` returns basic metadata.
- stdio: JSON-RPC requests are read line-by-line from stdin and compact JSON responses are written to stdout.

The host targets `net10.0`, enables nullable reference types and implicit usings, and uses `ContainerMcpJsonContext` for `System.Text.Json` source generation. The project currently has no third-party package references.

## Repository Layout

- `ContainerMcp.sln`: solution file.
- `global.json`: repository-level .NET SDK selection.
- `ContainerMcp.Server/`: ASP.NET Core MCP server project.
- `ContainerMcp.Server/Program.cs`: app entry point, transport selection, DI registration, HTTP endpoints, and stdio service wiring.
- `ContainerMcp.Server/Configuration/`: command-line and environment option parsing.
- `ContainerMcp.Server/Mcp/`: MCP JSON-RPC request handling, tool registry, and stdio service loop.
- `ContainerMcp.Server/ContainerRuntime/`: Docker/Podman endpoint resolution, API client creation, Docker-compatible API adapter, and volume safety policy.
- `ContainerMcp.Server/Tools/`: MCP tool implementations for images, containers, volumes, diagnostics, and ports.
- `ContainerMcp.Server/Ports/`: local free-port discovery.
- `ContainerMcp.Server/Models/`: error codes, exception types, JSON helpers, and JSON source-generation context.
- `ContainerMcp.Server/Properties/launchSettings.json`: local launch profiles for HTTP and stdio modes.
- `.agents/`, `.cursor/`, `.codegraph/`, and `plugins/`: repository-level AI assistant, editor, CodeGraph, and local plugin configuration.

## Runtime Behavior

Options are read in this order: command-line arguments, environment variables, then hard-coded defaults.

Important defaults:

- `--transport` or `CONTAINER_MCP_TRANSPORT`: defaults to HTTP.
- `--urls`, `CONTAINER_MCP_HTTP_URLS`, or `ASPNETCORE_URLS`: defaults to `http://127.0.0.1:7010`.
- `--default-engine` or `CONTAINER_MCP_DEFAULT_ENGINE`: defaults to `auto`.
- `--default-target` or `CONTAINER_MCP_DEFAULT_TARGET`: defaults to `local`.
- API timeout defaults to 10 seconds, probe timeout defaults to 2 seconds, and tool timeout defaults to 15 seconds.

Version 1 only supports the local target. Docker is reached through `\\.\pipe\docker_engine` on Windows and `/var/run/docker.sock` on Unix unless `DOCKER_HOST` is set. Podman on Windows is not implemented for v1; on Unix it is discovered from `CONTAINER_MCP_PODMAN_HOST`, `PODMAN_HOST`, or common socket paths.

## MCP Interface

Tool registration lives in `ContainerMcp.Server/Mcp/McpToolRegistry.cs`.

Current tools:

- `image_list`
- `image_inspect`
- `image_pull`
- `image_tag`
- `image_prune`
- `image_build`
- `image_push`
- `image_load`
- `image_save`
- `image_remove`
- `container_list`
- `container_inspect`
- `container_create`
- `container_start`
- `container_pause`
- `container_unpause`
- `container_rename`
- `container_exec_create`
- `container_exec_start`
- `container_stats`
- `container_top`
- `container_wait`
- `container_stop`
- `container_restart`
- `container_kill`
- `container_prune`
- `container_remove`
- `container_logs`
- `container_logs_follow`
- `volume_list`
- `volume_create`
- `volume_remove`
- `docker_diagnose`
- `port_find_free`

All runtime-related tools accept shared `engine` and `target` parameters unless the tool schema already defines parameters with those names. Preserve that behavior when adding tools.

Image build, load, and save use local tar file paths. `image_build` accepts an existing tar build context, `image_load` accepts an existing image tar archive, and `image_save` writes a tar archive to `outputPath`. Do not put tar payloads into MCP JSON responses. Registry authentication for private pull/push remains a separate design item.

## Development Commands

Run from the repository root:

```powershell
dotnet build
dotnet run --project ContainerMcp.Server -- --transport http --urls http://127.0.0.1:7010
dotnet run --project ContainerMcp.Server -- --transport stdio
```

The repository includes focused xUnit tests in `ContainerMcp.Server.Tests/`. When adding meaningful behavior, prefer adding focused tests rather than relying only on manual Docker checks.

## Coding Conventions

- Keep types `internal` unless they are intentionally part of a public API surface.
- Preserve nullable correctness; use null-forgiving only when surrounding code has already proven the invariant.
- Prefer `JsonObject`, `JsonArray`, `JsonNode`, and `JsonElement` because the app intentionally avoids reflection-based JSON serialization.
- If a new strongly typed JSON serialization shape is introduced, add it to `ContainerMcpJsonContext`.
- JSON-RPC responses should use `JsonNodeExtensions.ToCompactJson()` to keep compact formatting.
- Tool code should return structured MCP errors through `ContainerMcpException` and `McpErrorCode`; do not throw generic exceptions directly from user-facing paths.
- Runtime API calls should stay behind `ContainerApiAdapter`; do not call Docker or Podman endpoints directly from tool registry code.
- Validate and normalize user-supplied tool arguments close to the tool implementation before using them to construct runtime API paths or request bodies.
- Keep the volume safety policy conservative. Version 1 rejects host bind mounts and allows only named or anonymous container volumes.
- Continue treating `target=local` as the only supported target unless resolver, schema, tool behavior, and error handling are updated together.

## Architecture Notes

`ContainerMcp.Server/Program.cs` currently maintains two equivalent singleton registration paths: one for ASP.NET Core DI in HTTP mode and one explicit `ServiceCollection` for stdio mode. If service registration changes, update both paths or extract shared registration.

`McpJsonRpcHandler` handles these MCP methods:

- `initialize`
- `ping`
- `tools/list`
- `tools/call`
- `notifications/initialized`

Tool execution is wrapped by `ContainerMcpOptions.ToolTimeout`. Lower-level runtime API calls also have separate API and probe timeouts.

`DockerApiClientFactory` caches `HttpClient` instances per runtime endpoint with custom connection callbacks for named pipes, Unix sockets, and TCP endpoints. Endpoint probe results are cached briefly, so diagnostics and tests should not assume every call immediately re-probes.

`PortDiscoveryService` does not depend on a container engine. It returns `engine: "none"` and `target: "local"`.

## Safety Constraints

- Do not casually add host bind mount support; that changes the security model and requires explicit design.
- Do not extend targets beyond `local` unless `EngineResolver`, schemas, tool behavior, and error handling are updated together.
- Be careful when changing timeouts. Tool timeout, API timeout, and probe timeout are normalized so probe timeout does not exceed API timeout, and API timeout does not exceed tool timeout.
- Avoid long-running background work inside request paths. MCP calls should return bounded, structured results.
- In stdio mode, do not write diagnostics to stdout. stdout is reserved for JSON-RPC responses; runtime logs belong on stderr.

## CodeGraph

This repository can use a CodeGraph MCP index from the root `.codegraph/` directory. If `.codegraph/` does not exist and structural analysis is needed, ask the user whether to run:

```powershell
codegraph init -i
```
