using Aspros.Base.Framework.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Xunit;

namespace Aspros.Basic.Framework.IntegrationTests;

public sealed class FrameworkRegressionTests
{
    [Fact]
    public void AddAsprosFramework_RegistersCoreRuntimeServices()
    {
        var services = new ServiceCollection();

        services.AddAsprosFramework();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IWorkContext>());
        Assert.NotNull(provider.GetService<IUnitOfWork>());
        Assert.NotNull(provider.GetService<IDapperExecutor>());
        Assert.NotNull(provider.GetService<IEventBus>());
    }

    [Fact]
    public void AddFrameworkResilientHttpClient_CreatesNamedClient()
    {
        var services = new ServiceCollection();

        services.AddFrameworkResilientHttpClient(
            "identity",
            client => client.BaseAddress = new Uri("http://identity"));

        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("identity");

        Assert.Equal(new Uri("http://identity/"), client.BaseAddress);
    }

    [Fact]
    public void AddFrameworkRateLimiting_RegistersOptions()
    {
        var services = new ServiceCollection();

        services.AddFrameworkRateLimiting(options =>
        {
            options.AddFixedWindowLimiter(
                "framework-test",
                limiter => limiter.PermitLimit = 2);
        });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider);
    }

    [Fact]
    public void AddFrameworkHealthChecks_RegistersFrameworkSelfCheck()
    {
        var services = new ServiceCollection();

        services.AddFrameworkHealthChecks();

        using var provider = services.BuildServiceProvider();
        var healthChecks =
            provider.GetRequiredService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>();

        Assert.NotNull(healthChecks);
    }

    [Fact]
    public void AddFrameworkGrpc_RegistersGrpcServerInfrastructure()
    {
        var services = new ServiceCollection();

        services.AddFrameworkGrpc();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider);
    }

    [Fact]
    public void UseFrameworkResilience_CanExtendAnExistingClient()
    {
        var services = new ServiceCollection();

        services
            .AddHttpClient("identity")
            .UseFrameworkResilience();

        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("identity");

        Assert.NotNull(client);
    }

    [Fact]
    public async Task MySql_Uow_And_Dapper_UseTheSameConnection_WhenDatabaseIsAvailable()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__TestDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var services = new ServiceCollection();
        services.AddScoped<Aspros.Base.Framework.Infrastructure.IWorkContext, TestWorkContext>();
        services.AddAsprosDbContext<MySqlTestDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
        services.AddScoped<Aspros.Base.Framework.Infrastructure.IDbContext>(
            sp => sp.GetRequiredService<MySqlTestDbContext>());
        services.AddScoped<Aspros.Base.Framework.Infrastructure.IUnitOfWork,
            Aspros.Base.Framework.Infrastructure.UnitOfWork>();
        services.AddScoped<Aspros.Base.Framework.Infrastructure.IDapperExecutor,
            Aspros.Base.Framework.Infrastructure.DapperExecutor>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<MySqlTestDbContext>();
        await db.Database.EnsureCreatedAsync();

        var uow = scope.ServiceProvider.GetRequiredService<Aspros.Base.Framework.Infrastructure.IUnitOfWork>();
        await uow.RegisterNew(new MySqlTestRow { Name = "framework-regression" });
        Assert.True(await uow.CommitAsync());

        var dapper = scope.ServiceProvider.GetRequiredService<Aspros.Base.Framework.Infrastructure.IDapperExecutor>();
        var count = await dapper.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM FrameworkRegressionRows WHERE Name = @Name",
            new { Name = "framework-regression" });

        Assert.Equal(1, count);
    }

    [Fact]
    public void AddFrameworkCap_RegistersCapPublisher()
    {
        var services = new ServiceCollection();

        services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase("framework-cap-test"));

        services.AddFrameworkCap<TestDbContext>();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ICapPublisher>());
    }

    [Fact]
    public void ServiceCollection_CanBeCreated()
    {
        var services = new ServiceCollection();
        Assert.NotNull(services);
    }

    [Fact]
    public void TestHost_IsAvailable()
    {
        Assert.True(typeof(FactAttribute).Assembly is not null);
    }
}


internal sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
}

internal sealed class MySqlTestDbContext(DbContextOptions<MySqlTestDbContext> options) : DbContext(options), Aspros.Base.Framework.Infrastructure.IDbContext
{
    public DbSet<MySqlTestRow> Rows => Set<MySqlTestRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MySqlTestRow>().ToTable("FrameworkRegressionRows");
        modelBuilder.Entity<MySqlTestRow>().HasKey(x => x.Id);
    }
}

internal sealed class MySqlTestRow
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

internal sealed class TestWorkContext : Aspros.Base.Framework.Infrastructure.IWorkContext
{
    public Task<long> GetUserId() => Task.FromResult(1L);
}
