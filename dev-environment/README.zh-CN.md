# Docker 开发环境

该 Compose 环境负责启动本地基础设施和示例 HTTP/gRPC API，不承担 Framework 测试执行。Framework IntegrationTests 直接在宿主机通过 `dotnet test` 执行；当前发布结果见 `docs/Final-Regression-Matrix.zh-CN.md`。

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
| ClickHouse 26.8 LTS | `http://localhost:18123`（HTTP），`localhost:19000`（Native TCP） | 用户 `framework`，密码 `framework_password` |
| Nacos 2.5.1 | `http://localhost:18848/nacos` | 本地开发环境关闭认证 |

这些基础设施容器独立于示例 API 启动。ClickHouse 通过 HTTP 8123（宿主机 18123）和 Native TCP 9000（宿主机 19000）提供服务。当前示例 API 的 smoke request 只验证 HTTP API 到 gRPC Provider/Consumer 的调用链；基础设施容器启动本身不代表业务 API 已完成集成验证。

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

旧的 `framework-test`、`provider-test`、`migration-test`、`full-regression` Compose runner 已从当前环境移除，不要继续使用旧命令。当前 IntegrationTests 的宿主机执行命令为：

```bash
dotnet test --project tests/Aspros.Basic.Framework.IntegrationTests/Aspros.Basic.Framework.IntegrationTests.csproj --configuration Release
```

Migration Fixture 是独立可执行项目，不属于 IntegrationTests；根据本次发布决策，Migration 回归明确排除。独立 NuGet 包消费验证单独执行：

```bash
bash scripts/verify-packaged-consumer.sh
```

构建 API 镜像时，Docker 会编译两个示例应用及其引用的 Framework 项目，不会运行 Framework IntegrationTests。
