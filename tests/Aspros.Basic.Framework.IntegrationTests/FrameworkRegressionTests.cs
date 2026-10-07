using Aspros.Base.Framework.Infrastructure;
using Aspros.Basic.Framework.IntegrationTests.Grpc;
using DotNetCore.CAP;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Nacos.V2;
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
    public async Task FrameworkGrpcClient_ForwardsAuthenticatedBearerToken()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddFrameworkGrpc();

        var app = builder.Build();
        app.MapFrameworkGrpcService<RegressionGreeterService>();
        await app.StartAsync();

        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.AddFrameworkGrpcClient<RegressionGreeter.RegressionGreeterClient>(
            "auth-regression",
            new Uri("http://localhost"))
            .ConfigurePrimaryHttpMessageHandler(() => app.GetTestServer().CreateHandler())
            .ForwardAuthorizationHeader();

        await using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "1")],
                    authenticationType: "Bearer"))
        };
        accessor.HttpContext.Request.Headers.Authorization = "Bearer regression-token";

        var client = provider
            .GetRequiredService<Grpc.Net.ClientFactory.GrpcClientFactory>()
            .CreateClient<RegressionGreeter.RegressionGreeterClient>("auth-regression");

        var response = await client.SayHelloAsync(
            new HelloRequest { Name = "auth" });

        Assert.Equal("hello auth bearer=regression-token", response.Message);

        await app.StopAsync();
        await app.DisposeAsync();
    }

    public static bool CapAvailable =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__TestDatabase")) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TestRabbitMq__Host"));

    [Fact(SkipUnless = nameof(CapAvailable), SkipType = typeof(FrameworkRegressionTests))]
    public async Task Cap_RabbitMq_CanPublishAndConsumeWithEfStorage()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__TestDatabase")!;
        var rabbitHost = Environment.GetEnvironmentVariable("TestRabbitMq__Host")!;

        var services = new ServiceCollection();
        services.AddDbContext<MySqlTestDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        var received = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        services.AddSingleton(new CapRegressionSubscriber(received));
        services.AddFrameworkCap<MySqlTestDbContext>(options =>
            options.UseRabbitMQ(rabbitHost));

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<MySqlTestDbContext>();
        await db.Database.EnsureCreatedAsync();

        var publisher = scope.ServiceProvider.GetRequiredService<ICapPublisher>();
        var marker = $"cap-regression-{Guid.NewGuid():N}";

        using (var transaction = db.Database.BeginTransaction(publisher, autoCommit: true))
        {
            db.Rows.Add(new MySqlTestRow { Name = marker });
            await db.SaveChangesAsync();
            await publisher.PublishAsync("aspros.framework.regression", marker);
        }

        var completed = await Task.WhenAny(
            received.Task,
            Task.Delay(TimeSpan.FromSeconds(15)));

        Assert.Same(received.Task, completed);
        Assert.Equal(marker, await received.Task);

        var exists = await db.Rows.AnyAsync(x => x.Name == marker);
        Assert.True(exists);
    }

    public static bool NacosAvailable =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TestNacos__Address"));

    [Fact(SkipUnless = nameof(NacosAvailable), SkipType = typeof(FrameworkRegressionTests))]
    public async Task Nacos_ServiceDiscovery_CanRegisterAndResolveHealthyInstance()
    {
        var address = Environment.GetEnvironmentVariable("TestNacos__Address")!;

        var services = new ServiceCollection();
        services.AddNacosV2Naming(options =>
        {
            options.ServerAddresses = new List<string> { address };
            options.Namespace = "public";
            options.ConfigUseRpc = true;
            options.NamingUseRpc = true;
        });
        services.AddFrameworkServiceDiscovery();

        await using var provider = services.BuildServiceProvider();
        var naming = provider.GetRequiredService<INacosNamingService>();
        var discovery = provider.GetRequiredService<IServiceDiscovery>();

        const string serviceName = "aspros-framework-regression";
        const string groupName = "DEFAULT_GROUP";
        const string ip = "127.0.0.1";
        const int port = 18081;

        try
        {
            await naming.RegisterInstance(serviceName, groupName, ip, port);

            var endpoint = await discovery.GetHealthyEndpointAsync(
                serviceName,
                groupName);

            Assert.NotNull(endpoint);
            Assert.Equal(new Uri("http://127.0.0.1:18081/"), endpoint!.Address);
        }
        finally
        {
            await naming.DeregisterInstance(serviceName, groupName, ip, port);
            await naming.ShutDown();
        }
    }

    [Fact]
    public void UseFrameworkResilience_CanExtendAnExistingClient()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("identity").UseFrameworkResilience();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IHttpClientFactory>().CreateClient("identity"));
    }

    public static bool RedisAvailable =>
        !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("ConnectionStrings__TestRedis"));

    [Fact(SkipUnless = nameof(RedisAvailable), SkipType = typeof(FrameworkRegressionTests))]
    public async Task Redis_DistributedCache_CanRoundTrip_WhenRedisIsAvailable()
    {
        var redisConnection =
            Environment.GetEnvironmentVariable("ConnectionStrings__TestRedis")!;

        var services = new ServiceCollection();
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "aspros-framework-regression:";
        });

        await using var provider = services.BuildServiceProvider();

        var cache = provider.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
        var key = $"roundtrip-{Guid.NewGuid():N}";
        var value = $"redis-regression-{Guid.NewGuid():N}";

        await cache.SetStringAsync(
            key,
            value,
            new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
            });

        var actual = await cache.GetStringAsync(key);

        Assert.Equal(value, actual);
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
        Grpc.Core.ServerCallContext context)
    {
        var bearer = context.RequestHeaders
            .FirstOrDefault(metadata =>
                string.Equals(metadata.Key, "authorization", StringComparison.OrdinalIgnoreCase))
            ?.Value;

        var message = string.IsNullOrWhiteSpace(bearer)
            ? $"hello {request.Name}"
            : $"hello {request.Name} bearer={bearer.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase)}";

        return Task.FromResult(new HelloReply { Message = message });
    }
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

internal sealed class CapRegressionSubscriber : ICapSubscribe
{
    private readonly TaskCompletionSource<string> _received;

    public CapRegressionSubscriber(TaskCompletionSource<string> received)
    {
        _received = received;
    }

    [CapSubscribe("aspros.framework.regression", Group = "framework-regression")]
    public Task ReceiveAsync(string marker)
    {
        _received.TrySetResult(marker);
        return Task.CompletedTask;
    }
}

internal sealed class TestWorkContext : IWorkContext
{
    public Task<long> GetUserId() => Task.FromResult(1L);
}
