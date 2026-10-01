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

            var interfaceTypes = allTypes
                .Where(t => t.IsInterface)
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
                var interfaceType = interfaceTypes.FirstOrDefault(x => x.IsAssignableFrom(classType));

                if (interfaceType != null)
                {
                    if (interfaceType.GetInterfaces().Contains(transientType))
                        services.AddTransient(interfaceType, classType);

                    if (interfaceType.GetInterfaces().Contains(scopedType))
                        services.AddScoped(interfaceType, classType);

                    if (interfaceType.GetInterfaces().Contains(singletonType))
                        services.AddSingleton(interfaceType, classType);
                }
                else
                {
                    if (classType.GetInterfaces().Contains(transientType))
                        services.AddTransient(classType);

                    if (classType.GetInterfaces().Contains(scopedType))
                        services.AddScoped(classType);

                    if (classType.GetInterfaces().Contains(singletonType))
                        services.AddSingleton(classType);
                }
            }

            #endregion
        }
    }
}