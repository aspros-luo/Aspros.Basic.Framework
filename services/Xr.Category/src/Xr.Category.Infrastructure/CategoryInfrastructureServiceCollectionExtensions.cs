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

        // CAP's MySQL storage provider uses MySqlConnector. Use the same driver
        // for EF Core so CAP and the business DbContext share compatible
        // connections, transactions and parameter types.
        var serverVersion = new MySqlServerVersion(new Version(8, 4, 0));

        services.AddAsprosDbContext<CategoryDbContext>(
            options => options.UseMySql(connectionString, serverVersion));

        return services;
    }
}
