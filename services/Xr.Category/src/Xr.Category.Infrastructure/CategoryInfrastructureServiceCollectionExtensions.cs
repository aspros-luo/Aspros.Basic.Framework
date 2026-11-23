using Aspros.Base.Framework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Xr.Category.Infrastructure;

public static class CategoryInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddCategoryInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Category");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Category must be configured.");
        }

        services.AddAsprosDbContext<CategoryDbContext>(
            options => options.UseMySQL(connectionString));

        return services;
    }
}
