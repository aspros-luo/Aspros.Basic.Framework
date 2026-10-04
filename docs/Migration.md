# Migration Guide

## Goal

Keep existing business code working while moving to the new framework runtime entry point.

Legacy:

    builder.Services.AddHttpContextAccessor();
    builder.Services.AutoInject();
    builder.Services.AddTransient<IEventBus, EventBus>();

Recommended:

    builder.Services.AddAsprosFramework(
        typeof(UserApplicationAssemblyMarker).Assembly,
        typeof(UserInfrastructureAssemblyMarker).Assembly);

Optional integrations:

    builder.Services.AddFrameworkGrpc();
    builder.Services.AddFrameworkServiceDiscovery();
    builder.Services.AddFrameworkPermissionValidation();

## Do not call BuildServiceProvider during registration

Avoid:

    var provider = builder.Services.BuildServiceProvider();

This creates a second container and can produce inconsistent lifetimes and singleton instances.

Use app.Services after the application is built, or move the operation to options configuration, a hosted service, or another deferred mechanism.

## Repository

Existing repositories do not need to change:

    public class UserRepository : BaseRepository<User>
    {
        public UserRepository(IDbContext dbContext) : base(dbContext)
        {
        }
    }

The Domain repository base depends only on Domain abstractions. IDbContext adapts the EF Core set through IEntitySetProvider.

## UnitOfWork

Recommended:

    await unitOfWork.RegisterNew(entity);
    await unitOfWork.CommitAsync();

For atomic multi-write operations:

    await unitOfWork.BeginTransactionAsync(cancellationToken);
    await unitOfWork.RegisterDirty(first);
    await unitOfWork.RegisterDirty(second);
    await unitOfWork.CommitAsync(cancellationToken);

## gRPC

Provider:

    builder.Services.AddFrameworkGrpc();
    app.MapFrameworkGrpcService<CoreGrpcService>();

Consumer:

    builder.Services
        .AddFrameworkGrpcClient<CoreService.CoreServiceClient>(
            new Uri("https://core-provider"))
        .ForwardAuthorizationHeader();

Business services continue to own their protobuf contracts.

## Service discovery

    builder.Services.AddFrameworkServiceDiscovery();

Business code depends on IServiceDiscovery rather than the Nacos Instance type.

## Migration principle

Replace startup registration first, then migrate internal APIs gradually.

Do not rewrite every business service merely because the framework is being refactored.
