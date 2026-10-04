using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aspros.Base.Framework.Infrastructure;

public static class ServiceDiscoveryExtensions
{
    public static IServiceCollection AddFrameworkServiceDiscovery(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddTransient<IServiceDiscovery, NacosServiceDiscovery>();
        return services;
    }
}
