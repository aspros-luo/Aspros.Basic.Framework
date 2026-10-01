namespace Aspros.Base.Framework.Application.Abstractions.Rpc;

/// <summary>
/// 应用层 RPC 调用抽象。
/// Application 只依赖调用契约，不依赖具体 RPC 框架。
/// </summary>
public interface IRpcClient
{
    Task<TResponse> CallAsync<TRequest, TResponse>(
        string service,
        string method,
        TRequest request,
        CancellationToken cancellationToken = default);
}
