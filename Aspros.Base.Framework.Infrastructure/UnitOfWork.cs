using Aspros.Base.Framework.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Default EF Core based Unit of Work implementation.
/// Coordinates tracked changes and an optional explicit transaction.
/// 默认的 EF Core Unit of Work 实现。
/// 负责协调 ChangeTracker 中的修改，以及按需开启的显式事务。
/// </summary>
public sealed class UnitOfWork(
    IDbContext dbContext,
    IWorkContext workContext) : IUnitOfWork
{
    private readonly IDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private readonly IWorkContext _workContext =
        workContext ?? throw new ArgumentNullException(nameof(workContext));

    private bool _ownsTransaction;

    /// <summary>
    /// Exposes the current framework database context.
    /// 暴露当前 Framework 数据库上下文。
    /// </summary>
    public IDbContext DbContext => _dbContext;

    public DatabaseFacade Database => _dbContext.Database;

    public IDbConnection Connection => _dbContext.Database.GetDbConnection();

    public IDbContextTransaction? DbContextTransaction { get; private set; }

    /// <summary>
    /// Starts or reuses an explicit transaction.
    /// 开启或复用一个显式事务。
    ///
    /// <para>
    /// A supplied transaction must already be active on this same DbContext.
    /// This UnitOfWork never commits, rolls back or disposes a caller-owned transaction.
    /// 传入的事务必须已在同一个 DbContext 上开启；UnitOfWork 不会提交、回滚或释放外部事务。
    /// </para>
    /// </summary>
    public IDbContextTransaction BeginTransaction(
        IDbContextTransaction? dbContextTransaction = null)
    {
        if (DbContextTransaction is not null)
        {
            return DbContextTransaction;
        }

        if (dbContextTransaction is null)
        {
            DbContextTransaction = Database.BeginTransaction();
            _ownsTransaction = true;
            return DbContextTransaction;
        }

        var currentTransaction = Database.CurrentTransaction;
        if (currentTransaction is null ||
            !ReferenceEquals(
                currentTransaction.GetDbTransaction(),
                dbContextTransaction.GetDbTransaction()))
        {
            throw new ArgumentException(
                "The supplied transaction must already be active on this UnitOfWork DbContext.",
                nameof(dbContextTransaction));
        }

        DbContextTransaction = dbContextTransaction;
        _ownsTransaction = false;
        return DbContextTransaction;
    }

    /// <summary>
    /// Starts an explicit transaction asynchronously and owns the created transaction.
    /// 异步开启显式事务，并由当前 Unit of Work 负责创建出来的事务生命周期。
    /// </summary>
    public async Task<IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        if (DbContextTransaction is not null)
        {
            return DbContextTransaction;
        }

        DbContextTransaction =
            await Database.BeginTransactionAsync(cancellationToken);
        _ownsTransaction = true;
        return DbContextTransaction;
    }

    /// <summary>
    /// Persists staged changes using the default cancellation behavior.
    /// 使用默认取消行为持久化当前暂存修改。
    /// </summary>
    public Task<bool> CommitAsync() => CommitAsync(CancellationToken.None);

    /// <summary>
    /// Saves EF Core changes and commits only a transaction created by this UnitOfWork.
    /// For a caller-owned transaction, this only flushes changes and leaves the transaction open.
    /// 持久化 EF Core 修改；仅提交由当前 UnitOfWork 创建的事务，外部事务只写入但保持开启。
    ///
    /// <para>
    /// Without an explicit transaction, EF Core SaveChanges already provides the
    /// atomicity needed by a normal single SaveChanges operation.
    /// 没有显式事务时，普通的一次 SaveChanges 本身已经具备单次提交所需的原子性，
    /// 因此不应该为了形式额外开启事务。
    /// </para>
    /// </summary>
    public async Task<bool> CommitAsync(CancellationToken cancellationToken)
    {
        var transaction = DbContextTransaction;
        if (transaction is null)
        {
            // A successful commit is not the same as "at least one row changed".
            // SaveChangesAsync returns zero for a valid no-op, which is still a
            // successful unit-of-work completion.
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        try
        {
            // SaveChanges flushes pending changes into the active transaction.
            // Only a transaction created by this UnitOfWork may be committed here.
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (_ownsTransaction)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return true;
        }
        catch
        {
            if (_ownsTransaction)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // Preserve the original persistence exception.
                }

                _dbContext.ClearTrackedChanges();
            }

            // An externally owned transaction belongs to its caller. The caller
            // decides whether to roll it back after a failed SaveChanges.
            throw;
        }
        finally
        {
            if (_ownsTransaction)
            {
                await DisposeTransactionAsync();
            }
        }
    }

    public Task<int> ExecuteSqlCommandAsync(
        string sql,
        CancellationToken cancellationToken = default,
        params object[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return _dbContext.Database.ExecuteSqlRawAsync(
            sql,
            parameters,
            cancellationToken);
    }

    /// <summary>
    /// Adds a new entity to EF Core's change tracker; does not commit the database.
    /// 将新实体加入 EF Core ChangeTracker；不会在这里提交数据库。
    /// </summary>
    public async Task<bool> RegisterNew<TEntity>(
        TEntity entity)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);

        var now = DateTime.UtcNow;
        var userId = await _workContext.GetUserId();

        ApplyCreateAudit(entity, userId, now);

        await _dbContext.Set<TEntity>().AddAsync(entity);
        return true;
    }

    public async Task<bool> RegisterRangeNew<TEntity>(
        IEnumerable<TEntity> entities)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entities);

        var materialized = entities as TEntity[] ?? entities.ToArray();
        var now = DateTime.UtcNow;
        var userId = await _workContext.GetUserId();

        foreach (var entity in materialized)
        {
            ArgumentNullException.ThrowIfNull(entity);
            ApplyCreateAudit(entity, userId, now);
        }

        await _dbContext.Set<TEntity>().AddRangeAsync(materialized);
        return true;
    }

    /// <summary>
    /// Marks an entity as modified; the actual database write happens at Commit.
    /// 将实体标记为修改状态；真正的数据库写入发生在 Commit。
    /// </summary>
    public async Task<bool> RegisterDirty<TEntity>(
        TEntity entity)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);

        ApplyModifyAudit(
            entity,
            await _workContext.GetUserId(),
            DateTime.UtcNow);

        AttachAsModifiedIfDetached(entity);
        return true;
    }

    public async Task<bool> RegisterRangeDirty<TEntity>(
        IEnumerable<TEntity> entities)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entities);

        var materialized = entities as TEntity[] ?? entities.ToArray();
        var now = DateTime.UtcNow;
        var userId = await _workContext.GetUserId();

        foreach (var entity in materialized)
        {
            ArgumentNullException.ThrowIfNull(entity);
            ApplyModifyAudit(entity, userId, now);
        }

        foreach (var entity in materialized)
        {
            AttachAsModifiedIfDetached(entity);
        }

        return true;
    }

    /// <summary>
    /// Stages either physical deletion or soft deletion according to isDel.
    /// 根据 isDel 决定执行物理删除还是逻辑删除，并等待 Commit 才真正持久化。
    /// </summary>
    public async Task<bool> RegisterDeleted<TEntity>(
        TEntity entity,
        bool isDel = false)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (isDel)
        {
            _dbContext.Set<TEntity>().Remove(entity);
            return true;
        }

        var userId = await _workContext.GetUserId();
        var now = DateTime.UtcNow;

        ApplyModifyAudit(entity, userId, now);
        ApplySoftDelete(entity);

        AttachAsModifiedIfDetached(entity);
        return true;
    }

    public async Task<bool> RegisterRangeDeleted<TEntity>(
        IEnumerable<TEntity> entities,
        bool isDel = false)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entities);

        var materialized = entities as TEntity[] ?? entities.ToArray();

        if (isDel)
        {
            _dbContext.Set<TEntity>().RemoveRange(materialized);
            return true;
        }

        var userId = await _workContext.GetUserId();
        var now = DateTime.UtcNow;

        foreach (var entity in materialized)
        {
            ArgumentNullException.ThrowIfNull(entity);
            ApplyModifyAudit(entity, userId, now);
            ApplySoftDelete(entity);
        }

        foreach (var entity in materialized)
        {
            AttachAsModifiedIfDetached(entity);
        }

        return true;
    }

    /// <summary>
    /// Rolls back a transaction created by this UnitOfWork and clears tracked entity state.
    /// Caller-owned transactions must be rolled back by their owner.
    /// 回滚由当前 UnitOfWork 创建的事务；外部事务必须由其所有者回滚。
    /// </summary>
    public async Task RollbackAsync(
        CancellationToken cancellationToken = default)
    {
        if (DbContextTransaction is null)
        {
            _dbContext.ClearTrackedChanges();
            return;
        }

        if (!_ownsTransaction)
        {
            throw new InvalidOperationException(
                "This UnitOfWork does not own the active transaction. Roll it back through its owner.");
        }

        try
        {
            await DbContextTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            _dbContext.ClearTrackedChanges();
            await DisposeTransactionAsync();
        }
    }

    /// <summary>
    /// Synchronously rolls back a transaction created by this UnitOfWork.
    /// Caller-owned transactions must be rolled back by their owner.
    /// 同步回滚由当前 UnitOfWork 创建的事务；外部事务必须由其所有者回滚。
    /// </summary>
    public void Rollback()
    {
        if (DbContextTransaction is null)
        {
            _dbContext.ClearTrackedChanges();
            return;
        }

        if (!_ownsTransaction)
        {
            throw new InvalidOperationException(
                "This UnitOfWork does not own the active transaction. Roll it back through its owner.");
        }

        try
        {
            DbContextTransaction.Rollback();
        }
        finally
        {
            _dbContext.ClearTrackedChanges();

            if (DbContextTransaction is not null)
            {
                DbContextTransaction.Dispose();
            }

            DbContextTransaction = null;
            _ownsTransaction = false;
        }
    }


    /// <summary>
    /// Keeps EF Core's property-level change tracking for entities loaded by this DbContext.
    /// Detached entities are treated as complete replacements and marked modified.
    /// 对当前 DbContext 已跟踪实体保留属性级变更；游离实体按完整对象更新处理。
    /// </summary>
    private void AttachAsModifiedIfDetached<TEntity>(TEntity entity)
        where TEntity : class
    {
        if (_dbContext.Entry(entity).State == EntityState.Detached)
        {
            _dbContext.Set<TEntity>().Update(entity);
        }
    }

    private static void ApplyCreateAudit<TEntity>(
        TEntity entity,
        long userId,
        DateTime now)
        where TEntity : class
    {
        switch (entity)
        {
            case BasicEntity basicEntity:
                basicEntity.Creator = userId;
                basicEntity.GmtCreated = now;
                basicEntity.Modifier = userId;
                basicEntity.GmtModified = now;
                break;

            case BaseEntity baseEntity:
                baseEntity.Creator = userId;
                baseEntity.CreateTime = now;
                baseEntity.Updater = userId;
                baseEntity.UpdateTime = now;
                break;
        }
    }

    private static void ApplyModifyAudit<TEntity>(
        TEntity entity,
        long userId,
        DateTime now)
        where TEntity : class
    {
        switch (entity)
        {
            case BasicEntity basicEntity:
                basicEntity.Modifier = userId;
                basicEntity.GmtModified = now;
                break;

            case BaseEntity baseEntity:
                baseEntity.Updater = userId;
                baseEntity.UpdateTime = now;
                break;
        }
    }

    private static void ApplySoftDelete<TEntity>(TEntity entity)
        where TEntity : class
    {
        switch (entity)
        {
            case BasicEntity basicEntity:
                basicEntity.IsDeleted = true;
                break;

            case BaseEntity baseEntity:
                baseEntity.Deleted = true;
                break;
        }
    }

    private async Task DisposeTransactionAsync()
    {
        if (DbContextTransaction is null || !_ownsTransaction)
        {
            return;
        }

        await DbContextTransaction.DisposeAsync();
        DbContextTransaction = null;
        _ownsTransaction = false;
    }
}
