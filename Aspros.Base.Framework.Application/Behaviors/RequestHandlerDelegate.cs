namespace Aspros.Base.Framework.Application.Behaviors;

/// <summary>
/// 下一个管道节点的委托。
/// </summary>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken = default);
