using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Registers CAP using the business service's DbContext as the local message persistence boundary.
/// 使用业务服务自己的 DbContext 注册 CAP，并把本地数据库作为消息持久化边界。
///
/// <para>
/// CAP supports durable event delivery through a local message store. It addresses
/// the business-data/message consistency gap without creating a cross-service ACID transaction.
/// CAP 通过本地消息存储解决业务数据与消息之间的一致性缺口，而不是创建跨服务 ACID 事务。
/// </para>
/// </summary>
public static class FrameworkCapExtensions
{
    /// <summary>
    /// Registers CAP storage through Entity Framework and lets the application
    /// configure the transport and remaining CAP options.
    /// 使用 EF Core 注册 CAP 本地存储，并由业务服务配置 RabbitMQ、Redis Streams 等传输方式。
    /// </summary>
    public static IServiceCollection AddFrameworkCap<TDbContext>(
        this IServiceCollection services,
        Action<CapOptions>? configure = null)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCap(options =>
        {
            options.UseEntityFramework<TDbContext>();
            configure?.Invoke(options);
        });

        return services;
    }
}
