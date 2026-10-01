using Aspros.Base.Framework.Domain.Kernel;

namespace Aspros.Base.Framework.Application.Abstractions.Events;

/// <summary>
/// Application 层领域事件分发抽象。
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default);
}
