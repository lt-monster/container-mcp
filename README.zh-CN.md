<h1 align="center">container-mcp</h1>

<p align="center">
  <a href="README.md">English</a> | <strong>中文</strong>
</p>

**container-mcp** 是一个面向本地容器运行时的轻量级 MCP Server。它通过 MCP JSON-RPC 暴露 Docker 兼容的镜像、容器、卷、诊断和本地端口发现能力，让 AI 助手或开发工具可以用结构化方式检查和操作本地容器环境。

## 功能概览

- MCP JSON-RPC server，支持 `initialize`、`ping`、`tools/list` 和 `tools/call`。
- HTTP transport：通过 `POST /mcp` 接收 MCP 请求，`GET /` 返回基础元数据。
- stdio transport：适合集成通过 stdin/stdout 通信的代理工具。
- 通过本地 socket、命名管道或 TCP endpoint 访问 Docker 兼容运行时。
- 提供镜像、容器、卷、Docker 诊断和本地空闲端口发现工具。
- 默认安全边界保守：v1 仅支持本地 target，并拒绝 host bind mount。
- 使用 `System.Text.Json` source generation 和紧凑 JSON-RPC 响应。

## 快速开始

构建项目：

```powershell
dotnet build
```

以 HTTP transport 运行：

```powershell
dotnet run --project ContainerMcp.Server -- --transport http --urls http://127.0.0.1:7010
```

MCP JSON-RPC 请求地址：

```text
POST http://127.0.0.1:7010/mcp
```

以 stdio transport 运行：

```powershell
dotnet run --project ContainerMcp.Server -- --transport stdio
```

## MCP 工具

| 工具 | 用途 |
|---|---|
| `image_list` | 列出本地镜像。 |
| `image_inspect` | 查看本地镜像详情。 |
| `image_pull` | 拉取镜像。 |
| `image_tag` | 为本地镜像打 tag。 |
| `image_prune` | 清理未使用的本地镜像。 |
| `image_build` | 从 tar 构建上下文构建镜像。 |
| `image_push` | 推送镜像。 |
| `image_load` | 从 tar 归档加载镜像。 |
| `image_save` | 将本地镜像保存为 tar 归档。 |
| `image_remove` | 删除镜像。 |
| `container_list` | 列出容器。 |
| `container_inspect` | 查看容器详情。 |
| `container_create` | 创建容器。 |
| `container_start` | 启动容器。 |
| `container_stop` | 停止容器。 |
| `container_remove` | 删除容器。 |
| `container_logs` | 读取容器日志。 |
| `volume_list` | 列出卷。 |
| `volume_create` | 创建命名卷。 |
| `volume_remove` | 删除卷。 |
| `docker_diagnose` | 诊断 Docker 连接。 |
| `port_find_free` | 查找本地空闲端口。 |

运行时相关工具默认支持共享参数 `engine` 和 `target`，除非对应工具 schema 已经显式定义同名参数。

`container_create` 支持常见 Docker 创建参数，包括 `name`、`platform`、`ports`、`env`、`volumes`、`command`、`workingDir`、`user`、`hostname`、`networkMode`、`tty`、`entrypoint`、资源限制、restart policy、labels 和 healthcheck 设置。

镜像构建、导入和导出工具使用本地 tar 文件路径。`image_build` 需要已有 tar 构建上下文，`image_load` 需要已有镜像 tar 归档，`image_save` 会将镜像 tar 写入本地输出路径。私有 registry 的认证方案尚未实现。

## 配置

配置读取顺序为：命令行参数、环境变量、默认值。

| 参数 | 环境变量 | 默认值 |
|---|---|---|
| `--transport` | `CONTAINER_MCP_TRANSPORT` | `http` |
| `--urls` | `CONTAINER_MCP_HTTP_URLS` 或 `ASPNETCORE_URLS` | `http://127.0.0.1:7010` |
| `--default-engine` | `CONTAINER_MCP_DEFAULT_ENGINE` | `auto` |
| `--default-target` | `CONTAINER_MCP_DEFAULT_TARGET` | `local` |

重要默认值：

- API timeout 默认为 10 秒。
- probe timeout 默认为 2 秒。
- tool timeout 默认为 15 秒。

## 运行时支持

v1 仅支持本地 target。

- **Windows 上的 Docker：** 默认使用 `\\.\pipe\docker_engine`。
- **Unix 上的 Docker：** 默认使用 `/var/run/docker.sock`，除非设置了 `DOCKER_HOST`。
- **Unix 上的 Podman：** 从 `CONTAINER_MCP_PODMAN_HOST`、`PODMAN_HOST` 或常见 socket 路径发现 endpoint。
- **Windows 上的 Podman：** v1 尚未实现。

## 项目结构

```text
container-mcp/
├── ContainerMcp.sln
├── ContainerMcp.Server/
│   ├── Configuration/
│   ├── ContainerRuntime/
│   ├── Mcp/
│   ├── Models/
│   ├── Ports/
│   ├── Tools/
│   └── Program.cs
├── ContainerMcp.Server.Tests/
├── README.md
└── README.zh-CN.md
```

## 安全边界

- v1 拒绝 host bind mount。
- 仅允许命名卷或匿名容器卷。
- 仅支持 `target=local`。
- 镜像 tar 导入/导出使用显式本地文件路径，并限制读写大小。
- stdio transport 中 stdout 只用于 JSON-RPC 响应，诊断日志应写入 stderr。
- 不建议在 MCP 请求路径中加入长时间运行的后台任务。

## 开发

核心项目位于 `ContainerMcp.Server/`，目标框架为 `net10.0`。项目启用了 nullable reference types、implicit usings，并使用 `System.Text.Json` source generation。

常用命令：

```powershell
dotnet build
dotnet test
dotnet run --project ContainerMcp.Server -- --transport http --urls http://127.0.0.1:7010
dotnet run --project ContainerMcp.Server -- --transport stdio
```

测试项目位于 `ContainerMcp.Server.Tests/`。新增行为时，优先添加聚焦的自动化测试，不要只依赖手动 Docker 检查。

更多代理和维护约定见 [AGENTS.md](AGENTS.md)。

## 许可证

见 [LICENSE](LICENSE)。
