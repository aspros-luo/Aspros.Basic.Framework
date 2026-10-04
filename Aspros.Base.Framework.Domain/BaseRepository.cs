namespace Aspros.Base.Framework.Domain;

public abstract class BaseRepository<TAggregateRoot> : IRepository<TAggregateRoot>
    where TAggregateRoot : class, IAggregateRoot
{
    protected BaseRepository(IEntitySetProvider setProvider)
    {
        ArgumentNullException.ThrowIfNull(setProvider);
        Entities = setProvider.Query<TAggregateRoot>();
    }

    protected IQueryable<TAggregateRoot> Entities { get; }

    public IQueryable<TAggregateRoot> GetAll() => Entities;
