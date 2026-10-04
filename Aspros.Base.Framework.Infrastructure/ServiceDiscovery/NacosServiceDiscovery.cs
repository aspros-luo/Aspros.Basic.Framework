using Nacos.V2;
using System.Collections.ObjectModel;

namespace Aspros.Base.Framework.Infrastructure;

public sealed class NacosServiceDiscovery(
    INacosNamingService namingService) : IServiceDiscovery
{
    private readonly INacosNamingService _namingService =
        namingService ?? throw new ArgumentNullException(nameof(namingService));

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
