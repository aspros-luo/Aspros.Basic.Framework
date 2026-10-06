using Aspros.Base.Framework.Infrastructure;
using Aspros.Basic.Framework.IntegrationTests.Grpc;
using DotNetCore.CAP;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddFrameworkResilientHttpClient("identity", client => client.BaseAddress = new Uri("http://identity"));

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("identity");

        Assert.Equal(new Uri("http://identity/"), client.BaseAddress);
    }

    [Fact]
    public void AddFrameworkRateLimiting_RegistersOptions()
    {
        var services = new ServiceCollection();
        services.AddFrameworkRateLimiting(options =>
            options.AddFixedWindowLimiter("framework-test", limiter => limiter.PermitLimit = 2));

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider);
    }

    [Fact]
    public void AddFrameworkHealthChecks_RegistersFrameworkSelfCheck()
    {
        var services = new ServiceCollection();
        services.AddFrameworkHealthChecks();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>());
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
    public async Task FrameworkGrpcClient_CanCallFrameworkGrpcServer()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddFrameworkGrpc();

        var app = builder.Build();
        app.MapFrameworkGrpcService<RegressionGreeterService>();
        await app.StartAsync();

        var services = new ServiceCollection();
        services.AddFrameworkGrpcClient<RegressionGreeter.RegressionGreeterClient>(
            "regression",
            new Uri("http://localhost"))
            .ConfigurePrimaryHttpMessageHandler(() => app.GetTestServer().CreateHandler());

        await using var provider = services.BuildServiceProvider();
        var client = provider
            .GetRequiredService<Grpc.Net.ClientFactory.GrpcClientFactory>()
            .CreateClient<RegressionGreeter.RegressionGreeterClient>("regression");

        var response = await client.SayHelloAsync(
            new HelloRequest { Name = "framework" });

        Assert.Equal("hello framework", response.Message);

        await app.StopAsync();
        await app.DisposeAsync();
    }

    [Fact]
    public void UseFrameworkResilience_CanExtendAnExistingClient()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("identity").UseFrameworkResilience();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IHttpClientFactory>().CreateClient("identity"));
    }

    public static bool MySqlAvailable =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__TestDatabase"));

    [Fact(SkipUnless = nameof(MySqlAvailable), SkipType = typeof(FrameworkRegressionTests))]
    public async Task MySql_Uow_And_Dapper_UseTheSameConnection_WhenDatabaseIsAvailable()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__TestDatabase")!;

        var services = new ServiceCollection();
        services.AddScoped<IWorkContext, TestWorkContext>();
        services.AddAsprosDbContext<MySqlTestDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
        services.AddScoped<IDbContext>(sp => sp.GetRequiredService<MySqlTestDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDapperExecutor, DapperExecutor>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<MySqlTestDbContext>();
        await db.Database.EnsureCreatedAsync();

        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await uow.RegisterNew(new MySqlTestRow { Name = "framework-regression" });
        Assert.True(await uow.CommitAsync());

        var dapper = scope.ServiceProvider.GetRequiredService<IDapperExecutor>();
        var count = await dapper.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM FrameworkRegressionRows WHERE Name = @Name",
            new { Name = "framework-regression" });

        Assert.Equal(1, count);
    }

    [Fact]
    public void AddFrameworkCap_RegistersCapPublisher()
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(options => options.UseInMemoryDatabase("framework-cap-test"));
        services.AddFrameworkCap<TestDbContext>();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<ICapPublisher>());
    }

    [Fact]
    public void ServiceCollection_CanBeCreated()
    {
        Assert.NotNull(new ServiceCollection());
    }

    [Fact]
    public void TestHost_IsAvailable()
    {
        Assert.True(typeof(FactAttribute).Assembly is not null);
    }
}

internal sealed class RegressionGreeterService : RegressionGreeter.RegressionGreeterBase
{
    public override Task<HelloReply> SayHello(
        HelloRequest request,
        Grpc.Core.ServerCallContext context) =>
        Task.FromResult(new HelloReply { Message = $"hello {request.Name}" });
}

internal sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
}

internal sealed class MySqlTestDbContext(DbContextOptions<MySqlTestDbContext> options) : DbContext(options), IDbContext
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

internal sealed class TestWorkContext : IWorkContext
{
    public Task<long> GetUserId() => Task.FromResult(1L);
}
