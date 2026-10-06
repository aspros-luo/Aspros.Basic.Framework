using Aspros.Base.Framework.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Default EF Core based Unit of Work implementation.
Coordinates tracked changes and an optional explicit transaction.
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
    /// Passing an existing transaction means this Unit of Work does not own its lifetime.
    /// 传入已有事务表示该事务的生命周期由外部调用方负责。
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
        }
        else
        {
            DbContextTransaction = dbContextTransaction;
            _ownsTransaction = false;
        }

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
    /// Saves EF Core changes and commits the current explicit transaction when present.
    /// 持久化 EF Core 修改；存在显式事务时，同时提交当前事务。
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
        if (DbContextTransaction is null)
        {
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await DbContextTransaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            try
            {
                await DbContextTransaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // Preserve the original persistence exception.
            }

            _dbContext.ClearTrackedChanges();
            throw;
        }
        finally
        {
            await DisposeTransactionAsync();
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
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);

        ApplyModifyAudit(
            entity,
            await _workContext.GetUserId(),
            DateTime.UtcNow);

        _dbContext.Set<TEntity>().Update(entity);
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

        _dbContext.Set<TEntity>().UpdateRange(materialized);
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

        _dbContext.Set<TEntity>().Update(entity);
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

        _dbContext.Set<TEntity>().UpdateRange(materialized);
        return true;
    }

    /// <summary>
    /// Rolls back the current transaction and clears tracked entity state.
    /// 回滚当前事务，并清理 EF Core 已跟踪的实体状态。
    /// </summary>
    public async Task RollbackAsync(
        CancellationToken cancellationToken = default)
    {
        if (DbContextTransaction is null)
        {
            _dbContext.ClearTrackedChanges();
            return;
        }

        try
        {
            await DbContextTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            _dbContext.ClearTrackedChanges();

            if (_ownsTransaction)
            {
                await DbContextTransaction.DisposeAsync();
            }

            DbContextTransaction = null;
            _ownsTransaction = false;
        }
    }

    /// <summary>
    /// Synchronously rolls back the current transaction and clears tracked entity state.
    /// 同步回滚当前事务，并清理 EF Core 已跟踪的实体状态。
    /// </summary>
    public void Rollback()
    {
        if (DbContextTransaction is null)
        {
            _dbContext.ClearTrackedChanges();
            return;
        }

        try
        {
            DbContextTransaction.Rollback();
        }
        finally
        {
            _dbContext.ClearTrackedChanges();

            if (_ownsTransaction)
            {
                DbContextTransaction.Dispose();
            }

            DbContextTransaction = null;
            _ownsTransaction = false;
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
        if (DbContextTransaction is null)
        {
            return;
        }

        if (_ownsTransaction)
        {
            await DbContextTransaction.DisposeAsync();
        }

        DbContextTransaction = null;
        _ownsTransaction = false;
    }
}
