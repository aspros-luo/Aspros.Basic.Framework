namespace Aspros.Base.Framework.Application.Common;

public class ResultModel
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }

    public static ResultModel Success() => new() { IsSuccess = true };
    public static ResultModel Success<T>(T data) => new() { IsSuccess = true, Data = data };
    public static ResultModel Fail(string message) => new() { IsSuccess = false, Message = message };
}