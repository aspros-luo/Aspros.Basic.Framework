using Aspros.Base.Framework.RAG.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aspros.Base.Framework.RAG.EntityFrameworkCore;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers RAG persistence services against an application-owned DbContext.
    /// The application must call ModelBuilder.ApplyRagModel() from OnModelCreating.
    /// </summary>
    public static IServiceCollection AddRagEntityFrameworkCore<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IIngestionJobQueue, EfCoreIngestionJobQueue<TContext>>();
        services.AddScoped<EfCoreIndexGenerationActivator<TContext>>();
        return services;
    }
}
