using Microsoft.Extensions.DependencyInjection;

namespace Aspros.Base.Framework.Infrastructure;

public static class ServiceDiscoveryExtensions
{
    public static IServiceCollection AddFrameworkServiceDiscovery(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<IServiceDiscovery, NacosServiceDiscovery>();
        return services;
    }
}
