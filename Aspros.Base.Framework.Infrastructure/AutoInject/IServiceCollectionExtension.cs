using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Aspros.Base.Framework.Infrastructure
{
    public static class IServiceCollectionExtension
    {
        public static void AutoInject(this IServiceCollection services)
        {
            // register assembly 
            services.InjectService();
        }

        private static void InjectService(this IServiceCollection services)
        {
            #region 依赖注入

            var mediatRType = typeof(IBaseRequest); //MediatR
            var transientType = typeof(ITransient); //每次新建
            var scopedType = typeof(IScoped); //作用域
            var singletonType = typeof(ISingleton); //全局唯一

            //获取实现了接口自动注入接口 的程序集
            var allTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes()
                .Where(t =>
                    t.GetInterfaces().Contains(mediatRType) || t.GetInterfaces().Contains(transientType) ||
                    t.GetInterfaces().Contains(scopedType) || t.GetInterfaces().Contains(singletonType)));
            //class的程序集
            var classTypes = allTypes.Where(x => x.IsClass).ToArray();
            //接口的程序集
            var interfaceTypes = allTypes.Where(x => x.IsInterface).ToArray();
            foreach ( var classType in classTypes) { 
            
            
                var interfaceType = interfaceTypes.FirstOrDefault(x => x.IsAssignableFrom(classType));
                // 注入MediatR
                if (classType.GetInterfaces().Contains(mediatRType)) services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(classType.Assembly));

                //判断class有接口，用接口注入
                if (interfaceType != null)
                {
                    //判断用什么方式注入
                    if (interfaceType.GetInterfaces().Contains(transientType)) services.AddTransient(interfaceType, classType);
                    if (interfaceType.GetInterfaces().Contains(scopedType)) services.AddScoped(interfaceType, classType);
                    if (interfaceType.GetInterfaces().Contains(singletonType)) services.AddSingleton(interfaceType, classType);
                }
                else //class没有接口，直接注入class
                {
                    //判断用什么方式注入
                    if (classType.GetInterfaces().Contains(transientType)) services.AddTransient(classType);
                    if (classType.GetInterfaces().Contains(scopedType)) services.AddScoped(classType);
                    if (classType.GetInterfaces().Contains(singletonType)) services.AddSingleton(classType);
                }

            }
            #endregion
        }
    }
}
