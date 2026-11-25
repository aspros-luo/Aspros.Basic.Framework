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


## 13. 通用个性化推荐

推荐能力是可选模块，不会由 \`AddAsprosFramework\` 自动启用，也不绑定 ClickHouse、Redis 或某种业务对象。

### 注册

每一种推荐对象类型注册一个 Provider。Provider 负责读取该类型的用户画像、召回候选对象，并执行租户、上下架、库存、地域等业务过滤：

\`\`\`csharp
builder.Services.AddFrameworkRecommendation(options =>
{
    options.SimilarityWeight = 0.65;
    options.QualityWeight = 0.20;
    options.PopularityWeight = 0.10;
    options.FreshnessWeight = 0.05;
});

builder.Services.AddRecommendationProvider<ProductRecommendationProvider>();
builder.Services.AddRecommendationProvider<MerchantRecommendationProvider>();
builder.Services.AddRecommendationProvider<ArticleRecommendationProvider>();
\`\`\`

Provider 的 \`ObjectType\` 必须唯一，例如 \`product\`、\`merchant\`、\`article\`。每种对象类型拥有自己的特征语义空间；不要把商品的“品牌偏好”和文章的“主题偏好”直接混入同一个向量。

### 发起推荐

\`\`\`csharp
public sealed class HomeFeedService(IRecommendationService recommendations)
{
    public Task<RecommendationResult> GetProductsAsync(
        long userId,
        CancellationToken cancellationToken)
        => recommendations.RecommendAsync(
            new RecommendationRequest
            {
                UserId = userId.ToString(),
                ObjectType = "product",
                TenantId = "tenant-1",
                Scene = "home",
                Page = 1,
                PageSize = 20,
                CandidateLimit = 500
            },
            cancellationToken);
}
\`\`\`

推荐结果只返回业务对象 ID、排序分数和分数组成。业务服务再通过 Redis 批量读取详情、对未命中项回源 MySQL，并按推荐结果中的 ID 顺序组装响应。推荐模块不依赖具体业务 Entity 或 DTO。

### Provider 契约

- \`GetUserProfileAsync\` 返回该用户在当前 \`ObjectType\` 下的稀疏特征向量。
- \`GetCandidatesAsync\` 返回有界候选集；多路召回应由 Provider 合并并按 ObjectId 去重后返回。
- 用户向量和候选向量必须使用相同的特征命名与含义、相同的 FeatureSchemaVersion；同一批候选必须使用同一个版本。特征值必须是有限的非负数。
- \`QualityScore\`、\`PopularityScore\`、\`FreshnessScore\` 必须预先归一化到 \`[0, 1]\`。
- Provider 必须执行可信的租户隔离和业务资格过滤；请求中的 Context 只能作为场景参数，不能作为授权依据。
- \`CandidateLimit\` 限制本次召回窗口，不代表整个业务目录的总量。排序在该候选窗口内完成，再进行稳定分页。
- 没有用户画像或画像为零向量时，服务会忽略相似度权重，按质量、热度和新鲜度退化排序。

### 排序说明与边界

默认权重只是可解释的起始值，不是通用最优值。上线前应使用离线样本和线上实验调整权重。第一版使用余弦相似度与线性加权，不包含模型训练、协同过滤、跨对象类型混排或多样性重排。

推荐服务不负责详情回填、曝光埋点或行为事件投递。业务服务应在实际曝光时记录 impression，而不是把“已返回推荐结果”误当成“用户已看到”；点击、收藏、购买等事件再通过业务事务与 CAP Outbox 连接到分析链路。
