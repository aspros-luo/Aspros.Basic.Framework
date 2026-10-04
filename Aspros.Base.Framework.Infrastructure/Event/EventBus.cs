using Microsoft.Extensions.DependencyInjection;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// In-process event dispatcher only.
/// It intentionally provides no durability guarantee; distributed consistency
/// should use the configured message bus / integration-event mechanism.
/// </summary>
public sealed class EventBus(IServiceProvider serviceProvider) : IEventBus
{
    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    public async Task PublishAsync<T>(T @event) where T : IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        var handlers = _serviceProvider.GetServices<IEventHandler<T>>().ToArray();

        foreach (var handler in handlers)
        {
            await handler.HandleAsync(@event);
        }
    }
}
