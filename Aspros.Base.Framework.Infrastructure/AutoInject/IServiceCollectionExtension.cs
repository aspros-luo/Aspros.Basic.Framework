using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Aspros.Base.Framework.Infrastructure;

public static class IServiceCollectionExtension
{
    /// <summary>
    /// Backward-compatible entry point. It scans currently loaded assemblies.
    /// Use AddAutoInject to explicitly control the assemblies when startup order
    /// or plugin loading makes that preferable.
    /// </summary>
    public static void AutoInject(this IServiceCollection services)
    {
        services.AddAutoInject();
    }

    public static IServiceCollection AddAutoInject(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);

        var scanAssemblies = (assemblies is { Length: > 0 }
                ? assemblies
                : AppDomain.CurrentDomain.GetAssemblies())
            .Where(a => !a.IsDynamic)
            .Distinct()
            .ToArray();

        var allTypes = scanAssemblies
            .SelectMany(GetLoadableTypes)
            .Where(t => t is { IsClass: true, IsAbstract: false });

        var registrationTypes = allTypes.ToArray();

        RegisterMediatR(services, registrationTypes);
        RegisterEventHandlers(services, registrationTypes);

        foreach (var implementationType in registrationTypes)
        {
            var interfaces = implementationType.GetInterfaces();

            var lifetime = GetLifetime(interfaces);
            if (lifetime is null)
            {
                continue;
            }

            var serviceInterfaces = interfaces
                .Where(i =>
                    i != typeof(ITransient) &&
                    i != typeof(IScoped) &&
                    i != typeof(ISingleton) &&
                    i != typeof(IEvent) &&
                    !IsEventHandlerInterface(i) &&
                    !typeof(IBaseRequest).IsAssignableFrom(i))
                .Distinct()
                .ToArray();

            if (serviceInterfaces.Length == 0)
            {
                Register(services, implementationType, implementationType, lifetime.Value);
                continue;
            }

            foreach (var serviceInterface in serviceInterfaces)
            {
                Register(services, serviceInterface, implementationType, lifetime.Value);
            }
        }

        return services;
    }

    private static void RegisterMediatR(
        IServiceCollection services,
        IReadOnlyCollection<Type> implementationTypes)
    {
        var requestAssemblies = implementationTypes
            .Where(t => typeof(IBaseRequest).IsAssignableFrom(t))
            .Select(t => t.Assembly)
            .Distinct()
            .ToArray();

        if (requestAssemblies.Length == 0)
        {
            return;
        }

        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblies(requestAssemblies));
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
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
