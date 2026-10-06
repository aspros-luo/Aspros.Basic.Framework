using Nacos.V2;
using System.Collections.ObjectModel;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
    /// Nacos-backed implementation of the framework service-discovery abstraction.
    /// 基于 Nacos 的 Framework 服务发现实现。
    /// 业务代码依赖 IServiceDiscovery，而不是直接依赖 Nacos SDK。
    /// </summary>
    public sealed class NacosServiceDiscovery(
        INacosNamingService namingService) : IServiceDiscovery
{
    private readonly INacosNamingService _namingService =
        namingService ?? throw new ArgumentNullException(nameof(namingService));

    /// <summary>
    /// Selects one healthy instance from Nacos and converts it to the framework endpoint model.
    /// 从 Nacos 选择一个健康实例，并转换为 Framework 自己的服务地址模型。
    /// </summary>
    public async Task<ServiceEndpoint?> GetHealthyEndpointAsync(
        string serviceName,
        string groupName = "DEFAULT_GROUP",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
        cancellationToken.ThrowIfCancellationRequested();

        var instance = await _namingService.SelectOneHealthyInstance(
            serviceName,
            groupName);

        cancellationToken.ThrowIfCancellationRequested();

        if (instance is null ||
            string.IsNullOrWhiteSpace(instance.Ip) ||
            instance.Port <= 0)
        {
            return null;
        }

        var metadata = instance.Metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(
                instance.Metadata,
                StringComparer.OrdinalIgnoreCase);

        var scheme = metadata.TryGetValue("secure", out var secure)
                     && bool.TryParse(secure, out var isSecure)
                     && isSecure
            ? Uri.UriSchemeHttps
            : Uri.UriSchemeHttp;

        var builder = new UriBuilder(
            scheme,
            instance.Ip,
            instance.Port);

        return new ServiceEndpoint(
            builder.Uri,
            new ReadOnlyDictionary<string, string>(metadata));
    }
}
