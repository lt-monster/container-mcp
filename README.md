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
| `container_stop` | Stop a container. |
| `container_remove` | Remove a container. |
| `container_logs` | Read container logs. |
| `volume_list` | List volumes. |
| `volume_create` | Create a named volume. |
| `volume_remove` | Remove a volume. |
| `docker_diagnose` | Diagnose Docker connectivity. |
| `port_find_free` | Find available local ports. |

Runtime-related tools accept shared `engine` and `target` parameters unless the individual tool schema already defines them.

`container_create` supports common Docker create options including `name`, `platform`, `ports`, `env`, `volumes`, `command`, `workingDir`, `user`, `hostname`, `networkMode`, `tty`, `entrypoint`, resource limits, restart policy, labels, and healthcheck settings.

Image build, load, and save tools operate on local tar file paths. `image_build` expects an existing tar build context, `image_load` expects an existing image tar archive, and `image_save` writes a tar archive to a local output path. Registry authentication for private image pull/push is not implemented yet.

## ⚙️ Configuration

Options are read in this order: command-line arguments, environment variables, then defaults.

| Option | Environment variable | Default |
|---|---|---|
| `--transport` | `CONTAINER_MCP_TRANSPORT` | `http` |
| `--urls` | `CONTAINER_MCP_HTTP_URLS` or `ASPNETCORE_URLS` | `http://127.0.0.1:7010` |
| `--default-engine` | `CONTAINER_MCP_DEFAULT_ENGINE` | `auto` |
| `--default-target` | `CONTAINER_MCP_DEFAULT_TARGET` | `local` |

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
