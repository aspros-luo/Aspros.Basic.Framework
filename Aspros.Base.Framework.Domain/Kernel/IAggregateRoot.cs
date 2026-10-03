namespace Aspros.Base.Framework.Domain.Kernel;

/// <summary>
/// 新版聚合根契约，同时兼容旧版空标记 IAggregateRoot。
/// </summary>
public interface IAggregateRoot : Aspros.Base.Framework.Domain.IAggregateRoot
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents(IEnumerable<IDomainEvent> domainEvents);
}
