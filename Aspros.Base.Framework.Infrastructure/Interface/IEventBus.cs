namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Optional in-process event dispatcher. This is not a durable integration-event bus.
/// </summary>
public interface IEventBus : ITransient
{
    Task PublishAsync<T>(T @event)
        where T : IEvent;
}
