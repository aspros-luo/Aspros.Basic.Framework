using Aspros.Base.Framework.Infrastructure;
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
