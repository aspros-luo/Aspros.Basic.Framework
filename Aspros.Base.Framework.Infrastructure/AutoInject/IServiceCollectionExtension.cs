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
            #region 依赖注入

            var mediatRType = typeof(IBaseRequest);
            var requestHandlerTypes = new[]
            {
                typeof(IRequestHandler<>),
                typeof(IRequestHandler<,>)
            };
            var pipelineBehaviorType = typeof(IPipelineBehavior<,>);

            var transientType = typeof(ITransient);
            var scopedType = typeof(IScoped);
            var singletonType = typeof(ISingleton);

            var allTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .ToArray();

            var classTypes = allTypes
                .Where(t => t.IsClass && !t.IsAbstract)
                .ToArray();

            var mediatRAssemblies = classTypes
                .Where(t =>
                    t.GetInterfaces().Contains(mediatRType) ||
                    t.GetInterfaces().Any(i =>
                        i.IsGenericType &&
                        requestHandlerTypes.Contains(i.GetGenericTypeDefinition())))
                .Select(t => t.Assembly)
                .Distinct()
                .ToArray();

            var pipelineBehaviors = classTypes
                .Where(t => t.IsGenericTypeDefinition &&
                            t.GetInterfaces().Any(i =>
                                i.IsGenericType &&
                                i.GetGenericTypeDefinition() == pipelineBehaviorType))
                .OrderBy(t => t.FullName)
                .ToArray();

            if (mediatRAssemblies.Any() || pipelineBehaviors.Any())
            {
                services.AddMediatR(cfg =>
                {
                    if (mediatRAssemblies.Any())
                    {
                        cfg.RegisterServicesFromAssemblies(mediatRAssemblies);
                    }

                    foreach (var behaviorType in pipelineBehaviors)
                    {
                        cfg.AddOpenBehavior(behaviorType);
                    }
                });
            }

            foreach (var classType in classTypes)
            {
                var implementedInterfaces = classType.GetInterfaces();

                if (implementedInterfaces.Contains(transientType))
                {
                    RegisterServices(
                        services,
                        classType,
                        implementedInterfaces,
                        transientType,
                        ServiceLifetime.Transient);
                }

                if (implementedInterfaces.Contains(scopedType))
                {
                    RegisterServices(
                        services,
                        classType,
                        implementedInterfaces,
                        scopedType,
                        ServiceLifetime.Scoped);
                }

                if (implementedInterfaces.Contains(singletonType))
                {
                    RegisterServices(
                        services,
                        classType,
                        implementedInterfaces,
                        singletonType,
                        ServiceLifetime.Singleton);
                }
            }

            #endregion
        }

        private static void RegisterServices(
            IServiceCollection services,
            Type implementationType,
            IEnumerable<Type> implementedInterfaces,
            Type markerType,
            ServiceLifetime lifetime)
        {
            var serviceInterfaces = implementedInterfaces
                .Where(interfaceType =>
                    interfaceType != markerType &&
                    !interfaceType.IsGenericTypeDefinition &&
                    !interfaceType.GetInterfaces().Contains(markerType))
                .ToArray();

            if (serviceInterfaces.Length == 0)
            {
                services.Add(new ServiceDescriptor(
                    implementationType,
                    implementationType,
                    lifetime));

                return;
            }

            foreach (var serviceInterface in serviceInterfaces)
            {
                services.Add(new ServiceDescriptor(
                    serviceInterface,
                    implementationType,
                    lifetime));
            }
        }
    }
}
