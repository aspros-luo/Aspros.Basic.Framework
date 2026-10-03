namespace Aspros.Base.Framework.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task<bool> RegisterNew<TEntity>(TEntity entity) where TEntity : class;
    Task<bool> RegisterRangeNew<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;
    Task<bool> RegisterDirty<TEntity>(TEntity entity) where TEntity : class;
    Task<bool> RegisterRangeDirty<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;
    Task<bool> RegisterDeleted<TEntity>(TEntity entity, bool isDel = false) where TEntity : class;
    Task<bool> RegisterRangeDeleted<TEntity>(
        IEnumerable<TEntity> entities,
        bool isDel = false) where TEntity : class;

    /// <summary>
    /// Persist the changes currently tracked by the unit of work.
    /// Returns whether EF Core reported at least one affected row.
    /// </summary>
    Task<bool> CommitAsync(CancellationToken cancellationToken = default);
}