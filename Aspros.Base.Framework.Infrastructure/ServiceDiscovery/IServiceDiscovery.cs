namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Abstracts service endpoint discovery from the business application.
/// 将服务地址发现能力从具体注册中心实现中抽离出来。
///
/// <para>
/// Business code depends on this interface instead of Nacos-specific types.
/// 业务代码依赖这个接口，而不是直接依赖 Nacos 的 Instance 等具体类型。
/// </para>
/// </summary>
public interface IServiceDiscovery : ITransient
{
    /// <summary>
    /// Finds a healthy endpoint for a service.
/// 查找指定服务的健康实例地址。
///
/// <param name="serviceName">Registered service name. 注册中心中的服务名。</param>
/// <param name="groupName">Service group. 服务分组，默认 DEFAULT_GROUP。</param>
/// <param name="cancellationToken">Request cancellation token. 请求取消令牌。</param>
/// </summary>
    Task<ServiceEndpoint?> GetHealthyEndpointAsync(
        string serviceName,
        string groupName = "DEFAULT_GROUP",
        CancellationToken cancellationToken = default);
}