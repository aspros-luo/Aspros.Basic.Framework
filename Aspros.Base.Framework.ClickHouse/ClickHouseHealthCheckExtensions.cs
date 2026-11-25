using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspros.Base.Framework.ClickHouse;

/// <summary>
/// Adds the ClickHouse readiness check without changing liveness checks.
/// </summary>
public static class ClickHouseHealthCheckExtensions
{
    public static IHealthChecksBuilder AddFrameworkClickHouseHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "clickhouse")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return builder.AddCheck<ClickHouseHealthCheck>(
            name,
            failureStatus: HealthStatus.Unhealthy,
            tags: ["ready"]);
    }
}
