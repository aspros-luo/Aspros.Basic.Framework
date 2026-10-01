namespace Aspros.Base.Framework.Domain.Kernel;

/// <summary>
/// 领域事件标记接口。
/// 用于描述领域状态变化，并支持后续事件分发。
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
