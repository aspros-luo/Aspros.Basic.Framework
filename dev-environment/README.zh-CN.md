# Framework 临时开发 / 测试环境

提供可重复、可销毁的 Docker 验证环境：.NET 10 SDK、EF Core CLI 10.0.12、MySQL 8.4，并预置 Redis、RabbitMQ、Nacos 作为可选基础设施。

## 启动

```bash
cd dev-environment
docker compose up -d mysql
docker compose --profile test run --rm framework-test
```

完整 Provider 回归（真实 MySQL / RabbitMQ / Nacos）：

```bash
docker compose --profile infra --profile provider-test run --rm framework-provider-test
```

进入测试容器：

```bash
docker compose --profile test run --rm framework-test bash
dotnet build Aspros.Base.Framework.sln
dotnet ef --version
```

销毁并删除测试数据库：

```bash
docker compose down -v
```

这不是生产部署配置，也不会创建 GitHub Actions。完整回归使用 `--profile test`；需要消息与服务发现基础设施时，再启动 `infra` profile：

```bash
docker compose --profile infra up -d redis rabbitmq nacos
docker compose --profile test run --rm framework-test
```

完整环境用于后续 MySQL / Redis / RabbitMQ / Nacos / gRPC / CAP 回归。

Provider 级真实基础设施测试：

```bash
# 启动 MySQL、RabbitMQ、Nacos，并执行 Nacos Provider 回归
docker compose --profile infra --profile provider-test run --rm framework-provider-test
```

Provider 测试会真实启动并连接 MySQL、RabbitMQ、Nacos。当前包含 Nacos 注册/发现回归，以及 CAP + RabbitMQ 消息发布/消费回归。
