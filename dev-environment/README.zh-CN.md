# Docker 开发环境

该 Compose 环境负责启动本地基础设施和示例 HTTP/gRPC API。它是 API 运行环境，不再是 Framework 自动回归测试入口。

## 启动完整环境

在仓库根目录执行：

```bash
docker compose -f dev-environment/docker-compose.yml --profile infra --profile api up -d --build
```

检查容器状态：

```bash
docker compose -f dev-environment/docker-compose.yml --profile infra --profile api ps
```

## 直接调用 API

Consumer 对外提供 HTTP/1.1（7141 端口），并通过 Compose 内部网络调用 Provider 的 gRPC/HTTP2 服务。

```bash
curl --fail http://localhost:7141/health
curl --fail "http://localhost:7141/core/ping?message=hello-from-api"
curl --fail http://localhost:17041/health
```

预期行为：
- Consumer 健康检查返回 `status: ok`。
- `/core/ping` 返回 Provider 响应的 `pong: hello-from-api`。
- Provider 健康检查返回 `status: ok`。

Provider 的 gRPC 端口映射为 `localhost:7041`，HTTP 健康检查映射为 `localhost:17041`。

## 基础设施连接信息

| 服务 | 本机地址 | 本地开发凭据 |
|---|---|---|
| MySQL 8.4 | `localhost:13306` | 数据库 `aspros_framework_test`，用户 `framework`，密码 `framework_password` |
| Redis 7 | `localhost:16379` | 无密码 |
| RabbitMQ | `localhost:5672` | 用户 `framework`，密码 `framework_password` |
| RabbitMQ 管理界面 | `http://localhost:15672` | 用户 `framework`，密码 `framework_password` |
| Nacos 2.5.1 | `http://localhost:18848/nacos` | 本地开发环境关闭认证 |

这些基础设施容器独立于示例 API 启动。当前示例 API 的 smoke request 只验证 HTTP API 到 gRPC Provider/Consumer 的调用链；MySQL、Redis、RabbitMQ、Nacos 已提供运行环境，但不能把“容器启动”误认为“已通过 API 验证”。

## 日志与关闭环境

```bash
docker compose -f dev-environment/docker-compose.yml --profile infra --profile api logs -f
docker compose -f dev-environment/docker-compose.yml --profile infra --profile api down
```

执行 `down` 会保留具名数据卷。若需要同时删除本地数据库和消息服务数据：

```bash
docker compose -f dev-environment/docker-compose.yml --profile infra --profile api down -v
```

## 当前验证边界

旧的 `framework-test`、provider-test、migration-test、full-regression Compose runner 已从当前环境中移除。Framework xUnit 和 Migration 测试源码暂时保留在仓库，但不再属于当前验收流程。

构建 API 镜像时，Docker 会编译两个示例应用及其引用的 Framework 项目，这是应用部署所需的构建步骤；不会运行 Framework IntegrationTests 测试项目。
