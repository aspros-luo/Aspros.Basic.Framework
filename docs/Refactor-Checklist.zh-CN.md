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

## 下一轮重点

- [ ] 把外部 HTTP 调用统一收敛到 IHttpClientFactory/typed client，减少 Flurl 直接调用。
- [ ] 抽象 Nacos 服务发现到统一 ServiceDiscovery 契约，供 HTTP/gRPC 共用。
- [ ] 增加统一的 gRPC deadline、错误码和 metadata 处理。
- [ ] 评估是否需要把 Web API 返回模型统一成 ApiResponse<T>，避免现有 Success + ActionFilter 的重复包装风险。
- [ ] 逐步减少 ServiceLocator 的遗留使用，并最终将其标记为 deprecated。
- [ ] 评估 Polly legacy API 与 Microsoft.Extensions.Http.Resilience 的整合，避免两套 resiliency 配置并存。
- [ ] 增加核心单元测试和最小集成测试。
- [ ] 根据 Xr.User / Xr.Category / Xr.Identity 的真实使用继续做兼容性修正。
- [ ] 最后再考虑 package version、NuGet 发布和 .NET 10 multi-target，而不是现在提前扩展。

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
