using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Aspros.Base.Framework.RAG.Hosting;

public static class RagWorkerServiceCollectionExtensions
{
    /// <summary>
    /// Registers an optional ingestion worker. The application supplies the handler;
    /// queue storage must be registered separately, for example with AddRagEntityFrameworkCore.
    /// </summary>
    public static IServiceCollection AddRagWorker<THandler>(
        this IServiceCollection services,
        Action<RagWorkerOptions>? configure = null)
        where THandler : class, IIngestionJobHandler
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<RagWorkerOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddScoped<IIngestionJobHandler, THandler>();
        services.AddHostedService<RagIngestionWorker>();
        return services;
    }
}
