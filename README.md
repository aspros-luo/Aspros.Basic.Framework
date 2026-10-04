# Aspros Basic Framework

面向 .NET 8 的轻量级业务基础框架，按 .NET 10 的工程思路持续演进。

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

## 事件

IEvent / IEventHandler 是可选的进程内事件机制。

    await eventBus.PublishAsync(new SomethingChangedEvent(...));

它不承担 MQ 的可靠投递、重试和持久化职责。需要跨服务最终一致性时，应使用当前业务环境中的 MQ / CAP / MassTransit 能力。

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

Provider 默认使用 https://localhost:7041。

Consumer 暴露 GET /core/ping，通过框架注册的 gRPC ClientFactory 调用 Provider。

## 典型业务流

    Controller -> Application/Service -> Repository -> Db
                             |
                             +-> optional in-process event
                             |
                             +-> durable MQ / CAP / MassTransit

当前代码以 .NET 8 为基线，设计上避免绑定过多业务能力，为后续 .NET 10 升级保留空间。

## 本地 Smoke Test

先启动 Provider：

    dotnet run --project samples/Framework.Core.Provider

再启动 Consumer：

    dotnet run --project samples/Framework.Core.Consumer

然后访问：

    https://localhost:7141/core/ping?message=hello

也可以直接打开 samples/Framework.Core.Consumer/SmokeTests.http，在 Rider 或 Visual Studio 中执行请求。

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

    app.UsePermissionValid();

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
