namespace Aspros.Base.Framework.Domain.Repositories;

using System.Linq.Expressions;
using Aspros.Base.Framework.Domain.Entities;

/// <summary>
/// 实体集合提供者抽象，解耦 Domain 与底层 ORM (如 EF Core DbContext)
/// </summary>
public interface IEntitySetProvider
{
    IQueryable<TEntity> GetSet<TEntity>() where TEntity : class;
}

/// <summary>
/// 纯净领域仓储契约
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">实体主键类型</typeparam>
public interface IRepository<TEntity, TKey> where TEntity : Entity<TKey>
{
    IQueryable<TEntity> Query();
    IQueryable<TEntity> QueryAsNoTracking();

    ValueTask<TEntity?> FindAsync(TKey id, CancellationToken cancellationToken = default);
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    ValueTask InsertAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task InsertRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    void Update(TEntity entity);
    void UpdateRange(IEnumerable<TEntity> entities);

    void Delete(TEntity entity);
    void DeleteRange(IEnumerable<TEntity> entities);
}

/// <summary>
/// 默认主键为 long 的仓储契约
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public interface IRepository<TEntity> : IRepository<TEntity, long> where TEntity : Entity<long>
{
}
