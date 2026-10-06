using Microsoft.Extensions.DependencyInjection;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// In-process event dispatcher only. It provides no durability guarantee.
/// 仅用于进程内事件分发，不提供持久化或可靠投递保证。
///
/// <para>
/// For cross-service reliability, use the project's durable MQ/event mechanism instead.
/// 跨服务且要求可靠性的业务，不应依赖这里的事件总线，而应该使用项目的持久化 MQ/事件机制。
/// </para>
/// </summary>
public sealed class EventBus(IServiceProvider serviceProvider) : IEventBus
{
    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// Resolves all handlers for the event type and invokes them sequentially.
    /// 根据事件类型解析所有 Handler，并按注册顺序依次执行。
    ///
    /// <para>
    /// <c>where T : IEvent</c> is a generic constraint: callers can only publish
    /// types implementing IEvent.
    /// <c>where T : IEvent</c> 是泛型约束：只有实现 IEvent 的类型才能发布。
    /// </para>
    /// </summary>
    public async Task PublishAsync<T>(T @event)
        where T : IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        var handlers = _serviceProvider
            .GetServices<IEventHandler<T>>()
            .ToArray();

        foreach (var handler in handlers)
        {
            await handler.HandleAsync(@event);
        }
    }
}