using Aspros.Base.Framework.Application.Abstractions.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Aspros.Base.Framework.Infrastructure
{
    /// <summary>
    /// In-process event bus kept for compatibility with existing services.
    /// </summary>
    public sealed class EventBus(IServiceProvider serviceProvider) : IEventBus, IScoped
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;

        public async Task PublishAsync<TEvent>(TEvent @event)
            where TEvent : Application.Abstractions.Events.IEvent
        {
            ArgumentNullException.ThrowIfNull(@event);

            var handlers = _serviceProvider
                .GetServices<Application.Abstractions.Events.IEventHandler<TEvent>>()
                .ToArray();

            if (handlers.Length == 0)
            {
                throw new InvalidOperationException(
                    $"No event handler is registered for event type '{typeof(TEvent).FullName}'.");
            }

            foreach (var handler in handlers)
            {
                await handler.HandleAsync(@event);
            }
        }
    }
}
