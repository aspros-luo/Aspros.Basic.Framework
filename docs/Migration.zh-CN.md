# 现有业务迁移指南

## 目标

保留现有业务代码，逐步使用新的 Framework runtime 入口。

旧代码常见形式：

    builder.Services.AddHttpContextAccessor();
    builder.Services.AutoInject();
    builder.Services.AddTransient<IEventBus, EventBus>();

新服务推荐：

    builder.Services.AddAsprosFramework(
        typeof(UserApplicationAssemblyMarker).Assembly,
        typeof(UserInfrastructureAssemblyMarker).Assembly);

然后按需打开：

    builder.Services.AddFrameworkGrpc();
    builder.Services.AddFrameworkServiceDiscovery();
    builder.Services.AddFrameworkPermissionValidation();

## 不要在 builder.Services 阶段 BuildServiceProvider

不要这样做：

    var provider = builder.Services.BuildServiceProvider();

这会建立额外的容器，容易让注册生命周期和最终应用容器不一致。

需要在应用启动后读取服务时，使用 app.Services 或把逻辑改成 Options / hosted service / 延迟解析。

## Repository 迁移

现有：

    public class UserRepository : BaseRepository<User>
    {
        public UserRepository(IDbContext dbContext) : base(dbContext)
        {
        }
    }

不需要改变。

新的 Domain Repository contract 只依赖 Domain abstraction；IDbContext 通过 IEntitySetProvider 适配 EF Core Set。

## UnitOfWork 迁移

旧：

    await unitOfWork.RegisterNew(entity);
    // 旧实现可能在 Register 内部立即 SaveChanges

新：

    await unitOfWork.RegisterNew(entity);
    await unitOfWork.CommitAsync();

需要多个写操作原子提交时：

    await unitOfWork.BeginTransactionAsync(cancellationToken);
    await unitOfWork.RegisterDirty(first);
    await unitOfWork.RegisterDirty(second);
    await unitOfWork.CommitAsync(cancellationToken);

## gRPC 迁移

Provider：

    builder.Services.AddFrameworkGrpc();
    app.MapFrameworkGrpcService<CoreGrpcService>();

Consumer：

    builder.Services
        .AddFrameworkGrpcClient<CoreService.CoreServiceClient>(
            new Uri("https://core-provider"))
        .ForwardAuthorizationHeader();

业务 proto 继续归业务服务管理。

## 服务发现迁移

    builder.Services.AddFrameworkServiceDiscovery();

业务只依赖 IServiceDiscovery，不再直接操作 Nacos Instance。

## 迁移原则

先替换启动注册，再逐步替换业务内部旧 API。

不要因为重构框架而一次性改造所有业务服务。
