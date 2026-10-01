using MediatR;

namespace Aspros.Base.Framework.Application.Behaviors;

/// <summary>
/// 应用层请求处理管道行为。
/// 直接接入 MediatR Pipeline，避免框架维护第二套请求执行器。
/// </summary>
public interface IPipelineBehavior<TRequest, TResponse>
    : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
}
