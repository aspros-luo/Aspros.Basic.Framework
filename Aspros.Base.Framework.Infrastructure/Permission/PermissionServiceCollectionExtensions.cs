using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aspros.Base.Framework.Infrastructure;

public static class PermissionServiceCollectionExtensions
{
    /// <summary>
    /// Registers permission validation configuration and its service-discovery dependency.
    /// 注册权限校验配置，以及权限服务所依赖的服务发现能力。
    ///
    /// <para>
    /// <paramref name="configure"/> is optional, so the framework can use defaults
    /// while applications can override the service name, group and validation path.
    /// <paramref name="configure"/> 是可选的，业务服务可以直接使用默认值，
    /// 也可以覆盖权限服务名称、分组和校验路径。
    /// </para>
    /// </summary>
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
