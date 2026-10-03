using Aspros.Base.Framework.Domain;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Aspros.Base.Framework.Infrastructure
{
    public static class IServiceCollectionExtension
    {
        public static void AutoInject(this IServiceCollection services)
        {
            services.InjectService();
        }

        private static void InjectService(this IServiceCollection services)
        {
            var transientType = typeof(ITransient);
            var scopedType = typeof(IScoped);
            var singletonType = typeof(ISingleton);

            var allTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .ToArray();

            var classTypes = allTypes
                .Where(type => type is { IsClass: true, IsAbstract: false })
                .ToArray();

            var mediatRAssemblies = classTypes
                .Where(type => type.GetInterfaces().Any(IsMediatRHandlerInterface))
                .Select(type => type.Assembly)
                .Distinct()
                .ToArray();

            var pipelineBehaviors = classTypes
                .Where(type =>
                    type.IsGenericTypeDefinition &&
                    type.GetInterfaces().Any(interfaceType =>
                        interfaceType.IsGenericType &&
                        interfaceType.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>)))
                .OrderBy(type => type.FullName)
                .ToArray();

            var mediatRAlreadyRegistered = services.Any(descriptor =>
                descriptor.ServiceType == typeof(IMediator));

            if (!mediatRAlreadyRegistered && (mediatRAssemblies.Length > 0 || pipelineBehaviors.Length > 0))
            {
                services.AddMediatR(configuration =>
                {
                    if (mediatRAssemblies.Length > 0)
                    {
                        configuration.RegisterServicesFromAssemblies(mediatRAssemblies);
                    }

                    foreach (var behaviorType in pipelineBehaviors)
                    {
                        configuration.AddOpenBehavior(behaviorType);
                    }
                });
            }
            else
            {
                // An application may have already registered MediatR for its own assembly.
                // Complete the registration for handlers discovered in other loaded assemblies
                // without registering the same descriptor twice.
                foreach (var classType in classTypes)
                {
                    foreach (var serviceInterface in classType.GetInterfaces().Where(IsMediatRHandlerInterface))
                    {
                        var alreadyRegistered = services.Any(descriptor =>
                            descriptor.ServiceType == serviceInterface &&
                            descriptor.ImplementationType == classType);

                        if (!alreadyRegistered)
                        {
                            services.AddTransient(serviceInterface, classType);
                        }
                    }
                }

                foreach (var behaviorType in pipelineBehaviors)
                {
                    var alreadyRegistered = services.Any(descriptor =>
                        descriptor.ServiceType == typeof(MediatR.IPipelineBehavior<,>) &&
                        descriptor.ImplementationType == behaviorType);

                    if (!alreadyRegistered)
                    {
                        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), behaviorType);
                    }
                }
            }

            foreach (var classType in classTypes)
            {
                var implementedInterfaces = classType.GetInterfaces();

                foreach (var eventHandlerInterface in implementedInterfaces.Where(IsLegacyEventHandlerInterface).Distinct())
                {
                    services.Add(new ServiceDescriptor(
                        eventHandlerInterface,
                        classType,
                        ServiceLifetime.Transient));
                }

                // Repository implementations are transient by convention. Skip marker
                // registration for repositories so a repository contract is not registered twice.
                var isRepository = implementedInterfaces.Any(IsRepositoryInterface);
                if (isRepository)
                {
                    RegisterRepository(services, classType, implementedInterfaces);
                }

                if (!isRepository)
                {
                    RegisterByMarker(services, classType, implementedInterfaces, transientType, ServiceLifetime.Transient);
                }
                RegisterByMarker(services, classType, implementedInterfaces, scopedType, ServiceLifetime.Scoped);
                RegisterByMarker(services, classType, implementedInterfaces, singletonType, ServiceLifetime.Singleton);
            }
        }

        private static bool IsMediatRHandlerInterface(Type interfaceType)
        {
            if (!interfaceType.IsGenericType)
            {
                return false;
            }

            var genericType = interfaceType.GetGenericTypeDefinition();
            return genericType == typeof(IRequestHandler<>) ||
                   genericType == typeof(IRequestHandler<,>) ||
                   genericType == typeof(INotificationHandler<>) ||
                   genericType == typeof(IStreamRequestHandler<,>);
        }

        private static bool IsRepositoryInterface(Type interfaceType)
            => interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IRepository<>);

        private static bool IsLegacyEventHandlerInterface(Type interfaceType)
            => interfaceType.IsGenericType &&
               interfaceType.GetGenericTypeDefinition() ==
               typeof(Aspros.Base.Framework.Application.Abstractions.Events.IEventHandler<>);

        private static void RegisterRepository(
            IServiceCollection services,
            Type implementationType,
            IEnumerable<Type> implementedInterfaces)
        {
            foreach (var serviceInterface in implementedInterfaces.Where(IsRepositoryContractToRegister).Distinct())
            {
                services.Add(new ServiceDescriptor(serviceInterface, implementationType, ServiceLifetime.Transient));
            }

            services.Add(new ServiceDescriptor(implementationType, implementationType, ServiceLifetime.Transient));
        }

        private static bool IsRepositoryContractToRegister(Type interfaceType)
            => interfaceType != typeof(ITransient) &&
               interfaceType != typeof(IScoped) &&
               interfaceType != typeof(ISingleton) &&
               !interfaceType.IsGenericTypeDefinition &&
               (interfaceType.GetInterfaces().Any(IsRepositoryInterface) || IsRepositoryInterface(interfaceType));

        private static void RegisterByMarker(
            IServiceCollection services,
            Type implementationType,
            IEnumerable<Type> implementedInterfaces,
            Type markerType,
            ServiceLifetime lifetime)
        {
            if (!implementedInterfaces.Contains(markerType))
            {
                return;
            }

            var serviceInterfaces = implementedInterfaces
                .Where(interfaceType =>
                    interfaceType != markerType &&
                    interfaceType != typeof(ITransient) &&
                    interfaceType != typeof(IScoped) &&
                    interfaceType != typeof(ISingleton) &&
                    !interfaceType.IsGenericTypeDefinition)
                .Distinct()
                .ToArray();

            if (serviceInterfaces.Length == 0)
            {
                services.Add(new ServiceDescriptor(implementationType, implementationType, lifetime));
                return;
            }

            foreach (var serviceInterface in serviceInterfaces)
            {
                services.Add(new ServiceDescriptor(serviceInterface, implementationType, lifetime));
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(System.Reflection.Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException exception)
            {
                return exception.Types.OfType<Type>();
            }
        }
    }
}
