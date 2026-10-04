namespace Aspros.Base.Framework.Domain;

public interface IRepository<out TAggregateRoot>
    where TAggregateRoot : class, IAggregateRoot
{
    IQueryable<TAggregateRoot> GetAll();
}

public interface IEntitySetProvider
{
    IQueryable<TEntity> Query<TEntity>()
        where TEntity : class;
}
