using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Provides opt-in ASP.NET Core inbound rate limiting registration.
/// 提供按需启用的 ASP.NET Core 入站限流注册。
///
/// <para>
/// The framework does not choose a global quota because limits are business-specific.
/// 业务服务应根据接口成本、用户维度和网关策略自行配置 limiter。
/// </para>
/// </summary>
public static class FrameworkRateLimitingExtensions
{
    /// <summary>
    /// Registers ASP.NET Core rate limiting with the supplied policies.
    /// 使用业务提供的策略注册 ASP.NET Core 限流。
    /// </summary>
    public static IServiceCollection AddFrameworkRateLimiting(
        this IServiceCollection services,
        Action<RateLimiterOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddRateLimiter(configure);
        return services;
    }
}
