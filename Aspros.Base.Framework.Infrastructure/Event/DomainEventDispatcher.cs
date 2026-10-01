using Aspros.Base.Framework.Application.Abstractions.Events;
using Aspros.Base.Framework.Domain.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspros.Base.Framework.Infrastructure.Event;

/// <summary>
/// 基于 Microsoft DI 的领域事件分发器。
/// 不让 Domain 层依赖 MediatR、CAP 或其他具体消息框架。
/// </summary>
public sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher, IScoped
{
    public async Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var domainEvent in domainEvents)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var handlers = serviceProvider.GetServices(handlerType);

            foreach (var handler in handlers)
            {
                if (handler is null)
                {
                    continue;
                }

                var handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync));
                if (handleMethod is null)
                {
                    throw new InvalidOperationException(
                        $"Domain event handler '{handler.GetType().FullName}' does not expose HandleAsync.");
                }

                var task = handleMethod.Invoke(handler, [domainEvent, cancellationToken]) as Task;
                if (task is null)
                {
                    throw new InvalidOperationException(
                        $"Domain event handler '{handler.GetType().FullName}' returned an invalid result.");
                }

                await task;
            }
        }
    }
}
