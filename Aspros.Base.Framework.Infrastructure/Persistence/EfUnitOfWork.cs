using Aspros.Base.Framework.Application.Abstractions;
using Aspros.Base.Framework.Application.Abstractions.Events;
using Aspros.Base.Framework.Application.Abstractions.Persistence;
using Aspros.Base.Framework.Domain;
using Aspros.Base.Framework.Domain.Kernel;
using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;

namespace Aspros.Base.Framework.Infrastructure.Persistence;

/// <summary>
/// 基于 EF Core 的轻量 Unit of Work。
/// Register* 只负责登记变更，CommitAsync 才持久化；显式事务通过 ITransactionalUnitOfWork。
/// </summary>
public sealed class EfUnitOfWork(
    IDbContext dbContext,
    IWorkContext workContext,
    ICapPublisher? capPublisher = null,
    IDomainEventDispatcher? domainEventDispatcher = null)
    : ITransactionalUnitOfWork, IScoped
{
    public Task<bool> RegisterNew<TEntity>(TEntity entity) where TEntity : class
        => RegisterNewInternalAsync(entity);

    public async Task<bool> RegisterRangeNew<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entities);
        var materialized = entities as TEntity[] ?? entities.ToArray();

        var userId = await workContext.GetUserId();
        var now = DateTime.Now;

        foreach (var entity in materialized)
        {
            if (entity is IAuditableEntity auditable)
            {
                auditable.CreatedBy = userId;
                auditable.CreatedAt = now;
                auditable.ModifiedBy = userId;
                auditable.ModifiedAt = now;
            }
        }

        await dbContext.Set<TEntity>().AddRangeAsync(materialized);
        return true;
    }

    public async Task<bool> RegisterDirty<TEntity>(TEntity entity) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);

        await StampModifiedAsync(entity);
        dbContext.Set<TEntity>().Update(entity);
        return true;
    }

    public async Task<bool> RegisterRangeDirty<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entities);
        var materialized = entities as TEntity[] ?? entities.ToArray();
        var userId = await workContext.GetUserId();

        foreach (var entity in materialized)
        {
            if (entity is IAuditableEntity auditable)
            {
                auditable.ModifiedBy = userId;
                auditable.ModifiedAt = DateTime.Now;
            }
        }

        dbContext.Set<TEntity>().UpdateRange(materialized);
        return true;
    }

    public async Task<bool> RegisterDeleted<TEntity>(TEntity entity, bool isDel = false) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (isDel)
        {
            dbContext.Set<TEntity>().Remove(entity);
            return true;
        }

        var userId = await workContext.GetUserId();
        if (entity is IAuditableEntity auditable)
        {
            auditable.ModifiedBy = userId;
            auditable.ModifiedAt = DateTime.Now;
            auditable.IsDeleted = true;
        }

        dbContext.Set<TEntity>().Update(entity);
        return true;
    }

    public async Task<bool> RegisterRangeDeleted<TEntity>(
        IEnumerable<TEntity> entities,
        bool isDel = false) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entities);
        var materialized = entities as TEntity[] ?? entities.ToArray();

        if (isDel)
        {
            dbContext.Set<TEntity>().RemoveRange(materialized);
            return true;
        }

        var userId = await workContext.GetUserId();
        foreach (var entity in materialized)
        {
            if (entity is IAuditableEntity auditable)
            {
                auditable.ModifiedBy = userId;
                auditable.ModifiedAt = DateTime.Now;
                auditable.IsDeleted = true;
            }
        }

        dbContext.Set<TEntity>().UpdateRange(materialized);
        return true;
    }

    public Task<int> CommitAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var transaction = await BeginTransactionAsync(cancellationToken);

        try
        {
            await operation(cancellationToken);
            await SaveChangesAndDispatchDomainEventsAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var transaction = await BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await operation(cancellationToken);
            await SaveChangesAndDispatchDomainEventsAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<bool> RegisterNewInternalAsync<TEntity>(TEntity entity) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);

        await StampCreatedAsync(entity);
        await dbContext.Set<TEntity>().AddAsync(entity);
        return true;
    }

    private async Task StampCreatedAsync<TEntity>(TEntity entity) where TEntity : class
    {
        if (entity is not IAuditableEntity auditable)
        {
            return;
        }

        var userId = await workContext.GetUserId();
        var now = DateTime.Now;
        auditable.CreatedBy = userId;
        auditable.CreatedAt = now;
        auditable.ModifiedBy = userId;
        auditable.ModifiedAt = now;
    }

    private async Task StampModifiedAsync<TEntity>(TEntity entity) where TEntity : class
    {
        if (entity is not IAuditableEntity auditable)
        {
            return;
        }

        auditable.ModifiedBy = await workContext.GetUserId();
        auditable.ModifiedAt = DateTime.Now;
    }

    private async Task SaveChangesAndDispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);

        while (true)
        {
            var aggregateRoots = dbContext.ChangeTracker
                .Entries()
                .Select(entry => entry.Entity)
                .OfType<IAggregateRoot>()
                .Distinct()
                .ToArray();

            var domainEvents = aggregateRoots
                .SelectMany(aggregate => aggregate.DomainEvents)
                .ToArray();

            if (domainEvents.Length == 0)
            {
                return;
            }

            if (domainEventDispatcher is null)
            {
                throw new InvalidOperationException(
                    "Domain events were raised, but no IDomainEventDispatcher is registered.");
            }

            await domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var aggregateRoot in aggregateRoots)
            {
                aggregateRoot.ClearDomainEvents(domainEvents);
            }
        }
    }

    private Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "An explicit transaction is already active for this DbContext. " +
                "ExecuteInTransactionAsync does not support nested transaction scopes; " +
                "compose the work inside the existing transaction instead.");
        }

        if (capPublisher is not null)
        {
            return dbContext.Database.BeginTransactionAsync(
                capPublisher,
                autoCommit: false,
                cancellationToken: cancellationToken);
        }

        return dbContext.Database.BeginTransactionAsync(cancellationToken);
    }
}
