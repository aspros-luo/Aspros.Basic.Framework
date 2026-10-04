using Microsoft.Extensions.DependencyInjection;

namespace Aspros.Base.Framework.Infrastructure
{
    public class EventBus : IEventBus
    {
        public async Task PublishAsync<T>(T @event) where T : IEvent
        {
            var eventHandler = ServiceLocator.Instance.GetService<IEventHandler<T>>();
            await eventHandler.HandleAsync(@event);
        }
    }
}
