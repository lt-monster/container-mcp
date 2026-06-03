<h1 align="center">🐳 container-mcp</h1>

<p align="center">
  <strong>English</strong> | <a href="README.zh-CN.md">中文</a>
</p>

**container-mcp** is a lightweight MCP server for local container runtimes. It exposes Docker-compatible container, image, volume, diagnostics, and port-discovery operations through MCP JSON-RPC so AI assistants and developer tools can inspect and operate local containers through a structured interface.

## ✨ Features

- 🧠 MCP JSON-RPC server with `initialize`, `ping`, `tools/list`, and `tools/call` support.
- 🌐 HTTP transport through `POST /mcp`, with `GET /` metadata.
- 📟 stdio transport for agent integrations that communicate over stdin/stdout.
- 🐳 Docker-compatible runtime access through local sockets, named pipes, or TCP endpoints.
- 🧰 Tools for images, containers, volumes, Docker diagnostics, and local free-port discovery.
- 🔒 Conservative safety defaults: v1 supports local targets only and rejects host bind mounts.
- ⚡ `System.Text.Json` source generation and compact JSON-RPC responses.

## 🚀 Quick Start

Build the solution:

```powershell
dotnet build
```

Run with HTTP transport:

```powershell
dotnet run --project ContainerMcp.Server -- --transport http --urls http://127.0.0.1:7010
```

Send MCP JSON-RPC requests to:

```text
POST http://127.0.0.1:7010/mcp
```

Run with stdio transport:

```powershell
dotnet run --project ContainerMcp.Server -- --transport stdio
```

## 🧰 Available MCP Tools

| Tool | Purpose |
|---|---|
| `image_list` | List local images. |
| `image_inspect` | Inspect a local image. |
| `image_pull` | Pull an image. |
| `image_tag` | Tag a local image. |
| `image_prune` | Prune unused local images. |
| `image_build` | Build an image from a tar build context. |
| `image_push` | Push an image. |
| `image_load` | Load images from a tar archive. |
| `image_save` | Save a local image to a tar archive. |
| `image_remove` | Remove an image. |
| `container_list` | List containers. |
| `container_inspect` | Inspect a container. |
| `container_create` | Create a container. |
| `container_start` | Start a container. |
| `container_pause` | Pause a running container. |
| `container_unpause` | Unpause a paused container. |
| `container_rename` | Rename a container. |
| `container_exec_create` | Create an exec instance in a container. |
| `container_exec_start` | Start an exec instance and read bounded output. |
| `container_stats` | Read a bounded container stats snapshot. |
| `container_top` | List processes running in a container. |
| `container_wait` | Wait for a container condition and return its exit status. |
| `container_stop` | Stop a container. |
| `container_restart` | Restart a container. |
| `container_kill` | Kill a container. |
| `container_prune` | Prune stopped containers. |
| `container_remove` | Remove a container. |
| `container_logs` | Read container logs. |
| `container_logs_follow` | Follow container logs for a bounded duration. |
| `volume_list` | List volumes. |
| `volume_inspect` | Inspect a volume. |
| `volume_create` | Create a named volume. |
| `volume_prune` | Prune unused local volumes. |
| `volume_remove` | Remove a volume. |
| `network_list` | List networks. |
| `network_inspect` | Inspect a network. |
| `network_create` | Create a network. |
| `network_remove` | Remove a network. |
| `network_connect` | Connect a container to a network. |
| `network_disconnect` | Disconnect a container from a network. |
| `network_prune` | Prune unused local networks. |
| `docker_diagnose` | Diagnose Docker connectivity. |
| `port_find_free` | Find available local ports. |

Runtime-related tools accept shared `engine` and `target` parameters unless the individual tool schema already defines them.

`container_create` supports common Docker create options including `name`, `platform`, `ports`, `env`, `volumes`, `command`, `workingDir`, `user`, `hostname`, `networkMode`, `tty`, `entrypoint`, resource limits, restart policy, labels, and healthcheck settings.

Container lifecycle tools expose the runtime options they commonly need: `container_stop` and `container_restart` accept `timeoutSeconds`, `container_kill` accepts `signal`, `container_wait` accepts `condition` (`not-running`, `next-exit`, or `removed`) and `timeoutSeconds`, and `container_logs` accepts `tail`, `since`, `timestamps`, and bounded `maxBytes` output. `container_logs` is non-following (`follow=false`) by default; `container_logs_follow` uses `follow=true` with `durationSeconds`, `tail`, `timestamps`, and `maxBytes`. Log `maxBytes` defaults to 1 MiB and is capped at 4 MiB; follow duration defaults to 10 seconds and is capped at 60 seconds.

Image build, load, and save tools operate on local tar file paths. `image_build` expects an existing tar build context and supports `dockerfile`, `noCache`, `pull`, `removeIntermediate`, `forceRemoveIntermediate`, and `maxEvents`. `image_load` expects an existing image tar archive. `image_save` writes a tar archive to an absolute local output path, requires the parent directory to exist, and supports `maxBytes` plus `overwrite`. Registry authentication for private image pull/push is not implemented yet.

Streaming and binary responses are bounded. `container_logs`, `container_logs_follow`, and `container_exec_start` return decoded stream fields such as `stdout`, `stderr`, `text`, `bytesRead`, `frameCount`, `truncated`, and `framed`; `container_logs_follow` also returns `durationSeconds` and `completedBy`. Image progress tools such as `image_pull`, `image_build`, `image_push`, and `image_load` return `events`, `eventCount`, `lastStatus`, `lastError`, and `truncated`.

`image_prune` supports `dangling`, `until`, `labels`, and `labelNe` filters. `volume_create` supports `driver`, `driverOptions`, and `labels`; `volume_prune` supports `labels` and `labelNe` filters. `network_create` supports `driver`, `internal`, `attachable`, `enableIPv6`, `options`, and `labels`; `network_connect` supports aliases and endpoint IPv4/IPv6 addresses; `network_prune` supports `until`, `labels`, and `labelNe` filters. `port_find_free` defaults to `host=127.0.0.1`, `start=1024`, `end=65535`, `count=1`, and `protocol=tcp`, and returns `engine` as `none`.

## ⚙️ Configuration

Options are read in this order: command-line arguments, environment variables, then defaults.

| Option | Environment variable | Default |
|---|---|---|
| `--transport` | `CONTAINER_MCP_TRANSPORT` | `http` |
| `--urls` | `CONTAINER_MCP_HTTP_URLS` or `ASPNETCORE_URLS` | `http://127.0.0.1:7010` |
| `--default-engine` | `CONTAINER_MCP_DEFAULT_ENGINE` | `auto` |
| `--default-target` | `CONTAINER_MCP_DEFAULT_TARGET` | `local` |
| `--api-timeout-seconds` | `CONTAINER_MCP_API_TIMEOUT_SECONDS` | `10` |
| `--api-probe-timeout-seconds` | `CONTAINER_MCP_API_PROBE_TIMEOUT_SECONDS` | `2` |
| `--tool-timeout-seconds` | `CONTAINER_MCP_TOOL_TIMEOUT_SECONDS` | `15` |

Important runtime defaults:

- API timeout defaults to 10 seconds.
- Probe timeout defaults to 2 seconds.
- Tool timeout defaults to 15 seconds.

## 🧭 Runtime Support

Version 1 supports local targets only.

- **Docker on Windows:** uses `\\.\pipe\docker_engine` by default.
- **Docker on Unix:** uses `/var/run/docker.sock` by default unless `DOCKER_HOST` is set.
- **Podman on Unix:** discovers endpoints from `CONTAINER_MCP_PODMAN_HOST`, `PODMAN_HOST`, or common socket paths.
- **Podman on Windows:** not implemented in v1.

Runtime endpoint environment variables accept `unix://`, `npipe://`, `tcp://`, and `http://` endpoint values.

## 📁 Project Structure

```text
container-mcp/
├─ ContainerMcp.sln
├─ ContainerMcp.Server/
│  ├─ Configuration/
│  ├─ ContainerRuntime/
│  ├─ Mcp/
│  ├─ Models/
│  ├─ Ports/
│  ├─ Tools/
│  └─ Program.cs
├─ ContainerMcp.Server.Tests/
├─ README.md
└─ README.zh-CN.md
```

## 🔒 Safety Notes

- Host bind mounts are rejected in v1.
- Only named or anonymous container volumes are allowed.
- Only `target=local` is supported.
- Image tar import/export uses explicit local file paths and bounded reads/writes.
- stdio mode reserves stdout for JSON-RPC responses; diagnostics must go to stderr.
- Long-running background work should not be added to MCP request paths.

## 🛠️ Development

The server project targets `net10.0` and uses nullable reference types, implicit usings, and `System.Text.Json` source generation.

Common commands:

```powershell
dotnet build
dotnet test
dotnet run --project ContainerMcp.Server -- --transport http --urls http://127.0.0.1:7010
dotnet run --project ContainerMcp.Server -- --transport stdio
```

The solution includes focused xUnit tests in `ContainerMcp.Server.Tests/`. When adding behavior, prefer focused automated tests over manual Docker checks alone.

For agent and maintenance guidance, see [AGENTS.md](AGENTS.md).

## 📄 License

See [LICENSE](LICENSE).
