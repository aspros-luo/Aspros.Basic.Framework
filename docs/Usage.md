# Usage

## Dependency registration

Legacy applications can keep:

    builder.Services.AutoInject();

New applications should prefer explicit assemblies:

    builder.Services.AddAutoInject(
        typeof(SomeApplicationService).Assembly);

## UnitOfWork

Simple write:

    await unitOfWork.RegisterNew(entity);
    await unitOfWork.CommitAsync();

Explicit transaction:

    unitOfWork.BeginTransaction();

    await unitOfWork.RegisterDirty(first);
    await unitOfWork.RegisterDirty(second);

    await unitOfWork.CommitAsync();

Do not wrap every service method in a transaction by default.

## Repository

Define the repository contract in Domain:

    public interface IUserRepository : IRepository<User>
    {
        IQueryable<User> QueryDetail(long id);
    }

Implement it in Infrastructure:

    public sealed class UserRepository : BaseRepository<User>, IUserRepository
    {
        public UserRepository(IDbContext dbContext) : base(dbContext)
        {
        }
    }

The Domain project does not directly reference EF Core or Infrastructure.

## Dapper

Use IDapperExecutor for complex SQL and read-model/reporting scenarios:

    public sealed class ReportService(IDapperExecutor dapper)
    {
        public Task<IEnumerable<UserRow>> QueryAsync(
            CancellationToken cancellationToken) =>
            dapper.QueryAsync<UserRow>(
                "select id, name from user where is_deleted = 0",
                cancellationToken: cancellationToken);
    }

When an explicit UnitOfWork transaction is active, Dapper uses the same database transaction.

## gRPC

Provider:

    builder.Services.AddFrameworkGrpc();
    app.MapFrameworkGrpcService<CoreGrpcService>();

Consumer:

    builder.Services
        .AddFrameworkGrpcClient<CoreService.CoreServiceClient>(
            new Uri("https://core-provider"))
        .ForwardAuthorizationHeader();

Business services own their own .proto contracts. The framework owns the common hosting and client-factory integration.

## In-process events

    await eventBus.PublishAsync(new SomethingChangedEvent(...));

Use in-process events only for local decoupling. They are not a replacement for durable MQ.

For cross-service consistency, prefer:

    DB commit -> durable MQ -> consumer -> own DB

## 7. Service discovery

With Nacos already configured by the host application:

    builder.Services.AddFrameworkServiceDiscovery();

Business code depends on IServiceDiscovery:

    public sealed class TradeClient(IServiceDiscovery discovery)
    {
        public Task<ServiceEndpoint?> ResolveAsync(
            CancellationToken cancellationToken) =>
            discovery.GetHealthyEndpointAsync(
                "xr.trade",
                cancellationToken: cancellationToken);
    }

The framework returns a healthy instance endpoint and metadata. Business code does not need to know the Nacos Instance type.

## 8. Permission validation

The legacy permission middleware can now be configured without hard-coded service names:

    builder.Services.AddFrameworkPermissionValidation(options =>
    {
        options.ServiceName = "saas-system";
        options.GroupName = "DEFAULT_GROUP";
        options.ValidationPath = "/system/user.permission.valid";
    });

    app.UseFrameworkPermissionValidation();

Permission failures are fail-closed: unavailable permission service -> 503; denied permission -> 403.

## 9. One-line runtime registration

Most business services can use:

    builder.Services.AddAsprosFramework(
        typeof(SomeApplicationService).Assembly,
        typeof(SomeRepository).Assembly);

This registers the common runtime services and scans only the supplied business assemblies.

gRPC, Nacos service discovery, permission validation and messaging remain explicit opt-in integrations.

## 10. Asynchronous rollback

For asynchronous business flows:

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

Commit and rollback clear the active UnitOfWork transaction. Rollback also clears tracked EF Core changes to prevent an accidental later SaveChanges.

## 11. gRPC call-context propagation

When a gRPC service calls another gRPC service, propagation can be enabled explicitly:

    builder.Services
        .AddFrameworkGrpcClient<YourGrpc.YourGrpcClient>(
            new Uri("https://service-address"))
        .PropagateGrpcCallContext();

This propagates the parent gRPC deadline and cancellation context.

## 12. Recommended startup shape for a new microservice

Framework does not require every service to enable the same feature set. Compose only the capabilities the service actually needs:

    builder.Services.AddAsprosFramework(
        typeof(TradeService).Assembly,
        typeof(TradeRepository).Assembly);

    builder.Services.AddAsprosDbContext<TradeDbContext>(options =>
        options.UseMySql(connectionString, serverVersion));

    builder.Services.AddFrameworkServiceDiscovery();
    builder.Services.AddFrameworkGrpc();

Only services using CQRS need MediatR; Redis, CAP and MQ integrations remain explicit.

The business service owns:

- Domain entities and repository contracts
- Application services and optional commands/queries
- Its DbContext and entity mappings
- Business-specific repository queries
- Controller and gRPC contracts

Framework owns repeated infrastructure mechanics rather than taking ownership of business code.
