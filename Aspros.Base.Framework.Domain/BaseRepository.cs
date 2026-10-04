namespace Aspros.Base.Framework.Domain;

/// <summary>
/// Small repository base that depends only on the domain-side set provider abstraction.
/// Concrete data-access concerns stay in Infrastructure.
/// </summary>
public abstract class BaseRepository<TAggregateRoot> : IRepository<TAggregateRoot>
    where TAggregateRoot : class, IAggregateRoot
{
    protected BaseRepository(IEntitySetProvider setProvider)
    {
        ArgumentNullException.ThrowIfNull(setProvider);
        Entities = setProvider.Set<TAggregateRoot>();
    }

    protected IQueryable<TAggregateRoot> Entities { get; }

    public IQueryable<TAggregateRoot> GetAll() => Entities;
}
