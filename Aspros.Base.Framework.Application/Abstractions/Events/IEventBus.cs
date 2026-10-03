namespace Aspros.Base.Framework.Application.Abstractions.Events;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event) where TEvent : IEvent;
}
