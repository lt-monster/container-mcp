# container-mcp 待办清单

## 1. MCP / JSON-RPC 协议

- [ ] **1.1** 为 HTTP 和 stdio 传输增加 JSON-RPC batch request 支持。（后续实现）
- [ ] **1.2** 复查 MCP HTTP 行为，不只支持简单的 `POST /mcp`，还要评估 session handling 和 streamable HTTP 兼容性。（后续实现）
- [ ] **1.3** 设计长时间运行或流式操作在 MCP 层的表达方式。（后续实现）
- [x] **1.4** 在工具执行前增加 schema 校验，真正落实 `additionalProperties: false`、必填字段和类型约束。（现在实现）
- [x] **1.5** 为不同错误返回更精确的 JSON-RPC error code，避免所有 `ContainerMcpException` 都映射到 `-32000`。（现在实现）

## 2. 运行时支持

- [ ] **2.1** 实现 Windows 上的 Podman endpoint 支持。（后续实现）
- [ ] **2.2** 在远程 target 有完整设计前，继续只支持 `target=local`。（现在实现）
- [ ] **2.3** 设计远程 target 支持，包括 resolver、工具 schema、认证和错误处理。（后续实现）
- [x] **2.4** 实现 `ApiFirst` 行为，或移除当前未使用的配置项。（现在实现）
- [ ] **2.5** 改进 Docker/Podman endpoint 诊断，让 `docker_diagnose` 不只报告 Docker，也能报告 Podman 和自动探测状态。（后续实现）
- [x] **2.6** 按 endpoint 缓存或复用运行时 HTTP handler/client，避免每次 API 调用都创建新 client。（现在实现）

## 3. 容器工具

- [ ] **3.1** 新增 `container_restart`。（现在实现）
- [ ] **3.2** 新增 `container_pause` 和 `container_unpause`。（后续实现）
- [ ] **3.3** 新增 `container_rename`。（后续实现）
- [ ] **3.4** 新增 `container_kill`。（现在实现）
- [ ] **3.5** 新增 `container_exec_create` 和有界的 `container_exec_start`。（后续实现）
- [ ] **3.6** 新增 `container_stats`，默认使用有界的 `stream=false`。（后续实现）
- [ ] **3.7** 新增 `container_top`。（后续实现）
- [ ] **3.8** 新增 `container_wait`，并明确 timeout 行为。（后续实现）
- [ ] **3.9** 新增 `container_prune`。（后续实现）
- [ ] **3.10** 扩展 `container_create` 参数：working directory、user、hostname、network mode、resource limits、platform、TTY、entrypoint、healthcheck。（后续实现）
- [ ] **3.11** 为容器端口映射增加校验，不再静默忽略格式错误的条目。（现在实现）
- [ ] **3.12** 为 restart policy 名称和 timeout 参数增加校验。（现在实现）

## 4. 镜像工具

- [x] **4.1** 新增 `image_inspect`。（现在实现）
- [x] **4.2** 新增 `image_tag`。（现在实现）
- [x] **4.3** 新增 `image_prune`。（现在实现）
- [x] **4.4** 新增 `image_build`，并支持有界的构建进度处理。（现在实现）
- [x] **4.5** 新增 `image_push`，并支持有界的 push 进度处理。（现在实现）
- [x] **4.6** 新增 `image_load` 和 `image_save`，并处理二进制/tar 流。（现在实现）
- [ ] **4.7** 在支持私有仓库 pull/push 前，先设计 registry 认证方案。（后续实现）

## 5. 日志与流式处理

- [ ] **5.1** 保持 `container_logs` 默认有界，并文档化 `maxBytes`。（现在实现）
- [ ] **5.2** 新增独立的有界 `container_logs_follow` 工具，支持 `durationSeconds`、`tail` 和 `maxBytes` 限制。（后续实现）
- [ ] **5.3** 增加 plain TTY logs 和 multiplexed non-TTY raw-stream logs 的测试。（现在实现）
- [ ] **5.4** 增加日志在 Docker raw-stream frame 中间被截断时的测试。（现在实现）
- [ ] **5.5** 决定未来真正的实时流式能力应使用 session、subscription 还是 streamable HTTP。（后续实现）

## 6. 卷工具

- [ ] **6.1** 新增 `volume_inspect`。（现在实现）
- [ ] **6.2** 新增 `volume_prune`。（后续实现）
- [ ] **6.3** 为 `volume_create` 增加 driver 和 driver option 支持。（后续实现）
- [ ] **6.4** 除非有单独通过评审的安全设计，否则 v1 继续不支持 host bind mount。（现在实现）

## 7. 网络工具

- [ ] **7.1** 新增 `network_list`。（后续实现）
- [ ] **7.2** 新增 `network_inspect`。（后续实现）
- [ ] **7.3** 新增 `network_create`。（后续实现）
- [ ] **7.4** 新增 `network_remove`。（后续实现）
- [ ] **7.5** 新增 `network_connect`。（后续实现）
- [ ] **7.6** 新增 `network_disconnect`。（后续实现）
- [ ] **7.7** 新增 `network_prune`。（后续实现）

## 8. 端口发现

- [ ] **8.1** 增加 TCP 和 UDP 空闲端口发现测试。（现在实现）
- [ ] **8.2** 校验 `count` 是否超过请求端口范围的容量。（现在实现）
- [ ] **8.3** 在找不到空闲端口时，考虑返回已跳过的占用端口或诊断元数据。（后续实现）

## 9. 测试

- [ ] **9.1** 扩展 `ContainerMcpOptions` 解析和 timeout 归一化测试。（现在实现）
- [ ] **9.2** 增加 `EngineResolver` Docker/Podman 选择行为测试。（后续实现）
- [ ] **9.3** 通过公开或 internal seam 增加 `DockerApiClientFactory.TryParseHost` 行为测试。（现在实现）
- [ ] **9.4** 增加 `VolumePolicy` 对命名卷接受和 host path 拒绝的测试。（现在实现）
- [ ] **9.5** 增加 `ContainerCreateRequestBuilder` 请求体生成测试。（现在实现）
- [ ] **9.6** 增加 `McpToolRegistry` schema 和共享 `engine` / `target` 参数测试。（现在实现）
- [ ] **9.7** 增加 JSON-RPC request、notification、parse error 和 tool error 行为测试。（现在实现）
- [ ] **9.8** 增加基于 fake Docker-compatible HTTP server 的集成测试。（现在实现）
- [ ] **9.9** 增加可选的真实本地 Docker engine 手动/集成测试。（后续实现）

## 10. 安全与运维

- [ ] **10.1** 当 HTTP transport 绑定到非 loopback 地址时，增加警告或防护。（现在实现）
- [ ] **10.2** 在推荐远程访问前，为 HTTP transport 设计认证或 token 保护。（后续实现）
- [ ] **10.3** 为 HTTP JSON 请求体增加大小限制。（现在实现）
- [ ] **10.4** 如果 HTTP transport 暴露到 localhost 以外，增加 rate limiting。（后续实现）
- [ ] **10.5** 如果计划以托管服务运行，增加 health/readiness 元数据 endpoint。（后续实现）
- [ ] **10.6** 确保 stdio 模式永远不向 stdout 写诊断信息。（现在实现）
- [ ] **10.7** 复查 publish profile，移除源码中的用户级发布文件。（现在实现）

## 11. 文档

- [ ] **11.1** 文档化 `container_logs` 响应结构：`stdout`、`stderr`、`text`、`bytesRead`、`frameCount`、`truncated`、`framed`。（现在实现）
- [ ] **11.2** 文档化 `image_pull` 响应结构：`events`、`eventCount`、`lastStatus`、`lastError`、`truncated`。（现在实现）
- [ ] **11.3** 文档化 HTTP 和 stdio 两种 transport 的差异。（现在实现）
- [ ] **11.4** 文档化当前不支持的能力：Windows Podman、远程 target、bind mount、真正无限流、registry auth、network 工具。（后续实现）
- [ ] **11.5** 修复 README 在仓库中显示为乱码的问题。（现在实现）

## 12. CI / 发布

- [ ] **12.1** 增加 GitHub Actions，执行 `dotnet build` 和 `dotnet test`。（现在实现）
- [ ] **12.2** 如果计划分发二进制，增加 self-contained binary release workflow。（后续实现）
- [ ] **12.3** 增加 package/version 元数据，替代 initialize 响应里硬编码的 `0.1.0`。（现在实现）
- [ ] **12.4** 明确 trimming/AOT 是否是发布目标；如果启用，则增加 publish 验证。（后续实现）
