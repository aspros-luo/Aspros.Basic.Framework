using ClickHouse.Driver;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aspros.Base.Framework.ClickHouse;

/// <summary>
/// Registers the optional ClickHouse client integration.
/// </summary>
public static class ClickHouseServiceCollectionExtensions
{
    /// <summary>
    /// Registers one thread-safe ClickHouse client as a singleton.
    /// The driver owns connection pooling; applications should reuse this client.
    /// </summary>
    public static IServiceCollection AddFrameworkClickHouse(
        this IServiceCollection services,
        Action<FrameworkClickHouseOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new FrameworkClickHouseOptions();
        configure(options);

        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.HttpClientName);

        var connectionString = options.ConnectionString;
        var httpClientName = options.HttpClientName;

        services.AddHttpClient(httpClientName);

        services.TryAddSingleton<ClickHouseClient>(serviceProvider =>
            new ClickHouseClient(
                connectionString,
                serviceProvider.GetRequiredService<IHttpClientFactory>(),
                httpClientName));

        services.TryAddSingleton<IClickHouseClient>(serviceProvider =>
            serviceProvider.GetRequiredService<ClickHouseClient>());

        return services;
    }
}
