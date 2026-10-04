using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Aspros.Base.Framework.Infrastructure;

public interface IUnitOfWork : IScoped
{
    IDbContext DbContext { get; }
    DatabaseFacade Database { get; }
    IDbConnection Connection { get; }
    IDbContextTransaction? DbContextTransaction { get; }

    /// <summary>
    /// Starts an explicit transaction. Transactions remain opt-in.
    /// </summary>
    IDbContextTransaction BeginTransaction(
        IDbContextTransaction? dbContextTransaction = null);

    Task<IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default);

    Task<int> ExecuteSqlCommandAsync(
        string sql,
        CancellationToken cancellationToken = default,
        params object[] parameters);

    Task<bool> RegisterNew<TEntity>(TEntity entity)
        where TEntity : class;

    Task<bool> RegisterRangeNew<TEntity>(IEnumerable<TEntity> entities)
        where TEntity : class;

    Task<bool> RegisterDirty<TEntity>(TEntity entity)
        where TEntity : class;

    Task<bool> RegisterRangeDirty<TEntity>(IEnumerable<TEntity> entities)
        where TEntity : class;

    Task<bool> RegisterDeleted<TEntity>(TEntity entity, bool isDel = false)
        where TEntity : class;

    Task<bool> RegisterRangeDeleted<TEntity>(
        IEnumerable<TEntity> entities,
        bool isDel = false)
        where TEntity : class;

    /// <summary>
    /// Persists staged changes and commits the explicit transaction, when one exists.
    /// </summary>
    Task<bool> CommitAsync();

    Task<bool> CommitAsync(CancellationToken cancellationToken);

    void Rollback();
}
