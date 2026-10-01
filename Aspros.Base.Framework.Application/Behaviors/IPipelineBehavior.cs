namespace Aspros.Base.Framework.Application.Behaviors;

/// <summary>
/// 应用层请求处理管道行为。
/// Pipeline 负责 Command/Query 用例级横切能力的组合，不限定具体实现。
/// </summary>
public interface IPipelineBehavior<TRequest, TResponse>
{
    Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default);
}
