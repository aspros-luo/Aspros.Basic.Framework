using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Provides standard liveness/readiness health-check endpoints.
/// 提供标准的存活探针与就绪探针入口。
///
/// <para>
/// Dependency checks should be registered by the consuming service because the
/// framework cannot know which dependencies are mandatory for a particular service.
/// 依赖检查由业务服务自行注册，因为 Framework 无法判断某个依赖对具体服务是否“必须”。
/// </para>
/// </summary>
public static class FrameworkHealthCheckExtensions
{
    /// <summary>
    /// Registers the framework health-check infrastructure.
    /// 注册 Framework 健康检查基础设施。
    /// </summary>
    public static IServiceCollection AddFrameworkHealthChecks(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHealthChecks();
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

        endpoints.MapHealthChecks(livenessPath);
        endpoints.MapHealthChecks(readinessPath);
        return endpoints;
    }
}
