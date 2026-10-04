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
    /// Starts an explicit transaction. A transaction is optional; callers can
    /// simply stage changes and call CommitAsync when only one atomic SaveChanges
    /// is required.
    /// </summary>
    IDbContextTransaction BeginTransaction(IDbContextTransaction? dbContextTransaction = null);

    Task<int> ExecuteSqlCommandAsync(
        string sql,
        CancellationToken cancellationToken = default,
        params object[] parameters);

    Task<bool> RegisterNew<TEntity>(TEntity entity) where TEntity : class;

    Task<bool> RegisterRangeNew<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;

    Task<bool> RegisterDirty<TEntity>(TEntity entity) where TEntity : class;

    Task<bool> RegisterRangeDirty<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;

    Task<bool> RegisterDeleted<TEntity>(TEntity entity, bool isDel = false) where TEntity : class;

    Task<bool> RegisterRangeDeleted<TEntity>(IEnumerable<TEntity> entities, bool isDel = false)
        where TEntity : class;

    /// <summary>
    /// Persists all staged changes and, when an explicit transaction is active,
    /// commits it. The unit of work owns neither the DbConnection nor DbContext
    /// and therefore never disposes either one here.
    /// </summary>
    Task<bool> CommitAsync();

    void Rollback();
}
