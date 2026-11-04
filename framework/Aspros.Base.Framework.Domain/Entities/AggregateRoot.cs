namespace Aspros.Base.Framework.Domain.Entities;

using Aspros.Base.Framework.Events;

/// <summary>
/// 聚合根契约
/// </summary>
public interface IAggregateRoot
{
    IReadOnlyCollection<IEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

/// <summary>
/// 领域聚合根基类，负责维护实体生命周期与领域事件
/// </summary>
/// <typeparam name="TKey">聚合根主键类型</typeparam>
public abstract class AggregateRoot<TKey> : Entity<TKey>, IAggregateRoot
{
    private readonly List<IEvent> _domainEvents = [];

    public IReadOnlyCollection<IEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IEvent @event)
    {
        _domainEvents.Add(@event);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

/// <summary>
/// 默认主键为 long 的聚合根
/// </summary>
public abstract class AggregateRoot : AggregateRoot<long>
{
}
