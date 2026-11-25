using ClickHouse.Driver;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspros.Base.Framework.ClickHouse;

/// <summary>
/// Checks ClickHouse reachability using the driver's lightweight ping endpoint.
/// </summary>
public sealed class ClickHouseHealthCheck(IClickHouseClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var reachable = await client.PingAsync(cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        return reachable
            ? HealthCheckResult.Healthy("ClickHouse is reachable.")
            : HealthCheckResult.Unhealthy("ClickHouse did not respond to ping.");
    }
}
