using Aspros.Base.Framework.Application.Abstractions.Persistence;
using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;

namespace Aspros.Base.Framework.Infrastructure.Persistence;

/// <summary>
/// 基于 EF Core DbContext 的 Unit of Work 实现。
/// Framework 不提供具体 DbContext，业务项目负责注册和配置自己的 DbContext。
/// </summary>
public sealed class EfUnitOfWork(
    DbContext dbContext,
    ICapPublisher? capPublisher = null) : IUnitOfWork
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
            await dbContext.SaveChangesAsync(cancellationToken);
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
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken)
    {
        if (capPublisher is not null)
        {
            return dbContext.Database.BeginTransactionAsync(
                capPublisher,
                autoCommit: false,
                cancellationToken);
        }

        return dbContext.Database.BeginTransactionAsync(cancellationToken);
    }
}
