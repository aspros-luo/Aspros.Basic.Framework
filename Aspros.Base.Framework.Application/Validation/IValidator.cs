namespace Aspros.Base.Framework.Application.Validation;

/// <summary>
/// 应用层验证器抽象。
/// 用于 Command/Query 执行前的数据和业务规则校验。
/// </summary>
public interface IValidator<TRequest>
{
    Task<IReadOnlyCollection<ValidationError>> ValidateAsync(
        TRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ValidationError(string Code, string Message);
