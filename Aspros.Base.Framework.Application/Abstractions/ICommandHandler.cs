using MediatR;

namespace Aspros.Base.Framework.Application.Abstractions;

/// <summary>
/// Command Handler 基础接口。
/// 业务代码继续使用 HandleAsync，MediatR 负责实际请求调度。
/// </summary>
public interface ICommandHandler<TCommand> : IRequestHandler<TCommand>
    where TCommand : ICommand
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);

    async Task<Unit> IRequestHandler<TCommand>.Handle(
        TCommand request,
        CancellationToken cancellationToken)
    {
        await HandleAsync(request, cancellationToken);
        return Unit.Value;
    }
}

/// <summary>
/// 带返回值的 Command Handler。
/// </summary>
public interface ICommandHandler<TCommand, TResult> : IRequestHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(
        TCommand command,
        CancellationToken cancellationToken = default);

    Task<TResult> IRequestHandler<TCommand, TResult>.Handle(
        TCommand request,
        CancellationToken cancellationToken)
        => HandleAsync(request, cancellationToken);
}
