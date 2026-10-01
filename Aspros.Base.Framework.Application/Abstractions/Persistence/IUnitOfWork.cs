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
    /// 此方法不会自动分发领域事件；需要领域事件和 Outbox 协作时，应使用 ExecuteInTransactionAsync。
    /// </summary>
    Task<int> CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 在一个数据库事务中执行多个持久化操作。
    /// 事务成功时会自动保存变更，并在事务内分发领域事件。
    /// 当 Infrastructure 启用 CAP 事务集成时，领域事件处理器产生的 Integration Event Outbox 记录也参与同一事务。
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在一个数据库事务中执行多个持久化操作并返回结果。
    /// 事务成功时会自动保存变更，并在事务内分发领域事件。
    /// 当 Infrastructure 启用 CAP 事务集成时，领域事件处理器产生的 Integration Event Outbox 记录也参与同一事务。
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
