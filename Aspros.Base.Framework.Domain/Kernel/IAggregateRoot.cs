namespace Aspros.Base.Framework.Domain.Kernel;

public interface IAggregateRoot
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents(IEnumerable<IDomainEvent> domainEvents);
}
