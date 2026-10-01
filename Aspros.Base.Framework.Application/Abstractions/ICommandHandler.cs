namespace Aspros.Base.Framework.Application.Abstractions;

/// <summary>
/// Command Handler 基础接口。
/// 负责执行应用层业务流程。
/// </summary>
public interface ICommandHandler<TCommand>
    where TCommand : ICommand
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

public interface ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(
        TCommand command,
        CancellationToken cancellationToken = default);
}
