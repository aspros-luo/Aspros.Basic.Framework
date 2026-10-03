using Microsoft.Extensions.DependencyInjection;

namespace Aspros.Base.Framework.Infrastructure
{
    /// <summary>
    /// Legacy in-process event bus。
    /// 事件处理通过当前 DI Scope 解析，不再依赖全局 ServiceLocator。
    /// </summary>
    public sealed class EventBus(IServiceProvider serviceProvider) : IEventBus, IScoped
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;

        public async Task PublishAsync<T>(T @event) where T : IEvent
        {
            ArgumentNullException.ThrowIfNull(@event);

            var handlers = _serviceProvider
                .GetServices<IEventHandler<T>>()
                .ToArray();

            if (handlers.Length == 0)
            {
                throw new InvalidOperationException(
                    $"No event handler is registered for event type '{typeof(T).FullName}'.");
            }

            foreach (var handler in handlers)
            {
                await handler.HandleAsync(@event);
            }
        }
    }
}
