namespace Aspros.Base.Framework.Domain.Kernel;

/// <summary>
/// 领域层统一结果模型。
/// 避免使用异常控制正常业务流程。
/// </summary>
public class Result
{
    protected Result(bool success, Error error)
    {
        IsSuccess = success;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success()
        => new(true, Error.None);

    public static Result Failure(Error error)
        => new(false, error);
}

public sealed class Result<T> : Result
{
    private Result(T value) : base(true, Error.None)
    {
        Value = value;
    }

    private Result(Error error) : base(false, error)
    {
        Value = default;
    }

    public T? Value { get; }

    public static Result<T> Success(T value)
        => new(value);

    public new static Result<T> Failure(Error error)
        => new(error);
}
