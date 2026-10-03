using Aspros.Base.Framework.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Aspros.Base.Framework.Infrastructure
{
    /// <summary>
    /// Legacy EF Core Unit of Work.
    /// Register* 只负责变更跟踪，CommitAsync 才执行 SaveChanges；事务仅在业务明确需要时开启。
    /// </summary>
    public class UnitOfWork(IDbContext dbContext, IWorkContext workContext) : IUnitOfWork
    {
        private readonly IDbContext _dbContext = dbContext;
        private readonly IWorkContext _workContext = workContext;

        public IDbContext DbContext => _dbContext;

        public DatabaseFacade Database => _dbContext.Database;

        public IDbConnection Connection => _dbContext.Database.GetDbConnection();

        public IDbContextTransaction? DbContextTransaction { get; set; }

        public IDbContextTransaction BeginTransaction(IDbContextTransaction? dbContextTransaction = null)
        {
            if (dbContextTransaction != null)
            {
                if (_dbContext.Database.CurrentTransaction is not null &&
                    !ReferenceEquals(_dbContext.Database.CurrentTransaction, dbContextTransaction))
                {
                    throw new InvalidOperationException("A different transaction is already active for this DbContext.");
                }

                return DbContextTransaction = dbContextTransaction;
            }

            if (_dbContext.Database.CurrentTransaction is not null)
            {
                throw new InvalidOperationException(
                    "An explicit transaction is already active for this DbContext. " +
                    "Nested transaction scopes are not supported.");
            }

            return DbContextTransaction = Database.BeginTransaction();
        }

        public async Task<bool> CommitAsync(CancellationToken cancellationToken = default)
        {
            var affected = await _dbContext.SaveChangesAsync(cancellationToken);

            if (DbContextTransaction is null)
            {
                return affected > 0;
            }

            try
            {
                await DbContextTransaction.CommitAsync(cancellationToken);
                return true;
            }
            finally
            {
                await DbContextTransaction.DisposeAsync();
                DbContextTransaction = null;
            }
        }

        public Task<int> ExecuteSqlCommandAsync(
            string sql,
            CancellationToken cancellationToken = default,
            params object[] parameters)
            => _dbContext.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);

        public async Task<bool> RegisterDeleted<TEntity>(TEntity entity, bool isDel = false)
            where TEntity : class
        {
            ArgumentNullException.ThrowIfNull(entity);

            if (isDel)
            {
                _dbContext.Set<TEntity>().Remove(entity);
                return true;
            }

            var userId = await _workContext.GetUserId();
            if (entity is IAuditableEntity auditable)
            {
                auditable.ModifiedBy = userId;
                auditable.ModifiedAt = DateTime.Now;
                auditable.IsDeleted = true;
            }

            _dbContext.Set<TEntity>().Update(entity);
            return true;
        }

        public async Task<bool> RegisterDirty<TEntity>(TEntity entity) where TEntity : class
        {
            ArgumentNullException.ThrowIfNull(entity);

            await StampModifiedAsync(entity);
            _dbContext.Set<TEntity>().Update(entity);
            return true;
        }

        public async Task<bool> RegisterNew<TEntity>(TEntity entity) where TEntity : class
        {
            ArgumentNullException.ThrowIfNull(entity);

            await StampCreatedAsync(entity);
            await _dbContext.Set<TEntity>().AddAsync(entity);
            return true;
        }

        public async Task<bool> RegisterRangeDeleted<TEntity>(IEnumerable<TEntity> entities, bool isDel = false)
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
            foreach (var entity in materialized)
            {
                if (entity is IAuditableEntity auditable)
                {
                    auditable.ModifiedBy = userId;
                    auditable.ModifiedAt = DateTime.Now;
                    auditable.IsDeleted = true;
                }
            }

            _dbContext.Set<TEntity>().UpdateRange(materialized);
            return true;
        }

        public async Task<bool> RegisterRangeDirty<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
            ArgumentNullException.ThrowIfNull(entities);
            var materialized = entities as TEntity[] ?? entities.ToArray();
            var userId = await _workContext.GetUserId();

            foreach (var entity in materialized)
            {
                if (entity is IAuditableEntity auditable)
                {
                    auditable.ModifiedBy = userId;
                    auditable.ModifiedAt = DateTime.Now;
                }
            }

            _dbContext.Set<TEntity>().UpdateRange(materialized);
            return true;
        }

        public async Task<bool> RegisterRangeNew<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
            ArgumentNullException.ThrowIfNull(entities);
            var materialized = entities as TEntity[] ?? entities.ToArray();
            var userId = await _workContext.GetUserId();
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

            await _dbContext.Set<TEntity>().AddRangeAsync(materialized);
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
                DbContextTransaction.Dispose();
                DbContextTransaction = null;
            }
        }

        private async Task StampCreatedAsync<TEntity>(TEntity entity) where TEntity : class
        {
            if (entity is not IAuditableEntity auditable)
            {
                return;
            }

            var userId = await _workContext.GetUserId();
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

            auditable.ModifiedBy = await _workContext.GetUserId();
            auditable.ModifiedAt = DateTime.Now;
        }
    }
}
