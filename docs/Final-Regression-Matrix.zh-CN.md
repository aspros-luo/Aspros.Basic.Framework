# Framework 正式发布回归矩阵

本文档是 `2.0.0-rc.1` 当前有效的验收证据矩阵，取代已经退役的 Docker 回归 Runner 命令。当前 Compose 环境用于本地基础设施和示例 API 运行，不承担 xUnit 回归执行职责。

## 发布门槛汇总

| 验收项 | 结果 | 说明 |
|---|---|---|
| Framework Solution Release 构建 | PASS | 用户已确认 Release 构建成功。 |
| Category API Release 构建 | PASS | 用户已确认 API 及 ProjectReference 依赖 Release 构建成功。 |
| IntegrationTests | PASS，另有一项跳过 | 共 62 项：61 通过、0 失败、1 跳过。 |
| Redis / MySQL / Nacos 集成测试 | PASS | 已连接本地基础设施并实际执行。 |
| Category API CAP 业务验证 | PASS | 通过真实 API 使用 RabbitMQ、MySQL 和 CAP EF Storage，验证提交、回滚、重试、重复投递和幂等收据。 |
| HTTP resilience 针对性回归 | PASS | 8 项针对性测试通过。 |
| Framework 独立 Generic Host CAP 测试 | 已知缺口 | 显式启用后等待 Subscriber 投递超时，不计为 PASS。 |
| 独立 NuGet 消费者 | 待执行 — 必需 | 执行 `bash scripts/verify-packaged-consumer.sh`。 |
| Migration 回归 | 排除 | 按明确发布决策排除，不宣称通过。 |

## IntegrationTests 结果

用户提供的 Release 测试结果：

```text
总计：62
失败：0
成功：61
跳过：1
```

唯一跳过的是 `Cap_RabbitMq_CanPublishAndConsumeWithEfStorage`。显式启用该独立 Generic Host 测试时，等待 Subscriber 投递超时。应将其保留为已知测试覆盖缺口。

Category API CAP 验证是另一条独立且成功的真实业务路径。结果为：提交业务记录 1 条、回滚业务记录 0 条；重试投递 2 次、收据 1 条；重复投递 2 次、收据 1 条；回滚消息没有产生收据。CAP 提供至少一次投递，业务幂等由应用负责。该结果不能证明 Framework 所有 Generic Host 注册方式都正常。

## 下一条必需命令

```bash
bash scripts/verify-packaged-consumer.sh
```

该脚本会打包三个 RC NuGet 包，使用隔离 NuGet 缓存从本地包源还原独立的 `Framework.Core.Api`，然后编译、启动，并验证健康检查和 Framework DI 解析。只有真实执行成功后，才能将独立包消费门槛从“待执行”改为 PASS。

## 环境边界

只有在需要时才启动本地基础设施和示例 API：

```bash
docker compose -f dev-environment/docker-compose.yml --profile infra --profile api up -d --build
```

当前 Compose profile 不替代 IntegrationTests 项目。旧的 `framework-test`、`provider-test`、`migration-test`、`full-regression` 服务以及旧包消费者 Runner 已有意从当前 Compose 环境移除，不应再使用它们的旧命令。

容器健康只证明进程及其健康检查端点可响应；API smoke request 只证明实际调用覆盖的路径。启动 MySQL、Redis、RabbitMQ、ClickHouse 或 Nacos，本身不代表 Framework 集成验证通过。

## 发布决策

独立 NuGet 消费门槛通过、CAP 测试已知缺口保留在发布记录中，并获得用户明确授权之前，不得提升 RC 到 `master`，也不得发布稳定版 `2.0.0`。Migration 按明确发布决策排除。
