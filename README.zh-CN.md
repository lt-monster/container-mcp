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

## Transport 差异

HTTP transport 在 `POST /mcp` 接收 JSON-RPC 请求，并支持 batch request。只有 notification 的消息或 batch 会返回 `202 Accepted`，且没有响应体。`GET /mcp` 返回 `405 Method Not Allowed`，因为尚未实现 server-sent event streaming；`GET /` 返回服务元数据。HTTP 请求体大小限制为 1 MiB。

stdio transport 从 stdin 按行读取紧凑 JSON-RPC 消息，并且只把 JSON-RPC 响应写入 stdout。启动信息和诊断日志写入 stderr，避免污染 MCP client 读取 stdout 的协议流。

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
| `container_pause` | 暂停运行中的容器。 |
| `container_unpause` | 恢复暂停的容器。 |
| `container_rename` | 重命名容器。 |
| `container_exec_create` | 在容器中创建 exec 实例。 |
| `container_exec_start` | 启动 exec 实例并读取有界输出。 |
| `container_stats` | 读取有界容器 stats 快照。 |
| `container_top` | 列出容器内运行的进程。 |
| `container_wait` | 等待容器条件并返回退出状态。 |
| `container_stop` | 停止容器。 |
| `container_restart` | 重启容器。 |
| `container_kill` | Kill 容器。 |
| `container_prune` | 清理已停止的容器。 |
| `container_remove` | 删除容器。 |
| `container_logs` | 读取容器日志。 |
| `container_logs_follow` | 在有界时间内跟随容器日志。 |
| `volume_list` | 列出卷。 |
| `volume_inspect` | 查看卷详情。 |
| `volume_create` | 创建命名卷。 |
| `volume_prune` | 清理未使用的本地卷。 |
| `volume_remove` | 删除卷。 |
| `network_list` | 列出网络。 |
| `network_inspect` | 查看网络详情。 |
| `network_create` | 创建网络。 |
| `network_remove` | 删除网络。 |
| `network_connect` | 将容器连接到网络。 |
| `network_disconnect` | 将容器从网络断开。 |
| `network_prune` | 清理未使用的本地网络。 |
| `docker_diagnose` | 诊断 Docker 连接。 |
| `port_find_free` | 查找本地空闲端口。 |

运行时相关工具默认支持共享参数 `engine` 和 `target`，除非对应工具 schema 已经显式定义同名参数。

`container_create` 支持常见 Docker 创建参数，包括 `name`、`platform`、`ports`、`env`、`volumes`、`command`、`workingDir`、`user`、`hostname`、`networkMode`、`tty`、`entrypoint`、资源限制、restart policy、labels 和 healthcheck 设置。

容器生命周期工具也暴露常用运行时参数：`container_stop` 和 `container_restart` 支持 `timeoutSeconds`，`container_kill` 支持 `signal`，`container_wait` 支持 `condition`（`not-running`、`next-exit` 或 `removed`）和 `timeoutSeconds`，`container_logs` 支持 `tail`、`since`、`timestamps` 和有界 `maxBytes` 输出。`container_logs` 默认不跟随日志（`follow=false`）；`container_logs_follow` 使用 `follow=true`，支持 `durationSeconds`、`tail`、`timestamps` 和 `maxBytes`。日志 `maxBytes` 默认 1 MiB，硬上限 4 MiB；follow 时长默认 10 秒，硬上限 60 秒。

镜像构建、导入和导出工具使用本地 tar 文件路径。`image_build` 需要已有 tar 构建上下文，并支持 `dockerfile`、`noCache`、`pull`、`removeIntermediate`、`forceRemoveIntermediate` 和 `maxEvents`。`image_load` 需要已有镜像 tar 归档。`image_save` 会将镜像 tar 写入绝对本地输出路径，要求父目录已存在，并支持 `maxBytes` 和 `overwrite`。私有 registry 的认证方案尚未实现。

流式和二进制响应都是有界的。`container_logs`、`container_logs_follow` 和 `container_exec_start` 返回解码后的 `stdout`、`stderr`、`text`、`bytesRead`、`frameCount`、`truncated` 和 `framed` 字段；`container_logs_follow` 还会返回 `durationSeconds` 和 `completedBy`。`image_pull`、`image_build`、`image_push`、`image_load` 等镜像进度工具返回 `events`、`eventCount`、`lastStatus`、`lastError` 和 `truncated`。

`image_prune` 支持 `dangling`、`until`、`labels` 和 `labelNe` 过滤器。`volume_create` 支持 `driver`、`driverOptions` 和 `labels`；`volume_prune` 支持 `labels` 和 `labelNe` 过滤器。`network_create` 支持 `driver`、`internal`、`attachable`、`enableIPv6`、`options` 和 `labels`；`network_connect` 支持 aliases 和 endpoint IPv4/IPv6 地址；`network_prune` 支持 `until`、`labels` 和 `labelNe` 过滤器。`port_find_free` 默认使用 `host=127.0.0.1`、`start=1024`、`end=65535`、`count=1` 和 `protocol=tcp`，并返回 `engine` 为 `none`。

## 配置

配置读取优先级为：命令行参数、环境变量、配置文件、默认值。可以通过 `--config <path>` 或 `CONTAINER_MCP_CONFIG` 指定配置文件；如果都没有指定，程序会尝试读取程序同目录（`AppContext.BaseDirectory`）下的 `container-mcp.config.json`，默认文件不存在时会忽略。

| 参数 | 环境变量 | 默认值 |
|---|---|---|
| `--config` | `CONTAINER_MCP_CONFIG` | 程序同目录下存在的 `container-mcp.config.json` |
| `--transport` | `CONTAINER_MCP_TRANSPORT` | `http` |
| `--urls` | `CONTAINER_MCP_HTTP_URLS` 或 `ASPNETCORE_URLS` | `http://127.0.0.1:7010` |
| `--default-engine` | `CONTAINER_MCP_DEFAULT_ENGINE` | `auto` |
| `--default-target` | `CONTAINER_MCP_DEFAULT_TARGET` | `local` |
| `--api-timeout-seconds` | `CONTAINER_MCP_API_TIMEOUT_SECONDS` | `10` |
| `--api-probe-timeout-seconds` | `CONTAINER_MCP_API_PROBE_TIMEOUT_SECONDS` | `2` |
| `--tool-timeout-seconds` | `CONTAINER_MCP_TOOL_TIMEOUT_SECONDS` | `15` |

配置文件示例：

```json
{
  "version": 1,
  "transport": "http",
  "urls": "http://127.0.0.1:7010",
  "defaultEngine": "auto",
  "defaultTarget": "local",
  "timeouts": {
    "toolSeconds": 15,
    "apiSeconds": 10,
    "apiProbeSeconds": 2
  },
  "http": {
    "maxRequestBodyBytes": 1048576,
    "tokens": [
      {
        "id": "local-admin",
        "value": "cmcp_xxx",
        "enabled": true,
        "createdAt": "2026-06-03T12:00:00Z",
        "description": "Local admin client"
      }
    ]
  }
}
```

生成并持久化 HTTP bearer token：

```powershell
dotnet run --project ContainerMcp.Server -- token generate
dotnet run --project ContainerMcp.Server -- token generate --config .\container-mcp.config.json --id local-admin --description "Local admin client"
```

当配置中存在 enabled token 时，`POST /mcp` 请求需要携带：

```http
Authorization: Bearer cmcp_xxx
```

如果 HTTP transport 绑定到非 loopback 地址，例如 `http://0.0.0.0:7010`，必须至少配置一个有效 token。

重要默认值：

- API timeout 默认为 10 秒。
- probe timeout 默认为 2 秒。
- tool timeout 默认为 15 秒。

## 运行时支持

v1 仅支持本地 target；当前文档聚焦 Windows 上的 Docker Desktop。

- **Windows 上的 Docker：** 默认使用 `\\.\pipe\docker_engine`。

Windows 上的 Docker endpoint 环境变量支持 `npipe://`、`tcp://` 和 `http://` endpoint 值。

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
- v1 尚未实现远程 target、host bind mount、无限实时流式输出和 registry 认证。
- 当 HTTP transport 绑定到非 loopback 地址时会输出警告；常规本地 MCP client 建议使用 loopback URL。
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

测试项目位于 `ContainerMcp.Server.Tests/`。新增行为时，优先添加聚焦的自动化测试，不要只依赖手动 Docker 检查。可选 Docker Desktop 集成测试默认不执行，可以这样启用：

```powershell
$env:CONTAINER_MCP_RUN_DOCKER_TESTS = "1"
dotnet test --filter LocalDockerIntegrationTests
```

更多代理和维护约定见 [AGENTS.md](AGENTS.md)。

## 许可证

见 [LICENSE](LICENSE)。
