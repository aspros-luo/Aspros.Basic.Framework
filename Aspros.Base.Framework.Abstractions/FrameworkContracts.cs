namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Marks a service for transient dependency injection.
/// 标记当前类型使用 Transient 生命周期。
///
/// <para>
/// A new instance may be created each time the service is requested.
/// 每次从 DI 容器请求时，都可能得到一个新的实例。
/// </para>
///
/// <para>
/// Kept in the historical namespace for source/binary compatibility.
/// 保留历史命名空间是为了兼容现有业务代码。
/// </para>
/// </summary>
public interface ITransient
{
}

/// <summary>
/// Marks a service for scoped dependency injection.
/// 标记当前类型使用 Scoped 生命周期。
///
/// <para>
/// In ASP.NET Core this normally means one instance per request scope.
/// 在 ASP.NET Core 中通常意味着一次 HTTP Request/Scope 内复用同一个实例。
/// </para>
/// </summary>
public interface IScoped
{
}

/// <summary>
/// Marks a service for singleton dependency injection.
/// 标记当前类型使用 Singleton 生命周期。
///
/// <para>
/// The same instance is reused for the application's lifetime.
/// 整个应用生命周期内通常复用同一个实例。
/// </para>
/// </summary>
public interface ISingleton
{
}

/// <summary>
/// Marker for an optional in-process application/domain event.
/// 可选的进程内 Application/Domain Event 标记接口。
///
/// <para>
/// This contract does not imply distributed delivery or durability.
/// 它不代表消息会通过 MQ 分布式投递，也不保证持久化可靠送达。
/// </para>
///
/// <para>
/// Use a message broker when the business requirement is cross-service or durable
/// eventual consistency.
/// 如果业务要求跨服务通信或可靠的最终一致性，应使用 MQ 等消息系统。
/// </para>
/// </summary>
public interface IEvent
{
}

/// <summary>
/// Handles an optional in-process event.
/// 处理可选的进程内 Event。
///
/// <typeparam name="T">
/// Event type handled by this handler.
/// 当前 Handler 负责处理的 Event 类型。
///
/// The <c>in</c> keyword makes this generic parameter contravariant.
/// <c>in</c> 表示这个泛型参数是“逆变”的，允许 Handler 使用更抽象的事件类型来处理更具体的事件。
/// </typeparam>
/// </summary>
public interface IEventHandler<in T> where T : IEvent
{
    /// <summary>
    /// Handles the event asynchronously.
/// 异步处理 Event。
/// </summary>
    Task HandleAsync(T @event);
}
