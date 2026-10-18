# .NET 10 升级与重构计划

## 分支决策

以 `feature/framework-net10-upgrade` 作为迁移基线。当前工作分支 `feature/framework-net10-completion` 从已审阅提交 `2ff862f14e438c6afb846d18523e4a2a6263008c` 创建；该基线原本比 `master` 超前 37 个提交。本次收尾提交已快进同步到升级分支。

不整体合并 DDD hardening、business-driven-refactor 或 v10-refactor 分支。这些分支与升级基线存在历史分叉，并包含独立的公开 API 和架构变化。先逐项审查，只有确认兼容且有测试覆盖的改进才单独移植。

## 升级目标

- Framework、示例、CLI、集成测试和 Migration Fixture 项目统一使用 `net10.0`。
- Framework NuGet 包统一为 `2.0.0`；目标框架升级属于破坏性包版本变更。
- Microsoft EF Core / ASP.NET Core 包对齐到 `10.0.12`。
- CAP 包对齐到 `10.0.2`。
- MySQL Migration Fixture 使用 Oracle `MySql.EntityFrameworkCore 10.0.9`。
- 不新增、不触发 GitHub Actions workflow。

## 必须保留的架构边界

- 依赖方向为 `Abstractions <- Domain <- Infrastructure`；Domain 不得引用 Infrastructure。
- EF Core 是默认事务持久化路径；Dapper 仅用于明确需要手写 SQL 的场景，并在适用时复用当前连接和事务。
- UnitOfWork 的 Register 操作只暂存变更；`CommitAsync` 才负责持久化。多步骤原子操作显式使用事务。
- 进程内/领域事件不等同于可靠集成消息。跨服务可靠投递应使用业务应用选择的持久化消息与 Outbox 方案。
- gRPC 辅助能力只负责传输接线；业务服务拥有自己的 protobuf 契约。
- 可选基础设施保持显式启用，不应被默认注册方法隐式强制开启。

## 测试平台兼容性

集成测试项目使用 xUnit v3，因为按环境条件跳过的测试依赖 `FactAttribute.SkipUnless` 与 `SkipType`。测试框架和 Visual Studio Adapter 必须保持兼容的 v3 包版本；除非同步重写这些测试，否则不能降回 xUnit v2。

## 消费者迁移步骤

1. 安装 .NET 10 SDK；业务消费者准备好接收破坏性升级时，再将目标框架切换为 `net10.0`。
2. 将消费者使用的 Framework 包成组升级到 `2.0.0`：Domain、Application（若使用）和 Infrastructure。
3. 将直接依赖的 EF Core / ASP.NET Core 包对齐到 10.x，并选择支持 EF Core 10 的数据库 Provider。当前 Migration Fixture 使用 Oracle MySQL Provider，现有生产服务可能需要根据实际数据库驱动单独选择。
4. 对每个消费者执行编译，明确处理源码/API 迁移问题；不要为了兼容而重新引入存在歧义的 Framework 类型别名。
5. 执行 Framework 集成测试、数据库迁移回归、gRPC smoke test 和独立 NuGet 消费者回归。

## 验收命令

独立 NuGet 消费者：

```bash
bash dev-environment/run-core-package-consumer-regression.sh
```

完整临时环境集成与迁移回归：

```bash
cd dev-environment
docker compose --profile infra --profile full-regression run --rm framework-full-regression
```

第二条命令需要 Docker；两条命令都需要可用的 .NET 10 SDK 与 NuGet 包源访问。

## 验收状态

静态检查不等于 Runtime PASS。只有真实执行命令并成功后，验收项才可以标记为 PASS。当前执行环境没有 .NET SDK 或 Docker，因此 solution build、集成测试、数据库迁移、gRPC smoke test 和独立包消费者启动均尚未获得运行时通过结果。

## 本次升级不包含

- 整体合并分叉的 DDD / Application Layer 重构。
- 应用启动时自动执行数据库迁移。
- 强制引入 CQRS、Event Sourcing 或领域事件。
- 新增或触发 GitHub Actions。
- 在运行时与消费者兼容验收通过前发布 NuGet 2.0.0。
