using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Aspros.Base.Framework.Infrastructure;

public static class IServiceCollectionExtension
{
    /// <summary>
    /// Backward-compatible entry point that scans currently loaded assemblies.
    /// 兼容旧代码的入口：扫描当前已经加载的程序集。
    /// </summary>
    public static void AutoInject(this IServiceCollection services)
    {
        services.AddAutoInject();
    }

    /// <summary>
    /// Registers framework-marked services discovered from the specified assemblies.
    /// 从指定程序集扫描并注册带有 Framework 生命周期标记的服务。
    ///
    /// <para>
    /// If no assemblies are supplied, the method scans currently loaded non-dynamic assemblies.
    /// 不传程序集时，会扫描当前已加载的非动态程序集。
    /// 实际业务项目更推荐显式传入程序集，这样扫描范围更可预测。
    /// </para>
    ///
    /// <para>
    /// <c>params Assembly[]</c> lets callers pass zero or more assemblies directly.
    /// <c>params Assembly[]</c> 允许调用方直接传入零个或多个程序集，编译器会自动组合成数组。
    /// </para>
    /// </summary>
    public static IServiceCollection AddAutoInject(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);

        var scanAssemblies = (assemblies is { Length: > 0 }
                ? assemblies
                : AppDomain.CurrentDomain.GetAssemblies())
            .Where(assembly => !assembly.IsDynamic)
            .Distinct()
            .ToArray();

        var implementationTypes = scanAssemblies
            .SelectMany(GetLoadableTypes)
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Distinct()
            .ToArray();

        RegisterMediatR(services, implementationTypes);
        RegisterEventHandlers(services, implementationTypes);

        foreach (var implementationType in implementationTypes)
        {
            var interfaces = implementationType.GetInterfaces();
            var lifetime = GetLifetime(interfaces);

            if (lifetime is null)
            {
                continue;
            }

            var serviceTypes = interfaces
                .Where(interfaceType =>
                    interfaceType != typeof(ITransient) &&
                    interfaceType != typeof(IScoped) &&
                    interfaceType != typeof(ISingleton) &&
                    interfaceType != typeof(IEvent) &&
                    !IsEventHandlerInterface(interfaceType) &&
                    !typeof(IBaseRequest).IsAssignableFrom(interfaceType))
                .Distinct()
                .ToArray();

            if (serviceTypes.Length == 0)
            {
                Register(services, implementationType, implementationType, lifetime.Value);
                continue;
            }

            foreach (var serviceType in serviceTypes)
            {
                Register(services, serviceType, implementationType, lifetime.Value);
            }
        }

        return services;
    }

    private static void RegisterMediatR(
        IServiceCollection services,
        IReadOnlyCollection<Type> implementationTypes)
    {
        var requestAssemblies = implementationTypes
            .Where(type => typeof(IBaseRequest).IsAssignableFrom(type))
            .Select(type => type.Assembly)
            .Distinct()
            .ToArray();

        if (requestAssemblies.Length == 0)
        {
            return;
        }

        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssemblies(requestAssemblies));
    }

    private static void RegisterEventHandlers(
        IServiceCollection services,
        IReadOnlyCollection<Type> implementationTypes)
    {
        foreach (var implementationType in implementationTypes)
        {
            foreach (var serviceType in implementationType.GetInterfaces()
                         .Where(IsEventHandlerInterface))
            {
                services.TryAddTransient(serviceType, implementationType);
            }
        }
    }

    private static ServiceLifetime? GetLifetime(IEnumerable<Type> interfaces)
    {
        var types = interfaces.ToArray();

        if (types.Contains(typeof(ISingleton)))
        {
            return ServiceLifetime.Singleton;
        }

        if (types.Contains(typeof(IScoped)))
        {
            return ServiceLifetime.Scoped;
        }

        if (types.Contains(typeof(ITransient)))
        {
            return ServiceLifetime.Transient;
        }

        return null;
    }

    private static void Register(
        IServiceCollection services,
        Type serviceType,
        Type implementationType,
        ServiceLifetime lifetime)
    {
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.TryAddSingleton(serviceType, implementationType);
                break;

            case ServiceLifetime.Scoped:
                services.TryAddScoped(serviceType, implementationType);
                break;

            default:
                services.TryAddTransient(serviceType, implementationType);
                break;
        }
    }

    private static bool IsEventHandlerInterface(Type type)
        => type.IsGenericType &&
           type.GetGenericTypeDefinition() == typeof(IEventHandler<>);

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types
                .Where(type => type is not null)
                .Select(type => type!);
        }
    }
}
