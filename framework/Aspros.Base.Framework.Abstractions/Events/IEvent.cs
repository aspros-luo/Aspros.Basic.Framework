namespace Aspros.Base.Framework.Events;

/// <summary>
/// 进程内轻量领域事件契约 (In-Process Domain Event)。
/// 提示：IEvent 仅用于本地解耦，不具备跨进程消息持久化与投递保证；跨微服务事务请使用 CAP / MQ。
/// </summary>
public interface IEvent
{
    /// <summary>
    /// 事件唯一标识
    /// </summary>
    string EventId { get; }

    /// <summary>
    /// 事件产生时间戳
    /// </summary>
    DateTimeOffset OccurredOn { get; }
}

/// <summary>
/// 基础轻量事件实现
/// </summary>
public abstract record BaseEvent : IEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 进程内事件订阅处理器
/// </summary>
/// <typeparam name="TEvent">事件类型</typeparam>
public interface IEventHandler<in TEvent> where TEvent : IEvent
{
    /// <summary>
    /// 处理事件
    /// </summary>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// 进程内事件总线
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// 发布进程内事件给所有已注册的处理程序
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IEvent;
}
