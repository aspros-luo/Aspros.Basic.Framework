using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Aspros.Base.Framework.Infrastructure
{
    /// <summary>
    /// Legacy compatibility Unit of Work contract.
    /// Normal commands should only Register* changes and call CommitAsync once.
    /// BeginTransaction is an explicit opt-in for operations that genuinely require local multi-step atomicity.
    /// </summary>
    public interface IUnitOfWork : IScoped
    {
        IDbContext DbContext { get; }
        DatabaseFacade Database { get; }
        IDbConnection Connection { get; }
        IDbContextTransaction BeginTransaction(IDbContextTransaction? dbContextTransaction = null);
        IDbContextTransaction DbContextTransaction { get; set; }

        Task<int> ExecuteSqlCommandAsync(string sql, CancellationToken cancellationToken = default, params object[] parameters);
        Task<bool> RegisterNew<TEntity>(TEntity entity) where TEntity : class;
        Task<bool> RegisterRangeNew<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;
        Task<bool> RegisterDirty<TEntity>(TEntity entity) where TEntity : class;
        Task<bool> RegisterRangeDirty<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;
        Task<bool> RegisterDeleted<TEntity>(TEntity entity, bool isDel = false) where TEntity : class;
        Task<bool> RegisterRangeDeleted<TEntity>(IEnumerable<TEntity> entities, bool isDel = false) where TEntity : class;
        Task<bool> CommitAsync();
        void Rollback();
    }
}
