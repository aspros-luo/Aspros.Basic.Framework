using Aspros.Base.Framework.ClickHouse;
using ClickHouse.Driver;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aspros.Basic.Framework.IntegrationTests;

public sealed class ClickHouseRegistrationTests
{
    [Fact]
    public void AddFrameworkClickHouse_RegistersOneSingletonClient()
    {
        var services = new ServiceCollection();
        services.AddFrameworkClickHouse(options =>
        {
            options.ConnectionString =
                "Host=localhost;Port=8123;Username=default;Password=;Protocol=http";
        });

        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IClickHouseClient>();
        var second = provider.GetRequiredService<IClickHouseClient>();

        Assert.Same(first, second);
        Assert.IsType<ClickHouseClient>(first);
    }

    [Fact]
    public void AddFrameworkClickHouse_RequiresConnectionString()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddFrameworkClickHouse(_ => { }));
    }

    [Fact]
    public void AddFrameworkClickHouseHealthCheck_CanBeAddedToReadinessChecks()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFrameworkClickHouse(options =>
        {
            options.ConnectionString =
                "Host=localhost;Port=8123;Username=default;Password=;Protocol=http";
        });
        services.AddHealthChecks().AddFrameworkClickHouseHealthCheck();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>());
    }
}
