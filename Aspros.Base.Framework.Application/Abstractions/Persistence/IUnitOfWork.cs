namespace Aspros.Base.Framework.Application.Abstractions.Persistence;

/// <summary>
/// 应用层持久化提交抽象。
/// 正常业务只需要 CommitAsync；显式事务仅用于确实要求本地多步原子性的少数用例。
/// </summary>
public interface IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
