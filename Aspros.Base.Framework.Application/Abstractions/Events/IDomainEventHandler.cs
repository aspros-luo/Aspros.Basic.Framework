using Aspros.Base.Framework.Domain.Kernel;

namespace Aspros.Base.Framework.Application.Abstractions.Events;

/// <summary>
/// Application 层领域事件处理器抽象。
/// 领域事件处理器可以协调其他聚合、应用服务或基础设施能力，
/// 但不应把具体消息队列实现带入 Domain 层。
/// </summary>
public interface IDomainEventHandler<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    Task HandleAsync(
        TDomainEvent domainEvent,
        CancellationToken cancellationToken = default);
}
