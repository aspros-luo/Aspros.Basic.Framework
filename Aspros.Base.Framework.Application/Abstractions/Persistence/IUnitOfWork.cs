namespace Aspros.Base.Framework.Application.Abstractions.Persistence;

/// <summary>
/// 应用层工作单元抽象。
/// 用于提交一次应用用例产生的持久化变更。
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// 提交当前工作单元中的持久化变更。
    /// </summary>
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
