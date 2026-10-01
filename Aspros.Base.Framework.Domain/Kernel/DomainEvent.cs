namespace Aspros.Base.Framework.Domain.Kernel;

/// <summary>
/// 领域事件基础实现。
/// </summary>
public abstract class DomainEvent : IDomainEvent
{
    protected DomainEvent()
    {
        OccurredOn = DateTime.UtcNow;
    }

    public DateTime OccurredOn { get; }
}
