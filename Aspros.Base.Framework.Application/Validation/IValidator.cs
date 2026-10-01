namespace Aspros.Base.Framework.Application.Validation;

/// <summary>
/// 可选的应用层验证器抽象。
/// Framework 不强制使用自带验证机制，具体项目可以接入 ASP.NET Core、DataAnnotations、FluentValidation 等方案。
/// </summary>
public interface IValidator<TRequest>
{
    Task<IReadOnlyCollection<ValidationError>> ValidateAsync(
        TRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ValidationError(string Code, string Message);
