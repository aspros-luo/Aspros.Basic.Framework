using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Aspros.Base.Framework.Infrastructure;

public static class FrameworkServiceCollectionExtensions
{

    /// <summary>
    /// Registers the lightweight framework runtime used by most business services.
    /// Optional feature integrations such as gRPC, Nacos discovery and messaging
    /// remain opt-in.
    /// 注册业务服务通常需要的轻量级 Framework 运行时。
    /// gRPC、Nacos 服务发现、消息等扩展能力仍然保持按需启用，避免基础框架“全家桶”。
    /// </summary>
    public static IServiceCollection AddAsprosFramework(
        this IServiceCollection services,
        params Assembly[] businessAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();

        services.TryAddScoped<IWorkContext, WorkContext>();
        services.TryAddScoped<IUnitOfWork, UnitOfWork>();
        services.TryAddScoped<IDapperExecutor, DapperExecutor>();
        services.TryAddTransient<IEventBus, EventBus>();

        if (businessAssemblies is { Length: > 0 })
        {
            services.AddAutoInject(businessAssemblies);
        }

        return services;
    }
}
