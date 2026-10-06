namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Lightweight application-level result envelope.
/// 轻量级业务结果包装对象。
///
/// <para>
/// This is intentionally independent from ASP.NET Core ActionResult so application
/// services can return it without coupling themselves to MVC.
/// 它故意不依赖 ASP.NET Core ActionResult，让 Service 层可以使用而不绑定 MVC。
/// </para>
/// </summary>
public class ResultModel
{
    public bool IsSuccess { get; set; }

    public string Message { get; set; } = string.Empty;

    public object? Data { get; set; }

    /// <summary>
    /// Creates a successful result without payload data.
    /// 创建一个不带数据的成功结果。
    /// </summary>
    public static ResultModel Success()
    {
        return new ResultModel { IsSuccess = true };
    }

    /// <summary>
    /// Creates a successful result with typed payload data.
    /// 创建一个带泛型数据载荷的成功结果。
    /// </summary>
    public static ResultModel Success<T>(T data)
    {
        return new ResultModel
        {
            IsSuccess = true,
            Data = data
        };
    }

    /// <summary>
    /// Creates a failed result and normalizes a null message to an empty string.
    /// 创建失败结果，并将 null 消息统一转换为空字符串。
    /// </summary>
    public static ResultModel Fail(string? message)
    {
        return new ResultModel
        {
            IsSuccess = false,
            Message = message ?? string.Empty
        };
    }
}