using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aspros.Base.Framework.Infrastructure.Recommendation;

public static class RecommendationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the optional, provider-agnostic recommendation ranking service.
    /// Business services must register one IRecommendationProvider per object type.
    /// </summary>
    public static IServiceCollection AddFrameworkRecommendation(
        this IServiceCollection services,
        Action<RecommendationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<RecommendationOptions>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddScoped<IRecommendationService, RecommendationService>();
        return services;
    }

    public static IServiceCollection AddRecommendationProvider<TProvider>(
        this IServiceCollection services)
        where TProvider : class, IRecommendationProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IRecommendationProvider, TProvider>();
        return services;
    }
}
