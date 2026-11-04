namespace Aspros.Base.Framework.Domain.UoW;

/// <summary>
/// 工作单元契约：严格遵循“Register 只暂存变更、Commit 才持久化”的原则。
/// 默认不强制嵌套事务，支持显式开启事务与回滚安全清理。
/// </summary>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 当前是否处于显式物理事务中
    /// </summary>
    bool HasActiveTransaction { get; }

    /// <summary>
    /// 注册新增实体 (暂存至追踪集合)
    /// </summary>
    Task RegisterNewAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default) where TEntity : class;

    /// <summary>
    /// 注册修改实体 (标记脏状态)
    /// </summary>
    Task RegisterDirtyAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default) where TEntity : class;

    /// <summary>
    /// 注册删除实体
    /// </summary>
    Task RegisterCleanAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default) where TEntity : class;

    /// <summary>
    /// 显式开启物理数据库事务
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 原子提交所有已暂存变更，并在开启事务时提交物理事务
    /// </summary>
    Task<int> CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 回滚事务并清空暂存变更跟踪器
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
