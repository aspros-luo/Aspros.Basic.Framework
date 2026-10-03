using Aspros.Base.Framework.Application.Abstractions.Events;
using Aspros.Base.Framework.Application.Abstractions.Persistence;
using Aspros.Base.Framework.Domain.Kernel;
using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;

namespace Aspros.Base.Framework.Infrastructure.Persistence;

/// <summary>
/// 基于 EF Core DbContext 的 Unit of Work 实现。
/// Framework 不提供具体 DbContext，业务项目负责注册和配置自己的 DbContext。
/// </summary>
public sealed class EfUnitOfWork(
    IDbContext dbContext,
    ICapPublisher? capPublisher = null,
    IDomainEventDispatcher? domainEventDispatcher = null) : IUnitOfWork, IScoped
{
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

            // Handler changes are persisted before the original event snapshot is cleared.
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
