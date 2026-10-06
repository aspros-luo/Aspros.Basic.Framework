using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Provides opt-in resilience registration for outbound HTTP calls.
/// 提供按需启用的 HTTP 出站弹性能力注册。
///
/// <para>
/// The standard pipeline combines timeout, retry, circuit-breaker and concurrency
/// limiting strategies. Unsafe HTTP methods are excluded from retries by default
/// to reduce duplicate-write amplification.
/// 标准管线包含超时、重试、熔断和并发限制；默认关闭不安全 HTTP 方法的重试，
/// 避免写操作在下游故障时被重复放大。
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

        AddFrameworkResilience(builder);
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

        AddFrameworkResilience(builder);
        return builder;
    }

    private static void AddFrameworkResilience(IHttpClientBuilder builder)
    {
        builder.AddStandardResilienceHandler(options =>
        {
            options.Retry.DisableForUnsafeHttpMethods();
        });
    }
}
