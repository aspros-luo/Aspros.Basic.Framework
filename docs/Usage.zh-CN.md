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
