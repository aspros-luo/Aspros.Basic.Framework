using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// EF Core registration helpers used by business microservices.
/// 业务微服务使用的 EF Core 注册辅助方法。
///
/// <para>
/// The business service still controls provider, connection string, migrations and other DbContext options.
/// 数据库 Provider、连接字符串、Migration 以及其他 DbContext 配置仍然由业务服务自己决定。
/// </para>
/// </summary>
public static class EntityFrameworkServiceCollectionExtensions
{
    /// <summary>
    /// Registers a business DbContext and exposes the same instance through IDbContext.
    /// 注册业务 DbContext，并通过 IDbContext 暴露同一个 Scoped 实例。
    ///
    /// <para>
    /// This removes the repeated AddDbContext + AddScoped&lt;IDbContext&gt; boilerplate
    /// without hiding database-provider configuration.
    /// 该方法只消除 AddDbContext + AddScoped&lt;IDbContext&gt; 的重复代码，
    /// 不会隐藏数据库 Provider 等业务服务自己的配置。
    /// </para>
    /// </summary>
    public static IServiceCollection AddAsprosDbContext<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> optionsAction)
        where TContext : DbContext, IDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsAction);

        services.AddDbContext<TContext>(optionsAction);
        services.TryAddScoped<IDbContext>(
            serviceProvider => serviceProvider.GetRequiredService<TContext>());

        return services;
    }
}