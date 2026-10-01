namespace Aspros.Base.Framework.Domain.Kernel;

/// <summary>
/// 统一错误描述。
/// 用于 Application 与 API 层传递业务错误。
/// </summary>
public sealed record Error(string Code, string Message)
{
    public static Error None => new(string.Empty, string.Empty);

    public static Error Validation(string code, string message)
        => new(code, message);
}
