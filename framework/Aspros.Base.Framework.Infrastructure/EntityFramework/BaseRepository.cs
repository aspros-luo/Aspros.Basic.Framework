namespace Aspros.Base.Framework.Infrastructure.EntityFramework;

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Aspros.Base.Framework.Domain.Entities;
using Aspros.Base.Framework.Domain.Repositories;

/// <summary>
/// 基础仓储通用实现，仅依赖 IEntitySetProvider 与 IDbContext
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public class BaseRepository<TEntity, TKey>(IDbContext dbContext) : IRepository<TEntity, TKey>
    where TEntity : Entity<TKey>
{
    protected readonly IDbContext DbContext = dbContext;
    protected DbSet<TEntity> DbSet => DbContext.Set<TEntity>();

    public virtual IQueryable<TEntity> Query() => DbSet;

    public virtual IQueryable<TEntity> QueryAsNoTracking() => DbSet.AsNoTracking();

    public virtual ValueTask<TEntity?> FindAsync(TKey id, CancellationToken cancellationToken = default) =>
        DbSet.FindAsync([id], cancellationToken);

    public virtual Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        DbSet.FirstOrDefaultAsync(predicate, cancellationToken);

    public virtual Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        DbSet.AnyAsync(predicate, cancellationToken);

    public virtual Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default) =>
        predicate == null ? DbSet.LongCountAsync(cancellationToken) : DbSet.LongCountAsync(predicate, cancellationToken);

    public virtual async ValueTask InsertAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        await DbSet.AddAsync(entity, cancellationToken);

    public virtual Task InsertRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default) =>
        DbSet.AddRangeAsync(entities, cancellationToken);

    public virtual void Update(TEntity entity) => DbSet.Update(entity);

    public virtual void UpdateRange(IEnumerable<TEntity> entities) => DbSet.UpdateRange(entities);

    public virtual void Delete(TEntity entity)
    {
        if (entity is ISoftDelete softDelete)
        {
            softDelete.IsDeleted = true;
            softDelete.DeletedTime = DateTimeOffset.UtcNow;
            DbSet.Update(entity);
        }
        else
        {
            DbSet.Remove(entity);
        }
    }

    public virtual void DeleteRange(IEnumerable<TEntity> entities)
    {
        foreach (var entity in entities)
        {
            Delete(entity);
        }
    }
}

/// <summary>
/// 默认主键为 long 的基础仓储实现
/// </summary>
public class BaseRepository<TEntity>(IDbContext dbContext) : BaseRepository<TEntity, long>(dbContext), IRepository<TEntity>
    where TEntity : Entity<long>
{
}
