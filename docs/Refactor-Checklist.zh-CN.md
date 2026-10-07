# Framework 重构清单

## 已完成

- [x] Domain 不再引用 Infrastructure。
- [x] 增加 Abstractions 层，承载 DI 标记与进程内事件最小契约。
- [x] 保留旧 Infrastructure 命名空间的类型转发，降低已有业务代码迁移成本。
- [x] Repository 基类改为依赖 Domain 的 IEntitySetProvider。
- [x] IDbContext 适配 IEntitySetProvider，保持现有 EF Repository 构造方式可用。
- [x] UnitOfWork 改为“Register 只跟踪变更、Commit 才持久化”。
- [x] 显式事务只在业务需要时开启。
- [x] 不再由 UnitOfWork 主动关闭或 Dispose EF Core 的 DbConnection。
- [x] Rollback 会释放并清理当前事务。
- [x] 软删除批量操作补齐审计字段。
- [x] EventBus 改为依赖注入，支持多个 Handler。
- [x] 明确 IEvent 只表示进程内事件，不承担 MQ 的可靠投递职责。
- [x] AutoInject 支持显式程序集扫描，并使用 TryAdd 避免覆盖已有注册。
- [x] AutoInject 对 ReflectionTypeLoadException 做兼容处理。
- [x] 增加 gRPC Server Hosting 与 ClientFactory 注册能力。
- [x] 增加 DapperExecutor，复杂 SQL 可以复用当前 UoW 连接与事务。
- [x] WorkContext 优先从已认证 Claims 读取 user_id、sub、tenant_id。
- [x] 增加 Core Provider / Consumer gRPC 示例。
- [x] 增加 HTTP smoke test 入口。
- [x] README 更新为当前架构说明。

## 完成判定

本次重构不以“所有未来增强都完成”为结束条件，而以以下验收标准为准：

### 必须满足

- [x] Domain 不直接依赖 Infrastructure。
- [x] Repository / UnitOfWork / DbContext 的接口与实现契约一致。
- [x] UnitOfWork 的 Register 与 Commit 语义明确；显式事务保持按需开启。
- [x] gRPC Provider / Consumer 最小链路已经有真实可运行示例。
- [x] Nacos 服务发现已经抽象为 Framework ServiceDiscovery 契约。
- [x] Permission 依赖服务故障能够返回 503，权限不足返回 403。
- [x] 现有 Xr.User / Xr.Category 的 Framework 使用方式不会因为本轮重构而被静默破坏。
- [x] 中文、英文 README / Usage / Migration / Checklist 与当前 API 方向一致。
- [x] 不增加 GitHub Actions，不依赖 CI 作为本轮验收手段。

### 仍需要真实环境完成

- [ ] 在本地使用可用的 .NET 10 SDK 执行完整 solution build。
- [ ] 启动 gRPC Provider / Consumer，执行 SmokeTests.http。
- [ ] 在真实 Xr.User / Xr.Category 环境运行最小 API 回归。
- [ ] 如果本地 build 暴露问题，再针对实际错误修正。

### 明确属于后续演进，不阻塞本次重构

- [ ] 将所有外部 HTTP 调用统一迁移到 typed client。
- [ ] 更完整的 gRPC error-code / metadata 标准化。
- [ ] ServiceLocator 的最终移除。
- [ ] Polly 与 .NET 新 Resilience API 的进一步统一。
- [ ] NuGet 发布与 .NET 10 multi-target。
- [ ] 更完整的单元测试 / 集成测试矩阵。

这些项目只有在实际业务需要时再做，不为了“框架看起来完整”而增加复杂度。

## 原则

保持轻量，不为了“像完整框架”而增加 CQRS、DDD、Event Sourcing、事务或消息抽象。

正常业务仍优先：

    Controller -> Service/Application -> Repository -> DB

只有实际业务存在异步最终一致性需求时才使用持久化 MQ。

- [x] 增加 IServiceDiscovery / ServiceEndpoint，并将 Nacos 作为默认实现。
- [x] 权限校验改为通过服务发现抽象定位权限服务。
- [x] 权限服务名称、Group 和路径改为 Options 配置。

- [x] 增加 AddAsprosFramework，一次性注册常用基础运行时能力。

- [x] 修复分页的 0 页码、0 页大小和总页数计算边界。
- [x] 修复字符串转换工具的空值与逐字符处理问题。
- [x] 修复 DisposableAction 重复 Dispose 重复执行 action 的问题。
- [x] 为 Snowflake worker/datacenter/sequence 增加范围校验。
- [x] 防止 API Result Filter 二次包装成功响应并保留错误响应。
- [x] 为权限中间件增加框架原生命名空间入口。

- [x] 增加 RollbackAsync，并让同步/异步回滚行为一致。
- [x] 回滚时清理 EF ChangeTracker，避免回滚后的 tracked entity 被再次持久化。
- [x] 将权限中间件实现迁移到框架原生命名空间，旧 SaaS 类型仅作为兼容包装。
- [x] 增加 gRPC deadline/cancellation 上下文的显式传播能力。


## 真实消费者反向提炼审计

- [x] 不再只检查 Framework 自身，同时检查真实业务消费者的基础设施实现。
- [x] 将 EF Core 实体 Mapping 程序集自动发现提炼到 Framework Infrastructure。
- [x] 将全局 QueryFilter 的表达式组合机制提炼到 Framework Infrastructure。
- [x] 业务 DbContext、实体、Mapping 类以及租户规则继续留在业务服务中。
- [x] 增加中英文 EF Core 模型约定说明。
- [ ] 完整 .NET build/runtime smoke test 仍需要在具备 .NET SDK 的环境中完成；当前执行环境没有可用 dotnet SDK。

- [x] 将重复的 AddDbContext + AddScoped<IDbContext> 注册提炼为按需 EF Core 辅助方法，同时保留业务服务自己的数据库 Provider 配置。

- [x] 最终静态审计未发现 Framework 内存在 `BuildServiceProvider()`、裸 `new HttpClient`、`TODO` 或 `NotImplementedException`；旧 `ServiceLocator` 仅作为现有消费者兼容 API 保留。
- [x] 确认没有新增 GitHub Actions workflow。
- [x] 确认可选基础设施仍保持按需启用，没有全部强制塞进基础注册方法。

## 微服务可靠性基础能力

- [x] 增加按需 CAP 注册入口，并绑定业务 DbContext。
- [x] 增加按需 HTTP 标准弹性能力入口。
- [x] 增加按需 ASP.NET Core 限流入口。
- [x] 增加独立 Liveness / Readiness 健康检查入口。
- [x] 明确分布式事务边界：本地事务 + 可靠消息，不做跨服务 2PC。
- [x] 明确幂等、分布式锁、级联故障以及 Saga/补偿的设计边界。
- [x] 可靠性能力不强制塞入 AddAsprosFramework 默认注册。
