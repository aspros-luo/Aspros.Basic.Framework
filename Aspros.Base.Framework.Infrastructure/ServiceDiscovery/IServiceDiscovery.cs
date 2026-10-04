namespace Aspros.Base.Framework.Infrastructure;

public interface IServiceDiscovery : ITransient
{
    Task<ServiceEndpoint?> GetHealthyEndpointAsync(
        string serviceName,
        string groupName = "DEFAULT_GROUP",
        CancellationToken cancellationToken = default);
}
