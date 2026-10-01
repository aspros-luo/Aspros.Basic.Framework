namespace Aspros.Base.Framework.Application.Behaviors;

/// <summary>
/// 应用层请求处理管道行为。
/// 用于在 Handler 执行前后插入横切逻辑，例如验证、事务、日志。
/// </summary>
public interface IPipelineBehavior<TRequest, TResponse>
{
    Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default);
}
