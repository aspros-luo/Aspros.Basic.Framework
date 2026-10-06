using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aspros.Base.Framework.Infrastructure;

public static class ServiceDiscoveryExtensions
{
    /// <summary>
/// Registers the Framework service-discovery abstraction.
/// 注册 Framework 服务发现抽象。
///
/// <para>
/// Nacos remains an infrastructure detail. Business code depends on IServiceDiscovery
/// and can therefore be tested without constructing Nacos SDK objects directly.
/// Nacos 仍然属于基础设施细节；业务代码只依赖 IServiceDiscovery，
/// 因此不需要直接创建 Nacos SDK 对象，也更容易测试。
/// </para>
/// </summary>
public static IServiceCollection AddFrameworkServiceDiscovery(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddTransient<IServiceDiscovery, NacosServiceDiscovery>();
        return services;
    }
}
