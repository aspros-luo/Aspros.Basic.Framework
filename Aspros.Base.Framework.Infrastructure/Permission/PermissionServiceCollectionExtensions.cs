using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aspros.Base.Framework.Infrastructure;

public static class PermissionServiceCollectionExtensions
{
    /// <summary>
    /// Registers permission validation configuration, service discovery and its resilient HTTP client.
    /// 注册权限校验配置、服务发现，以及使用 Framework 标准 Resilience Pipeline 的 HTTP Client。
    ///
    /// <para>
    /// The permission client is named and managed by IHttpClientFactory. Its standard
    /// resilience handler provides timeout, retry, circuit breaker and concurrency limiting.
    /// 权限服务客户端由 IHttpClientFactory 管理，并使用标准弹性管线提供超时、重试、熔断和并发限制。
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
        services.AddFrameworkResilientHttpClient(PermissionOptions.HttpClientName);

        return services;
    }
}
