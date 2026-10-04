using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Aspros.Base.Framework.Infrastructure
{
    public interface IUnitOfWork : IScoped
    {
        IDbContext DbContext { get; }
        DatabaseFacade Database { get; }
        IDbConnection Connection { get; }
        //IDbTransaction Transaction { get; }
        IDbContextTransaction BeginTransaction(IDbContextTransaction? dbContextTransaction = null);
        IDbContextTransaction DbContextTransaction { get; set; }

        /// <summary>
        /// 执行sql
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        Task<int> ExecuteSqlCommandAsync(string sql, CancellationToken cancellationToken = default, params object[] parameters);
        /// <summary>
        /// 新增
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task<bool> RegisterNew<TEntity>(TEntity entity) where TEntity : class;
        /// <summary>
        /// 批量新增
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task<bool> RegisterRangeNew<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;
        /// <summary>
        /// 修改
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task<bool> RegisterDirty<TEntity>(TEntity entity) where TEntity : class;
        /// <summary>
        /// 批量修改
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task<bool> RegisterRangeDirty<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;
        /// <summary>
        /// 删除
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="entity"></param>
        /// <param name="isDel">是否物理删除 默认否</param>
        /// <returns></returns>
        Task<bool> RegisterDeleted<TEntity>(TEntity entity, bool isDel = false) where TEntity : class;
        /// <summary>
        /// 批量删除
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="entities"></param>
        /// <param name="isDel">是否物理删除 默认否</param>       
        /// <returns></returns>
        Task<bool> RegisterRangeDeleted<TEntity>(IEnumerable<TEntity> entities, bool isDel = false) where TEntity : class;
        /// <summary>
        /// 提交 save change
        /// </summary>
        /// <returns></returns>
        Task<bool> CommitAsync();
        void Rollback();
    }
}
