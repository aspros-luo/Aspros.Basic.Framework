# Aspros Basic Framework

> **Release candidate:** `2.0.0-rc.1` targeting `.NET 10`. The current validation path is Docker infrastructure deployment plus real Core Provider/Consumer API calls. Framework xUnit and migration regression execution is paused.

面向 .NET 10 的轻量级业务基础框架，保持“少而够用”的工程边界。

升级决策、消费者迁移步骤和验收门槛：
- [REFACTORING_PLAN.md](REFACTORING_PLAN.md)
- [中文升级与重构计划](docs/REFACTORING_PLAN.zh-CN.md)

## 当前定位

框架保持“少而够用”：

- Domain 只承载领域模型、仓储契约和最小抽象，不依赖 Infrastructure。
- Infrastructure 负责 EF Core、Dapper、HTTP、gRPC、缓存、消息和运行时集成。
- EF Core 作为默认持久化方案；Dapper 用于复杂 SQL、报表和特殊性能场景。
- UnitOfWork 默认不强制事务；需要多个写操作原子提交时再显式开启事务。
- IEvent 是可选的进程内事件机制，不提供可靠投递；跨服务最终一致性优先使用 MQ / CAP / MassTransit 等持久化消息方案。
- gRPC 提供统一的 Server Hosting 与 ClientFactory 注册入口，业务服务仍自己维护 proto 和具体 RPC 契约。
- 自动注入支持显式程序集扫描，并保留 AutoInject() 兼容入口。

## 项目结构

| 项目 | 职责 |
| --- | --- |
| Aspros.Base.Framework.Abstractions | DI 标记、事件等最小通用契约 |
| Aspros.Base.Framework.Domain | Entity、Aggregate Root、Repository 契约 |
| Aspros.Base.Framework.Infrastructure | EF Core、Dapper、UoW、Web API、gRPC、缓存、消息等基础设施 |

依赖方向：

    Abstractions <- Domain <- Infrastructure

业务服务再依赖 Domain / Infrastructure；Domain 不再反向依赖 Infrastructure。

## gRPC

服务端：

    builder.Services.AddFrameworkGrpc();
    app.MapFrameworkGrpcService<YourGrpcService>();

客户端：

    builder.Services.AddFrameworkGrpcClient<YourGrpc.YourGrpcClient>(
        new Uri("https://service-address"));

业务服务保留自己的 proto，框架只负责统一宿主和客户端基础能力。

## Dapper

EF Core 仍是默认路径；复杂 SQL 可以直接注入 IDapperExecutor。

    public sealed class ReportService(IDapperExecutor dapper)
    {
        public Task<IEnumerable<UserRow>> QueryAsync(
            CancellationToken cancellationToken) =>
            dapper.QueryAsync<UserRow>(
                "select id, name from user where is_deleted = 0",
                cancellationToken: cancellationToken);
    }

开启事务后，Dapper 会复用当前 UnitOfWork 的数据库事务。

## UnitOfWork

普通单表或单次 SaveChanges 业务：

    await unitOfWork.RegisterNew(entity);
    await unitOfWork.CommitAsync();

需要显式原子事务时：

    unitOfWork.BeginTransaction();

    await unitOfWork.RegisterDirty(first);
    await unitOfWork.RegisterDirty(second);

    await unitOfWork.CommitAsync();

框架不再在 RegisterNew/RegisterDirty/RegisterDeleted 内部偷偷 SaveChanges，也不再主动关闭或 Dispose EF Core 管理的 DbConnection。

使用外部事务时，事务必须已经属于同一个 DbContext。UnitOfWork 的 `CommitAsync` 只把变更写入该事务，不会替外部调用方提交或释放它；外部事务的回滚也必须由其所有者执行。
已由 Repository 查询得到并处于跟踪状态的实体，`RegisterDirty` 会保留 EF Core 的属性级变更，不会强制把整行所有字段标记为修改。传入游离实体时则按完整对象更新处理，不应使用只含部分字段的 DTO 直接调用。

## 事件

IEvent / IEventHandler 是可选的进程内事件机制。

    await eventBus.PublishAsync(new SomethingChangedEvent(...));

它不承担 MQ 的可靠投递、重试和持久化职责。需要跨服务最终一致性时，应使用当前业务环境中的 MQ / CAP / MassTransit 能力。

## Redis Cache

框架不另外包装 `IDistributedCache`。业务服务直接使用 .NET 提供的 Redis Provider：

```csharp
var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Redis connection string is required.");

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnection;
    options.InstanceName = "my-service:";
});
```

业务服务注入 `IDistributedCache` 后即可使用 `GetStringAsync` / `SetStringAsync`。缓存 Key、TTL 和一致性策略仍由业务服务决定；`AddAsprosFramework` 不会隐式连接 Redis。

## 自动注入

推荐显式指定扫描程序集：

    builder.Services.AddAutoInject(
        typeof(SomeApplicationService).Assembly);

兼容旧项目：

    builder.Services.AutoInject();

自动注入不会覆盖已有显式注册，并会自动发现 IEventHandler<T> 与 MediatR 请求程序集。

## Core 示例

- samples/Framework.Core.Contracts/core.proto：业务 RPC 契约。
- samples/Framework.Core.Provider：gRPC Provider。
- samples/Framework.Core.Consumer：调用 Provider 的 HTTP Web API。

Provider 使用 HTTP/2 gRPC（http://localhost:7041），并通过 http://localhost:8080/health 提供 HTTP 健康检查。Consumer 通过 http://localhost:7141 暴露 HTTP API，并通过框架注册的 gRPC ClientFactory 调用 Provider。

## 典型业务流

    Controller -> Application/Service -> Repository -> Db
                             |
                             +-> optional in-process event
                             |
                             +-> durable MQ / CAP / MassTransit

当前代码基线为 .NET 10；框架本身保持业务无关，EF Core 10 作为默认 ORM 基线，MySQL 迁移夹具使用 Oracle MySQL EF Core 10 Provider。

## 本地 API Smoke Test

先启动 Provider：

    dotnet run --project samples/Framework.Core.Provider

再启动 Consumer：

    dotnet run --project samples/Framework.Core.Consumer

直接调用 Consumer HTTP API：

    curl "http://localhost:7141/core/ping?message=hello"

Provider 健康检查地址为 http://localhost:8080/health，gRPC 地址为 http://localhost:7041。可在 Rider 或 Visual Studio 中打开 samples/Framework.Core.Consumer/SmokeTests.http 执行示例请求。

## Service Discovery

When Nacos is already configured by the application, the framework can expose a small service-discovery abstraction:

    builder.Services.AddFrameworkServiceDiscovery();

    public sealed class TradeClient(IServiceDiscovery discovery)
    {
        public Task<ServiceEndpoint?> ResolveAsync(
            CancellationToken cancellationToken) =>
            discovery.GetHealthyEndpointAsync(
                "xr.trade",
                cancellationToken: cancellationToken);
    }

The framework returns a ServiceEndpoint containing the selected healthy instance address and metadata. Nacos remains an infrastructure detail. The current implementation uses the Nacos healthy-instance selector provided by the C# SDK.

## Permission validation

For applications that need the legacy permission middleware:

    builder.Services.AddFrameworkPermissionValidation(options =>
    {
        options.ServiceName = "saas-system";
        options.GroupName = "DEFAULT_GROUP";
        options.ValidationPath = "/system/user.permission.valid";
    });

    app.UseFrameworkPermissionValidation();

Permission checks fail closed: an unavailable permission service returns 503 and a denied permission returns 403.

## One-line runtime registration

Most business services can start with:

    builder.Services.AddAsprosFramework(
        typeof(SomeApplicationService).Assembly,
        typeof(SomeRepository).Assembly);

This registers the common runtime services: HttpContextAccessor, WorkContext, UnitOfWork, DapperExecutor and in-process EventBus, then scans the supplied business assemblies with AddAutoInject.

Feature integrations remain explicit:

    builder.Services.AddFrameworkGrpc();
    builder.Services.AddFrameworkServiceDiscovery();
    builder.Services.AddFrameworkPermissionValidation();

For service-to-service gRPC calls made from inside another gRPC handler, call-context propagation can be enabled explicitly:

    builder.Services
        .AddFrameworkGrpcClient<YourGrpc.YourGrpcClient>(
            new Uri("https://service-address"))
        .PropagateGrpcCallContext();

This propagates the parent gRPC deadline and cancellation context. For clients that are also used outside a gRPC call context, pass true to suppressMissingContextErrors when appropriate.

## Microservice reliability

The Framework now exposes opt-in foundations for common microservice reliability concerns:

- CAP registration for durable messaging with the business DbContext.
- Standard outbound HTTP resilience: timeout, retry, circuit breaker and concurrency limiting.
- ASP.NET Core inbound rate limiting.
- Separate liveness/readiness health endpoints.
- Explicit guidance for idempotency, distributed locks, cascading-failure protection and Saga/compensation boundaries.

These capabilities are intentionally not enabled by `AddAsprosFramework`. Business services choose their policies.

See:
- `docs/Microservice-Reliability.md`
- `docs/Microservice-Reliability.zh-CN.md`


## External Package Consumer Sample

samples/Framework.Core.Api remains as a standalone .NET 10 API example that consumes the Infrastructure package through PackageReference. It is not part of the active Docker environment workflow. The current runnable Docker API path is the Core Provider/Consumer pair described above.

## Database Code First / Migration

Database schema generation follows the standard EF Core Code First path: Entity/Aggregate → explicit mapping → DbContext → Migration → SQL.

数据库生成遵循标准 EF Core Code First：Entity/Aggregate → 显式 Mapping → DbContext → Migration → SQL。

The optional CLI is packaged as `Aspros.Basic.Framework.Tools` (the commands below assume the `2.0.0-rc.1` tool package has been published or packed locally):

    # After the release candidate is published to NuGet:
    dotnet tool install --global Aspros.Basic.Framework.Tools --version 2.0.0-rc.1 --prerelease

    aspros db migration add InitialCreate --project ./Xr.Trade.Infrastructure --startup-project ./Xr.Trade.Api --context TradeDbContext
    aspros db migration script --project ./Xr.Trade.Infrastructure --startup-project ./Xr.Trade.Api --context TradeDbContext --idempotent

`update` requires explicit `--allow-update` and is intended for development/test databases. Production should normally use a reviewed SQL script or migration bundle. See `docs/Database-CodeFirst-Migration-Roadmap.md` and its Chinese counterpart.

## Docker API Environment

The active Docker workflow starts MySQL, Redis, RabbitMQ, Nacos and ClickHouse together with the Core Provider and Consumer APIs:

    docker compose -f dev-environment/docker-compose.yml --profile infra --profile api up -d --build

Then call the HTTP APIs:

    curl http://localhost:7141/health
    curl "http://localhost:7141/core/ping?message=hello"

The complete port map, credentials, logs and shutdown commands are maintained in dev-environment/README.zh-CN.md and dev-environment/README.md.

Framework xUnit and migration test sources remain in tests/, but their Docker execution is intentionally paused and is not a current release or environment-startup gate.

## 2.0.0-rc.1 Migration note

`BasicEntity.Status` now uses `Aspros.Base.Framework.Domain.ValueObjects.EntityStatus` instead of the root-namespace `Status` enum. This avoids collisions with business-owned status models; update explicit enum references when moving to this package line.

## Business Services

The `services/` directory contains deployable business services and is kept separate from `samples/` (framework examples). Each service owns its solution and runtime entry point.

The first service scaffold is [Xr.Category](services/Xr.Category/README.md). It uses the four-layer structure and the current Framework source projects. The service has its own solution and is not added to the Framework solution.
