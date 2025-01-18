using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Aspros.Base.Framework.Infrastructure
{
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
            if (Connection.State == ConnectionState.Closed)
                Connection.Open();
            if (dbContextTransaction != null)
                return DbContextTransaction = dbContextTransaction;
            return DbContextTransaction = Database.BeginTransaction();
        }

        public async Task<bool> CommitAsync()
        {
            if (Connection.State == ConnectionState.Closed)
                Connection.Open();
            if (DbContextTransaction == null)
            {
                var result = await _dbContext.SaveChangesAsync() > 0;
                Connection.Close();
                Connection.Dispose();
                return result;
            }
            DbContextTransaction.Commit();
            DbContextTransaction = null;
            Connection.Close();
            Connection.Dispose();
            return true;
        }

        public async Task<int> ExecuteSqlCommandAsync(string sql, CancellationToken cancellationToken = default, params object[] parameters)
        {
            return await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken, parameters);
        }

        public async Task<bool> RegisterDeleted<TEntity>(TEntity entity, bool isDel = false) where TEntity : class
        {
            if (isDel)
                _dbContext.Set<TEntity>().Remove(entity);
            else
            {
                var userId = await _workContext.GetUserId();
                _dbContext.Entry(entity).Entity.GetType().GetProperty("Modifier")?.SetValue(entity, userId);
                _dbContext.Entry(entity).Entity.GetType().GetProperty("GmtModified")?.SetValue(entity, DateTime.Now);
                _dbContext.Entry(entity).Entity.GetType().GetProperty("IsDeleted")?.SetValue(entity, true);
                _dbContext.Set<TEntity>().Update(entity);
            }
            if (DbContextTransaction != null)
                return await _dbContext.SaveChangesAsync() > 0;
            return true;
        }

        public async Task<bool> RegisterDirty<TEntity>(TEntity entity) where TEntity : class
        {
            var userId = await _workContext.GetUserId();
            _dbContext.Entry(entity).Entity.GetType().GetProperty("Modifier")?.SetValue(entity, userId);
            _dbContext.Entry(entity).Entity.GetType().GetProperty("GmtModified")?.SetValue(entity, DateTime.Now);
            _dbContext.Set<TEntity>().Update(entity);
            if (DbContextTransaction != null)
                return await _dbContext.SaveChangesAsync() > 0;
            return true;
        }

        public async Task<bool> RegisterNew<TEntity>(TEntity entity) where TEntity : class
        {
            var userId = await _workContext.GetUserId();
            _dbContext.Entry(entity).Entity.GetType().GetProperty("Creator")?.SetValue(entity, userId);
            _dbContext.Entry(entity).Entity.GetType().GetProperty("GmtCreated")?.SetValue(entity, DateTime.Now);
            _dbContext.Entry(entity).Entity.GetType().GetProperty("Modifier")?.SetValue(entity, userId);
            _dbContext.Entry(entity).Entity.GetType().GetProperty("GmtModified")?.SetValue(entity, DateTime.Now);
            //if (_dbContext.Entry(entity).Property("Creator").CurrentValue!=null)
            //    _dbContext.Entry(entity).Property("Creator").CurrentValue = userId;
            //if (_dbContext.Entry(entity).Property("GmtCreated")?.CurrentValue!=null)
            //    _dbContext.Entry(entity).Property("GmtCreated").CurrentValue = DateTime.Now;
            //if (_dbContext.Entry(entity).Property("Modifier").CurrentValue != null)
            //    _dbContext.Entry(entity).Property("Modifier").CurrentValue = userId;
            //if (_dbContext.Entry(entity).Property("GmtModified").CurrentValue != null)
            //    _dbContext.Entry(entity).Property("GmtModified").CurrentValue = DateTime.Now;
            await _dbContext.Set<TEntity>().AddAsync(entity);
            if (DbContextTransaction != null)
                return await _dbContext.SaveChangesAsync() > 0;
            return true;
        }

        public async Task<bool> RegisterRangeDeleted<TEntity>(IEnumerable<TEntity> entities, bool isDel = false) where TEntity : class
        {
            if (isDel)
                _dbContext.Set<TEntity>().RemoveRange(entities);
            else
                _dbContext.Set<TEntity>().UpdateRange(entities);
            if (DbContextTransaction != null)
                return await _dbContext.SaveChangesAsync() > 0;
            return true;
        }

        public async Task<bool> RegisterRangeDirty<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
            var userId = await _workContext.GetUserId();
            foreach (var entity in entities)
            {
                _dbContext.Entry(entity).Entity.GetType().GetProperty("Modifier")?.SetValue(entity, userId);
                _dbContext.Entry(entity).Entity.GetType().GetProperty("GmtModified")?.SetValue(entity, DateTime.Now);
            }
            _dbContext.Set<TEntity>().UpdateRange(entities);
            if (DbContextTransaction != null)
                return await _dbContext.SaveChangesAsync() > 0;
            return true;
        }

        public async Task<bool> RegisterRangeNew<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
            var userId = await _workContext.GetUserId();
            foreach (var entity in entities)
            {
                _dbContext.Entry(entity).Entity.GetType().GetProperty("Creator")?.SetValue(entity, userId);
                _dbContext.Entry(entity).Entity.GetType().GetProperty("GmtCreated")?.SetValue(entity, DateTime.Now);
                _dbContext.Entry(entity).Entity.GetType().GetProperty("Modifier")?.SetValue(entity, userId);
                _dbContext.Entry(entity).Entity.GetType().GetProperty("GmtModified")?.SetValue(entity, DateTime.Now);
            }
            await _dbContext.Set<TEntity>().AddRangeAsync(entities);
            if (DbContextTransaction != null)
                return await _dbContext.SaveChangesAsync() > 0;
            return true;
        }

        public void Rollback()
        {
            DbContextTransaction?.Rollback();
        }
    }
}
