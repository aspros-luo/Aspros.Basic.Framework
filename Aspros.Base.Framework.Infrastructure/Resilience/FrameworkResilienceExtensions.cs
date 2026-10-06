using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Provides opt-in resilience registration for outbound HTTP calls.
/// 提供按需启用的 HTTP 出站弹性能力注册。
///
/// <para>
/// The standard pipeline combines timeout, retry, circuit-breaker and concurrency
/// limiting strategies. Applications should still tune policies for idempotency
/// and dependency-specific behavior.
/// 标准管线包含超时、重试、熔断和并发限制；业务仍应根据幂等性与下游特征调整策略。
/// </para>
/// </summary>
public static class FrameworkResilienceExtensions
{
    /// <summary>
    /// Adds the .NET standard resilience handler to a named HTTP client.
    /// 为命名 HTTP Client 添加 .NET 标准弹性处理器。
    /// </summary>
    public static IHttpClientBuilder AddFrameworkResilientHttpClient(
        this IServiceCollection services,
        string name,
        Action<HttpClient>? configureClient = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var builder = services.AddHttpClient(name);

        if (configureClient is not null)
        {
            builder.ConfigureHttpClient(configureClient);
        }

        builder.AddStandardResilienceHandler();
        return builder;
    }

    /// <summary>
    /// Adds the standard resilience handler to an existing HTTP client builder.
    /// 为已有 HTTP Client Builder 添加标准弹性处理器。
    /// </summary>
    public static IHttpClientBuilder UseFrameworkResilience(
        this IHttpClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddStandardResilienceHandler();
        return builder;
    }
}
