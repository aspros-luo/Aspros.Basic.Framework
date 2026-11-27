# Framework 回归测试

## 当前测试入口

xUnit v3 IntegrationTests 项目直接在宿主机执行，不通过 Docker Compose 运行。

在仓库根目录执行：

```bash
dotnet test --project tests/Aspros.Basic.Framework.IntegrationTests/Aspros.Basic.Framework.IntegrationTests.csproj --configuration Release
```

需要 MySQL、Redis、RabbitMQ 或 Nacos 的测试由环境配置控制。只有对应服务可用时才提供相应测试配置。当前发布测试结果见[最终回归矩阵](../docs/Final-Regression-Matrix.zh-CN.md)。

## 已退役的 Compose Runner

旧的 `framework-test`、`framework-provider-test`、`framework-migration-test` 和 `framework-full-regression` 服务已经从 Compose 移除，不要再使用旧命令。

## Migration Fixture

`Aspros.Basic.Framework.MigrationFixture` 是独立可执行 Fixture，不是 xUnit 测试项目。根据本次明确的发布决策，Migration 回归不属于当前发布门槛；这不代表 Migration 已通过。

## 独立 NuGet 消费者

独立 NuGet 包消费验证与 IntegrationTests 分开执行：

```bash
bash scripts/verify-packaged-consumer.sh
```

当前发布门槛状态和已知缺口以[最终回归矩阵](../docs/Final-Regression-Matrix.zh-CN.md)为准。
