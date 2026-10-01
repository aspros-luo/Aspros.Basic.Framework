using MediatR;

namespace Aspros.Base.Framework.Application.Abstractions;

/// <summary>
/// 命令模型基础接口。
/// Command 表示一次需要改变系统状态的业务操作。
/// 通过 MediatR 请求契约进入现有 Application Pipeline。
/// </summary>
public interface ICommand : IRequest
{
}

/// <summary>
/// 带返回值的命令。
/// </summary>
public interface ICommand<TResult> : IRequest<TResult>
{
}
