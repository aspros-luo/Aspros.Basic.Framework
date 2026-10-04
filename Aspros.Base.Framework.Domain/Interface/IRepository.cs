namespace Aspros.Base.Framework.Domain;

public interface IRepository<out TAggregateRoot>
    where TAggregateRoot : class, IAggregateRoot
{
    IQueryable<TAggregateRoot> GetAll();
}

/// <summary>
/// Minimal domain-facing abstraction for obtaining a queryable entity set.
/// Infrastructure implementations can adapt EF Core, Dapper-backed projections,
/// or another persistence technology without coupling the domain project to it.
/// </summary>
public interface IEntitySetProvider
{
    IQueryable<TEntity> Set<TEntity>() where TEntity : class;
}
