using Aspros.Base.Framework.Domain;
using Aspros.Base.Framework.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Aspros.Basic.Framework.IntegrationTests;

public sealed class FrameworkCoreBehaviorRegressionTests
{
    public static bool RedisAvailable =>
        !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("ConnectionStrings__TestRedis"));

    [Fact(SkipUnless = nameof(RedisAvailable), SkipType = typeof(FrameworkCoreBehaviorRegressionTests))]
    public async Task RedisDistributedCache_CanWriteAndReadWhenRedisIsAvailable()
    {
        var redis = Environment.GetEnvironmentVariable("ConnectionStrings__TestRedis")!;

        var services = new ServiceCollection();
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redis;
            options.InstanceName = "framework-regression:";
        });

        await using var provider = services.BuildServiceProvider();

        var cache = provider.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
        var key = $"framework-regression-{Guid.NewGuid():N}";

        await cache.SetStringAsync(key, "redis-ok");
        var value = await cache.GetStringAsync(key);

        Assert.Equal("redis-ok", value);
    }

    [Fact]
    public void Paging_NormalizesInvalidValuesAndCalculatesTotalPages()
    {
        var result = new PagingResult<int>([1, 2], totalCount: -1, pageSize: 0);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(0, result.TotalPage);

        var normal = new PagingResult<int>(
            Enumerable.Range(1, 21),
            totalCount: 21,
            pageSize: 10);

        Assert.Equal(3, normal.TotalPage);

        var parameters = new PagingParams(pageNo: 0, pageSize: 0);
        Assert.Equal(1, parameters.PageNo);
        Assert.Equal(10, parameters.PageSize);
        Assert.Equal(0, parameters.Skip);
    }

    [Fact]
    public void StringAndEnumExtensions_ReturnStableRepresentations()
    {
        Assert.Equal("hello_world", "HelloWorld".ToUnderscoreCase());
        Assert.Equal("HelloWorld", "hello_world".ToPascalCase());

        Assert.Equal(
            "active_status",
            RegressionStatus.ActiveStatus.ToString().ToUnderscoreCase());

        Assert.Equal("Enabled", RegressionStatus.Enabled.GetDisplayName());
        Assert.Equal("enabled:Active status", RegressionStatus.Enabled.GetFullName());

        var option = RegressionStatus.Enabled.GetKeyValue();
        var optionType = option.GetType();
        Assert.Equal("Enabled", optionType.GetProperty("name")!.GetValue(option));
        Assert.Equal("Active status", optionType.GetProperty("desc")!.GetValue(option));
    }

    [Fact]
    public void ResultModel_NormalizesSuccessAndFailure()
    {
        var success = ResultModel.Success(123);
        Assert.True(success.IsSuccess);
        Assert.Equal(123, success.Data);

        var failure = ResultModel.Fail(null);
        Assert.False(failure.IsSuccess);
        Assert.Equal(string.Empty, failure.Message);
    }

    [Fact]
    public void DisposableAction_ExecutesOnlyOnce()
    {
        var count = 0;
        var action = new DisposableAction(() => Interlocked.Increment(ref count));

        action.Dispose();
        action.Dispose();
        Parallel.For(0, 10, _ => action.Dispose());

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task UnitOfWork_RegisterNew_StagesUntilCommit()
    {
        var options = new DbContextOptionsBuilder<CoreBehaviorDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new CoreBehaviorDbContext(options);
        var uow = new UnitOfWork(db, new CoreBehaviorWorkContext());

        var entity = new CoreBehaviorEntity();
        Assert.True(await uow.RegisterNew(entity));

        Assert.Equal(EntityState.Added, db.Entry(entity).State);
        Assert.Equal(0, await db.Entities.CountAsync());

        Assert.True(await uow.CommitAsync());

        Assert.Equal(1, await db.Entities.CountAsync());
    }

    [Fact]
    public async Task UnitOfWork_RollbackWithoutExplicitTransaction_ClearsPendingChanges()
    {
        var options = new DbContextOptionsBuilder<CoreBehaviorDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new CoreBehaviorDbContext(options);
        var uow = new UnitOfWork(db, new CoreBehaviorWorkContext());

        Assert.True(await uow.RegisterNew(new CoreBehaviorEntity { Name = "rolled-back" }));
        await uow.RollbackAsync();

        Assert.Empty(db.ChangeTracker.Entries());
        Assert.False(await db.Entities.AnyAsync());
    }

    [Fact]
    public async Task UnitOfWork_SoftDelete_StagesLogicalDelete()
    {
        var options = new DbContextOptionsBuilder<CoreBehaviorDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new CoreBehaviorDbContext(options);
        var uow = new UnitOfWork(db, new CoreBehaviorWorkContext());

        var entity = new CoreBehaviorEntity { Name = "before-delete" };
        db.Entities.Add(entity);
        await db.SaveChangesAsync();

        Assert.True(await uow.RegisterDeleted(entity, isDel: false));
        Assert.True(entity.Deleted);
        Assert.Equal(1, await db.Entities.CountAsync());
        Assert.Equal(0, await db.Entities.CountAsync(x => !x.Deleted));
    }

    [Fact]
    public async Task WorkContext_PrefersAuthenticatedClaims()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim("user_id", "123"),
                new System.Security.Claims.Claim("tenant_id", "456")
            ],
            authenticationType: "Bearer"));

        var context = new WorkContext(
            new HttpContextAccessor { HttpContext = httpContext });

        Assert.Equal(123L, await context.GetUserId());
        Assert.Equal(456L, await context.GetTenantId());
    }
}

internal sealed class CoreBehaviorDbContext(
    DbContextOptions<CoreBehaviorDbContext> options)
    : DbContext(options), IDbContext
{
    public DbSet<CoreBehaviorEntity> Entities => Set<CoreBehaviorEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CoreBehaviorEntity>().HasKey(x => x.Id);
    }
}

internal sealed class CoreBehaviorEntity : BaseEntity
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

internal sealed class CoreBehaviorWorkContext : IWorkContext
{
    public Task<long> GetUserId() => Task.FromResult(99L);
}

internal enum RegressionStatus
{
    [Display(Name = "Enabled", Description = "Active status")]
    Enabled,

    ActiveStatus
}
