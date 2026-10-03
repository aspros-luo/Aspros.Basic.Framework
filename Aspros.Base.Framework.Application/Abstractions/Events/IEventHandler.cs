namespace Aspros.Base.Framework.Application.Abstractions.Events;

public interface IEventHandler<in TEvent> where TEvent : IEvent
{
    Task HandleAsync(TEvent @event);
}
