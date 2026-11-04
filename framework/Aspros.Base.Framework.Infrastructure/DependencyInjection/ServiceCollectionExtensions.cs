namespace Aspros.Base.Framework.Infrastructure.DependencyInjection;

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Aspros.Base.Framework.DependencyInjection;
using Aspros.Base.Framework.Domain.Repositories;
using Aspros.Base.Framework.Domain.UoW;
using Aspros.Base.Framework.Events;
using Aspros.Base.Framework.Infrastructure.Dapper;
using Aspros.Base.Framework.Infrastructure.EntityFramework;
using Aspros.Base.Framework.Infrastructure.Grpc;
using Aspros.Base.Framework.Infrastructure.ServiceDiscovery;
using Aspros.Base.Framework.Infrastructure.WorkContext;
using Aspros.Base.Framework.WorkContext;

/// <summary>
/// Aspros 核心框架极简一键依赖注入扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 一键启用 Aspros 基础微服务运行时：WorkContext、UoW、Dapper、事件总线、服务发现
    /// </summary>
    public static IServiceCollection AddAsprosFramework(this IServiceCollection services)
    {
        // 1. 上下文与租户隔离
        services.AddSingleton<IWorkContextAccessor, AsyncLocalWorkContextAccessor>();

        // 2. 领域仓储与工作单元
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDapperExecutor, DapperExecutor>();
        services.AddScoped(typeof(IRepository<,>), typeof(BaseRepository<,>));
        services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));

        // 3. gRPC 与链路追踪
        services.AddFrameworkGrpc();

        // 4. 服务发现与亲和性
        services.AddFrameworkServiceDiscovery();

        return services;
    }

    /// <summary>
    /// 注册业务 DbContext 并自动绑定为 IDbContext 契约
    /// </summary>
    public static IServiceCollection AddAsprosDbContext<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> optionsAction)
        where TContext : DbContext, IDbContext
    {
        services.AddDbContext<TContext>(optionsAction);
        services.AddScoped<IDbContext>(sp => sp.GetRequiredService<TContext>());
        services.AddScoped<IEntitySetProvider>(sp => sp.GetRequiredService<TContext>());
        return services;
    }

    /// <summary>
    /// 显式程序集扫描自动依赖注入
    /// </summary>
    public static IServiceCollection AddAutoInject(this IServiceCollection services, params Assembly[] assemblies)
    {
        var targetAssemblies = assemblies.Length > 0 ? assemblies : [Assembly.GetCallingAssembly()];

        foreach (var assembly in targetAssemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray()!;
            }

            foreach (var type in types)
            {
                if (!type.IsClass || type.IsAbstract) continue;

                var attr = type.GetCustomAttribute<AutoInjectAttribute>();
                if (attr == null) continue;

                if (attr.AsSelf)
                {
                    RegisterDescriptor(services, type, type, attr.Lifetime, attr.AllowMultiple);
                    continue;
                }

                if (attr.ServiceType != null)
                {
                    RegisterDescriptor(services, attr.ServiceType, type, attr.Lifetime, attr.AllowMultiple);
                    continue;
                }

                var interfaces = type.GetInterfaces();
                if (interfaces.Length == 0)
                {
                    RegisterDescriptor(services, type, type, attr.Lifetime, attr.AllowMultiple);
                }
                else
                {
                    foreach (var iface in interfaces)
                    {
                        RegisterDescriptor(services, iface, type, attr.Lifetime, attr.AllowMultiple);
                    }
                }
            }
        }

        return services;
    }

    /// <summary>
    /// 兼容旧版本调用的自动注入
    /// </summary>
    public static IServiceCollection AutoInject(this IServiceCollection services)
    {
        return services.AddAutoInject(Assembly.GetCallingAssembly());
    }

    private static void RegisterDescriptor(
        IServiceCollection services,
        Type serviceType,
        Type implementationType,
        ServiceLifetime lifetime,
        bool allowMultiple)
    {
        var descriptor = new ServiceDescriptor(serviceType, implementationType, lifetime);
        if (allowMultiple)
        {
            services.Add(descriptor);
        }
        else
        {
            services.TryAdd(descriptor);
        }
    }
}
