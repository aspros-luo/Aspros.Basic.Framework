using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aspros.Base.Framework.Infrastructure;

public static class PermissionServiceCollectionExtensions
{
    public static IServiceCollection AddFrameworkPermissionValidation(
        this IServiceCollection services,
        Action<PermissionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<PermissionOptions>();

        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.AddFrameworkServiceDiscovery();
        return services;
    }
}
