using MediatR;

namespace Aspros.Base.Framework.Application.Abstractions;

/// <summary>
/// 查询模型基础接口。
/// Query 只读取数据，不修改领域状态。
/// </summary>
public interface IQuery<TResult> : IRequest<TResult>
{
}

/// <summary>
/// Query Handler 基础接口。
/// 业务代码使用 HandleAsync，MediatR 负责实际请求调度。
/// </summary>
public interface IQueryHandler<TQuery, TResult> : IRequestHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(
        TQuery query,
        CancellationToken cancellationToken = default);

    Task<TResult> IRequestHandler<TQuery, TResult>.Handle(
        TQuery request,
        CancellationToken cancellationToken)
        => HandleAsync(request, cancellationToken);
}
