using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Coordinates persistence operations and optional database transactions.
/// 协调持久化操作以及“按需开启”的数据库事务。
///
/// <para>
/// A Unit of Work is not required for every business operation.
/// 简单的单表/单次写操作不需要为了形式而显式开启事务。
/// Use an explicit transaction when several persistence operations must succeed
/// or fail as one atomic unit.
/// 当多个数据库操作必须“要么全部成功、要么全部失败”时，再显式开启事务。
/// </para>
/// </summary>
public interface IUnitOfWork : IScoped
{
    /// <summary>
    /// The current framework database context.
/// 当前 Framework 使用的数据库上下文。
/// </summary>
    IDbContext DbContext { get; }

    /// <summary>
    /// EF Core database facade for database-level operations.
/// EF Core 的数据库级操作入口。
/// </summary>
    DatabaseFacade Database { get; }

    /// <summary>
    /// The current database connection.
/// 当前数据库连接。
/// </summary>
    IDbConnection Connection { get; }

    /// <summary>
    /// The currently active EF Core transaction, if any.
/// 当前正在使用的 EF Core 事务；没有显式事务时为 null。
/// </summary>
    IDbContextTransaction? DbContextTransaction { get; }

    /// <summary>
    /// Starts an explicit transaction. Transactions remain opt-in.
/// 显式开启事务。事务仍然采用“按需开启”的策略。
/// </summary>
    IDbContextTransaction BeginTransaction(
        IDbContextTransaction? dbContextTransaction = null);

    /// <summary>
    /// Asynchronously starts an explicit transaction.
/// 异步开启显式事务。
/// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes raw SQL through the current database connection.
/// 通过当前数据库连接执行原生 SQL。
///
/// <para>
/// Prefer repositories/EF for normal business persistence.
/// 普通业务持久化优先使用 Repository/EF；
/// 这里主要用于确实需要手写 SQL 的场景。
/// </para>
/// </summary>
    Task<int> ExecuteSqlCommandAsync(
        string sql,
        CancellationToken cancellationToken = default,
        params object[] parameters);

    /// <summary>
    /// Stages a new entity for persistence.
/// 将新实体加入当前工作单元，等待 Commit 时统一持久化。
/// </summary>
    Task<bool> RegisterNew<TEntity>(TEntity entity)
        where TEntity : class;

    /// <summary>
    /// Stages multiple new entities for persistence.
/// 将多个新实体加入当前工作单元，等待 Commit 时统一持久化。
/// </summary>
    Task<bool> RegisterRangeNew<TEntity>(IEnumerable<TEntity> entities)
        where TEntity : class;

    /// <summary>
    /// Marks an entity as modified without immediately committing it.
/// 标记实体为已修改，但不会立即提交数据库。
/// </summary>
    Task<bool> RegisterDirty<TEntity>(TEntity entity)
        where TEntity : class;

    /// <summary>
    /// Marks multiple entities as modified without immediately committing them.
/// 批量标记实体为已修改，但不会立即提交数据库。
/// </summary>
    Task<bool> RegisterRangeDirty<TEntity>(IEnumerable<TEntity> entities)
        where TEntity : class;

    /// <summary>
    /// Stages an entity for deletion or logical deletion.
/// 将实体加入删除或逻辑删除流程。
/// </summary>
    Task<bool> RegisterDeleted<TEntity>(TEntity entity, bool isDel = false)
        where TEntity : class;

    /// <summary>
    /// Stages multiple entities for deletion or logical deletion.
/// 批量将实体加入删除或逻辑删除流程。
/// </summary>
    Task<bool> RegisterRangeDeleted<TEntity>(
        IEnumerable<TEntity> entities,
        bool isDel = false)
        where TEntity : class;

    /// <summary>
    /// Persists staged changes and commits the explicit transaction, when one exists.
/// 持久化当前暂存的修改；如果存在显式事务，则同时提交事务。
/// </summary>
    Task<bool> CommitAsync();

    /// <summary>
    /// Persists staged changes and commits the explicit transaction with cancellation support.
/// 持久化当前暂存的修改，并支持 CancellationToken。
/// </summary>
    Task<bool> CommitAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Rolls back the explicit transaction asynchronously.
/// 异步回滚显式事务。
///
/// <para>
/// The framework also clears EF Core tracked changes after rollback,
/// preventing a later SaveChanges from accidentally persisting reverted state.
/// 回滚后 Framework 会清理 EF Core 的 ChangeTracker，
/// 防止后续误调用 SaveChanges 又把已经回滚的实体状态保存进去。
/// </para>
/// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the explicit transaction synchronously.
/// 同步回滚显式事务。
/// </summary>
    void Rollback();
}
