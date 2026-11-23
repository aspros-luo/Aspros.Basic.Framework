# Framework 回归矩阵（暂时停用）

本文档仅保留历史记录。当前阶段明确暂停执行 Framework xUnit 与 Migration 自动回归，不再把它们作为 Docker 环境部署或当前发布验收的前置条件。

## 当前工作流

启动本地基础设施与示例 API：

```bash
docker compose -f dev-environment/docker-compose.yml --profile infra --profile api up -d --build
```

直接调用 HTTP API，并验证其下游 gRPC Provider：

```bash
curl http://localhost:7141/health
curl "http://localhost:7141/core/ping?message=hello"
curl http://localhost:17041/health
```

端口、凭据、日志和关闭方式见 dev-environment/README.zh-CN.md 与英文版。

## Framework 测试历史状态

最后一次用户提供的 xUnit 执行结果为 44 项：38 通过、4 跳过、2 失败。随后已提交两项测试夹具修正，但尚无后续运行结果确认。因此这份历史结果不能标记为 PASS，也不再阻塞当前 Docker 环境部署。

测试源码仍保留在 tests/ 中，以便未来单独恢复。旧的 framework-test、provider-test、migration-test 和 full-regression Compose runner 已从当前 Compose 配置中移除，不应继续作为本阶段的启动命令。

## 验证边界

- Docker 容器健康状态只能证明容器进程启动，并且其配置的健康检查端点可响应。
- API 请求只能证明该请求实际覆盖的行为。当前示例覆盖 Consumer HTTP -> Provider gRPC 调用链。
- MySQL、Redis、RabbitMQ、Nacos 容器启动，不等于 Framework 已经通过这些服务的集成验证。
- API 镜像构建会编译 API 项目及其 ProjectReference，但不会执行 Framework 测试项目。
