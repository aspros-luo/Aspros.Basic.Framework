namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Optional in-process event dispatcher. This is not a durable integration-event bus.
/// 可选的进程内事件分发器；它不是可靠的跨服务 Integration Event Bus。
///
/// <para>
/// If the process stops before a handler completes, the event is not persisted for later delivery.
/// 如果进程在 Handler 完成前退出，事件不会被持久化等待之后重新投递。
/// </para>
/// </summary>
public interface IEventBus : ITransient
{
    /// <summary>
    /// Publishes an in-process event to all registered handlers.
    /// 将进程内事件发布给所有已注册 Handler。
    /// </summary>
    Task PublishAsync<T>(T @event)
        where T : IEvent;
}