using Aspros.Base.Framework.Application.Abstractions.Events;

namespace Aspros.Base.Framework.Infrastructure
{
    public interface IEventHandler<in TEvent> : Application.Abstractions.Events.IEventHandler<TEvent>
        where TEvent : Application.Abstractions.Events.IEvent
    {
    }
}
