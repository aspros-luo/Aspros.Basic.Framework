using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Provides standard liveness/readiness health-check endpoints.
/// 提供标准的存活探针与就绪探针入口。
///
/// <para>
/// Dependencies should be tagged with "ready" when their availability determines
/// whether the instance can receive traffic. Liveness should remain lightweight.
/// 当依赖决定实例能否接收流量时，应标记为 "ready"；liveness 应保持轻量。
/// </para>
/// </summary>
public static class FrameworkHealthCheckExtensions
{
    /// <summary>
    /// Registers framework health-check infrastructure and a lightweight self check.
    /// 注册 Framework 健康检查基础设施与轻量级自身检查。
    /// </summary>
    public static IServiceCollection AddFrameworkHealthChecks(
        this IServiceCollection services,
        Action<IHealthChecksBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = services.AddHealthChecks()
            .AddCheck(
                "framework",
                () => HealthCheckResult.Healthy(),
                tags: ["live", "ready"]);

        configure?.Invoke(builder);
        return services;
    }

    /// <summary>
    /// Maps separate liveness and readiness endpoints.
    /// 映射独立的存活与就绪探针。
    /// </summary>
    public static IEndpointRouteBuilder MapFrameworkHealthChecks(
        this IEndpointRouteBuilder endpoints,
        string livenessPath = "/health/live",
        string readinessPath = "/health/ready")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(livenessPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(readinessPath);

        endpoints.MapHealthChecks(
            livenessPath,
            new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("live")
            });

        endpoints.MapHealthChecks(
            readinessPath,
            new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready")
            });

        return endpoints;
    }
}
