using Aspros.Base.Framework.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Aspros.Base.Framework.Infrastructure;

public sealed class UnitOfWork(
    IDbContext dbContext,
    IWorkContext workContext) : IUnitOfWork
{
    private readonly IDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private readonly IWorkContext _workContext =
        workContext ?? throw new ArgumentNullException(nameof(workContext));

    private bool _ownsTransaction;

    public IDbContext DbContext => _dbContext;

    public DatabaseFacade Database => _dbContext.Database;

    public IDbConnection Connection => _dbContext.Database.GetDbConnection();

    public IDbContextTransaction? DbContextTransaction { get; private set; }

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

    public Task<bool> CommitAsync() => CommitAsync(CancellationToken.None);

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

    public async Task<bool> RegisterNew<TEntity>(TEntity entity)
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

    public async Task<bool> RegisterDirty<TEntity>(TEntity entity)
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

    public void Rollback()
    {
        if (DbContextTransaction is null)
        {
            return;
        }

        try
        {
            DbContextTransaction.Rollback();
        }
        finally
        {
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
