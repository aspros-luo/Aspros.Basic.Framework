namespace Aspros.Base.Framework.Application.Abstractions;

/// <summary>
/// 命令模型基础接口。
/// Command 表示一次需要改变系统状态的业务操作。
/// </summary>
public interface ICommand
{
}

/// <summary>
/// 带返回值的命令。
/// </summary>
public interface ICommand<TResult> : ICommand
{
}
