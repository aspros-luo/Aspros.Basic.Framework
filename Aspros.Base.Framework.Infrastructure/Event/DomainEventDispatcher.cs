using Aspros.Base.Framework.Application.Abstractions.Events;
using Aspros.Base.Framework.Domain.Kernel;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Aspros.Base.Framework.Infrastructure.Event;

/// <summary>
/// 基于 Microsoft DI 的领域事件分发器。
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
            var handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync));

            if (handleMethod is null)
            {
                throw new InvalidOperationException(
                    $"Domain event handler contract '{handlerType.FullName}' does not expose HandleAsync.");
            }

            var handlers = serviceProvider.GetServices(handlerType);
            foreach (var handler in handlers)
            {
                if (handler is null)
                {
                    continue;
                }

                try
                {
                    var task = handleMethod.Invoke(handler, [domainEvent, cancellationToken]) as Task;
                    if (task is null)
                    {
                        throw new InvalidOperationException(
                            $"Domain event handler '{handler.GetType().FullName}' returned an invalid result.");
                    }

                    await task;
                }
                catch (TargetInvocationException exception) when (exception.InnerException is not null)
                {
                    ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                    throw;
                }
            }
        }
    }
}
