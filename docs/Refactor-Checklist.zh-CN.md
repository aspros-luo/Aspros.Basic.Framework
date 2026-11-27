# Framework 重构清单

> 本清单记录已经完成的工程改造，不再作为发布验收依据。当前验收证据统一见[最终回归矩阵](Final-Regression-Matrix.zh-CN.md)。

## 已完成的工程改造

- [x] 拆分 Abstractions、Domain 和 Infrastructure；Domain 不直接依赖 Infrastructure。
- [x] Repository、UnitOfWork 和 DbContext 契约保持一致；Register 暂存变更，Commit 负责持久化。
- [x] 显式事务按需开启；Rollback 清理 EF 跟踪状态。
- [x] 使用 DI 和多 Handler 分发替代基于 ServiceLocator 的事件分发；明确 IEvent 仅用于进程内事件。
- [x] 改进 AutoInject 程序集扫描与 TryAdd 注册语义。
- [x] 增加 gRPC Hosting 和 typed client 注册辅助能力；protobuf 契约归业务服务所有。
- [x] 增加可复用当前 UnitOfWork 连接和事务的 Dapper 执行能力。
- [x] 增加按需启用的服务发现、CAP、HTTP resilience、限流和健康检查能力。
- [x] 可选基础设施集成不强制塞入 AddAsprosFramework 默认注册。
- [x] 权限中间件迁移到 IHttpClientFactory 和 Microsoft 标准 HTTP resilience pipeline；移除 Framework 自有 Polly 工具及直接 Polly / Flurl 引用。
- [x] 增加 Core Provider / Consumer 示例与中英文使用文档。
- [x] 将 Category 参考 API、通用算法和 ClickHouse 集成纳入 RC。
- [x] 保留 xUnit v3：条件跳过测试依赖 v3 的 SkipUnless / SkipType。

## .NET 10 / 2.0.0-rc.1 状态

- [x] Framework、示例和测试项目使用 net10.0。
- [x] Framework NuGet 包版本为 2.0.0-rc.1。
- [x] Microsoft EF Core / ASP.NET Core 依赖对齐到 10.0.15，CAP 包对齐到 10.0.2。
- [x] 独立 Core API 示例通过 NuGet PackageReference 消费全部五个 Framework 类库包。
- [x] `Aspros.Basic.Framework.Tools` CLI 已配置为对应 RC 版本的 .NET Tool 包。
- [x] Docker Compose 只负责基础设施和示例 API 运行，不承担 Framework xUnit 测试执行。
- [x] Framework Solution Release 构建成功。
- [x] Category API Release 构建成功。
- [x] IntegrationTests：61 通过、0 失败、1 跳过（共 62 项）。
- [x] 8 项 HTTP resilience 定向回归通过。
- [x] Category API CAP 业务验证通过，覆盖提交、回滚、重试和重复 / 幂等投递。
- [ ] 发布批准前执行 `bash scripts/verify-packaged-consumer.sh`，并记录真实结果。
- [ ] Framework 独立 Generic Host CAP 测试仍有超时缺口，不得计为 PASS。
- [ ] Migration 回归按本次发布决策明确排除。

## 设计原则

保持框架轻量。默认业务链路仍然是：

    Controller -> Service/Application -> Repository -> DB

只有多个变更必须原子提交时才显式开启事务。跨服务最终一致性使用可靠消息。不要为了架构完整而强行增加 CQRS、DDD 基础设施、Event Sourcing、事务或消息抽象。

本次发布不新增、不触发 GitHub Actions。独立包消费者门槛通过并获得用户明确授权之前，不得将 RC 提升到 master，也不得发布稳定版 2.0.0。
