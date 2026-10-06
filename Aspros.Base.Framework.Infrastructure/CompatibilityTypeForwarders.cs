using System.Runtime.CompilerServices;

// These forwarders keep the historical Infrastructure-qualified contracts usable
// after the abstractions were moved into the dedicated Abstractions assembly.
// 这些 TypeForwardedTo 用于兼容旧项目：即使契约已经移动到 Abstractions 程序集，
// 旧代码引用 Infrastructure 命名空间时仍可以解析到同一个类型。
[assembly: TypeForwardedTo(typeof(Aspros.Base.Framework.Infrastructure.ITransient))]
[assembly: TypeForwardedTo(typeof(Aspros.Base.Framework.Infrastructure.IScoped))]
[assembly: TypeForwardedTo(typeof(Aspros.Base.Framework.Infrastructure.ISingleton))]
[assembly: TypeForwardedTo(typeof(Aspros.Base.Framework.Infrastructure.IEvent))]
[assembly: TypeForwardedTo(typeof(Aspros.Base.Framework.Infrastructure.IEventHandler<>))]