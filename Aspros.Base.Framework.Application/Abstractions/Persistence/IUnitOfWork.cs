namespace Aspros.Base.Framework.Application.Abstractions.Persistence;

/// <summary>
/// 应用层工作单元抽象。
/// 用于提交一次应用用例产生的持久化变更，并在需要时显式包裹多个持久化操作。
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// 提交当前工作单元中的持久化变更。
    /// 单次 SaveChanges 本身由 EF Core 负责原子性。
    /// </summary>
    Task<int> CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 在一个数据库事务中执行多个持久化操作。
    /// 具体事务实现由 Infrastructure 决定。
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在一个数据库事务中执行多个持久化操作并返回结果。
    /// 具体事务实现由 Infrastructure 决定。
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
