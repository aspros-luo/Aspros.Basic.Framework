namespace Aspros.Base.Framework.Infrastructure.ServiceDiscovery;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 服务实例物理端点描述
/// </summary>
public sealed record ServiceEndpoint
{
    public required string ServiceName { get; init; }
    public required string Host { get; init; }
    public required int Port { get; init; }
    public string Scheme { get; init; } = "http";
    public string? Zone { get; init; }
    public double Weight { get; init; } = 1.0;
    public bool Healthy { get; init; } = true;
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    public Uri ToUri() => new($"{Scheme}://{Host}:{Port}");
}

/// <summary>
/// 服务发现核心契约
/// </summary>
public interface IServiceDiscovery
{
    Task<ServiceEndpoint?> GetHealthyEndpointAsync(string serviceName, string? preferredZone = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServiceEndpoint>> GetAllEndpointsAsync(string serviceName, CancellationToken cancellationToken = default);
}

/// <summary>
/// 默认具备机房就近亲和性 (Zone-Affinity) 的服务发现调度器
/// </summary>
public sealed class DefaultServiceDiscovery : IServiceDiscovery
{
    private readonly Dictionary<string, List<ServiceEndpoint>> _registry = new()
    {
        ["xr.trade"] =
        [
            new ServiceEndpoint { ServiceName = "xr.trade", Host = "10.0.1.20", Port = 5001, Scheme = "http", Zone = "zone-east-1a", Weight = 100, Healthy = true },
            new ServiceEndpoint { ServiceName = "xr.trade", Host = "10.0.1.21", Port = 5001, Scheme = "http", Zone = "zone-east-1b", Weight = 100, Healthy = true }
        ],
        ["framework-core-provider"] =
        [
            new ServiceEndpoint { ServiceName = "framework-core-provider", Host = "localhost", Port = 7041, Scheme = "https", Zone = "zone-east-1a", Weight = 100, Healthy = true }
        ]
    };

    public Task<ServiceEndpoint?> GetHealthyEndpointAsync(string serviceName, string? preferredZone = null, CancellationToken cancellationToken = default)
    {
        if (!_registry.TryGetValue(serviceName, out var list) || list.Count == 0)
        {
            return Task.FromResult<ServiceEndpoint?>(null);
        }

        var healthy = list.Where(x => x.Healthy).ToList();
        if (healthy.Count == 0)
        {
            return Task.FromResult<ServiceEndpoint?>(null);
        }

        // 优先同可用区 (Zone-Affinity) 路由匹配
        if (!string.IsNullOrEmpty(preferredZone))
        {
            var zoneMatched = healthy.FirstOrDefault(x => string.Equals(x.Zone, preferredZone, StringComparison.OrdinalIgnoreCase));
            if (zoneMatched != null)
            {
                return Task.FromResult<ServiceEndpoint?>(zoneMatched);
            }
        }

        // 默认按轮询或第一个健康实例
        return Task.FromResult<ServiceEndpoint?>(healthy[0]);
    }

    public Task<IReadOnlyList<ServiceEndpoint>> GetAllEndpointsAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        if (_registry.TryGetValue(serviceName, out var list))
        {
            return Task.FromResult<IReadOnlyList<ServiceEndpoint>>(list.AsReadOnly());
        }
        return Task.FromResult<IReadOnlyList<ServiceEndpoint>>([]);
    }
}

public static class ServiceDiscoveryExtensions
{
    public static IServiceCollection AddFrameworkServiceDiscovery(this IServiceCollection services)
    {
        services.AddSingleton<IServiceDiscovery, DefaultServiceDiscovery>();
        return services;
    }
}
