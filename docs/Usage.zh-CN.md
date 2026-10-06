# 使用方式

## 1. 注册框架

旧业务项目可以继续：

    builder.Services.AutoInject();

新项目推荐明确程序集：

    builder.Services.AddAutoInject(
        typeof(SomeApplicationService).Assembly);

## 2. UnitOfWork

简单业务：

    await unitOfWork.RegisterNew(entity);
    await unitOfWork.CommitAsync();

复杂原子业务：

    unitOfWork.BeginTransaction();

    await unitOfWork.RegisterDirty(order);
    await unitOfWork.RegisterDirty(outboxRecord);

    await unitOfWork.CommitAsync();

不要在 Service 外层统一强制开启事务。

## 3. Repository

Domain 中定义接口：

    public interface IUserRepository : IRepository<User>
    {
        IQueryable<User> QueryDetail(long id);
    }

Infrastructure 中实现：

    public class UserRepository : BaseRepository<User>, IUserRepository
    {
        public UserRepository(IDbContext dbContext) : base(dbContext)
        {
        }
    }

Domain 不直接引用 EF Core 或 Infrastructure。

## 4. Dapper

复杂查询或报表可以：

    public sealed class ReportService(IDapperExecutor dapper)
    {
        public Task<IEnumerable<UserRow>> QueryAsync(
            CancellationToken cancellationToken) =>
            dapper.QueryAsync<UserRow>(
                "select id, name from user where is_deleted = 0",
                cancellationToken: cancellationToken);
    }

开启显式 UoW 事务后，Dapper 会使用同一数据库事务。

## 5. gRPC

Provider：

    builder.Services.AddFrameworkGrpc();
    app.MapFrameworkGrpcService<CoreGrpcService>();

Consumer：

    builder.Services.AddFrameworkGrpcClient<CoreService.CoreServiceClient>(
        new Uri("https://core-provider"));

proto 始终归业务服务自己管理，框架只做基础设施接入。

## 6. 进程内事件

    await eventBus.PublishAsync(new SomethingChangedEvent(...));

进程内事件可以用于当前服务内部解耦，但不能代替可靠 MQ。

跨服务推荐：

    DB commit -> durable MQ -> consumer -> own DB

而不是：

    DB commit -> in-memory event -> hope process stays alive

## 7. 服务发现

配置好 Nacos 后可以使用框架统一服务发现契约：

    builder.Services.AddFrameworkServiceDiscovery();

业务代码只依赖 IServiceDiscovery：

    public sealed class TradeClient(IServiceDiscovery discovery)
    {
        public Task<ServiceEndpoint?> ResolveAsync(
            CancellationToken cancellationToken) =>
            discovery.GetHealthyEndpointAsync(
                "xr.trade",
                cancellationToken: cancellationToken);
    }

框架返回健康实例的地址和 metadata，业务代码不需要直接操作 Nacos Instance。

## 8. 权限校验

旧权限中间件现在可以通过 Options 配置：

    builder.Services.AddFrameworkPermissionValidation(options =>
    {
        options.ServiceName = "saas-system";
        options.GroupName = "DEFAULT_GROUP";
        options.ValidationPath = "/system/user.permission.valid";
    });

    app.UseFrameworkPermissionValidation();

权限服务不可用返回 503；权限不足返回 403，不再出现校验失败但请求继续向下执行的问题。

## 9. 一行注册基础运行时

大多数业务服务可以直接：

    builder.Services.AddAsprosFramework(
        typeof(SomeApplicationService).Assembly,
        typeof(SomeRepository).Assembly);

它会注册 HttpContextAccessor、WorkContext、UnitOfWork、DapperExecutor、进程内 EventBus，并扫描传入的业务程序集。

gRPC、Nacos 服务发现、权限校验、MQ 等仍然按需显式注册，避免每个服务启动时自动加载全部能力。

## 10. 异步事务回滚

推荐在异步业务中使用：

    await unitOfWork.BeginTransactionAsync(cancellationToken);

    try
    {
        await unitOfWork.RegisterDirty(first);
        await unitOfWork.RegisterDirty(second);
        await unitOfWork.CommitAsync(cancellationToken);
    }
    catch
    {
        await unitOfWork.RollbackAsync(cancellationToken);
        throw;
    }

Commit 或 Rollback 后当前 UnitOfWork 的事务状态会被清理；回滚还会清除 EF Core 当前 ChangeTracker 中的修改，避免后续误 SaveChanges。

## 11. gRPC 调用上下文传播

如果一个 gRPC 服务内部还要调用下游 gRPC，可以显式开启：

    builder.Services
        .AddFrameworkGrpcClient<YourGrpc.YourGrpcClient>(
            new Uri("https://service-address"))
        .PropagateGrpcCallContext();

这样下游调用可以继承上游 gRPC 的 deadline 和 cancellation。


## 12. 新微服务推荐启动顺序

Framework 不要求所有微服务使用同一套“全家桶”注册方式。推荐按实际能力组合：

    builder.Services.AddAsprosFramework(
        typeof(TradeService).Assembly,
        typeof(TradeRepository).Assembly);

    builder.Services.AddAsprosDbContext<TradeDbContext>(options =>
        options.UseMySql(connectionString, serverVersion));

    builder.Services.AddFrameworkServiceDiscovery();
    builder.Services.AddFrameworkGrpc();

    // 只有使用 CQRS 的服务才注册 MediatR。
    // 只有使用 Redis / CAP / MQ 的服务才注册对应组件。

业务服务最终仍然只负责：

- Domain Entity / Repository Contract
- Application Service / optional Command & Query
- DbContext 与业务 Mapping
- Repository 中的业务查询
- Controller / gRPC Contract

Framework 负责跨服务重复的基础机制，而不是接管业务代码。
