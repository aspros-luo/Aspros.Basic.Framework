using Microsoft.AspNetCore.Mvc;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Lightweight base MVC controller for the framework response envelope.
/// Framework 轻量级 MVC Controller 基类，用于统一成功/失败响应格式。
/// </summary>
public class WebApiController : Controller
{
    /// <summary>
    /// Creates a failed response using the framework's legacy response shape.
    /// 创建 Framework 传统失败响应格式。
    /// </summary>
    protected ObjectResult Fail(string message)
    {
        return new ObjectResult(new { msg = message, is_success = false });
    }

    /// <summary>
    /// Creates a successful response using the framework's legacy response shape.
    /// 创建 Framework 传统成功响应格式。
    /// </summary>
    protected ObjectResult Success(object obj)
    {
        return new ObjectResult(new { data = obj, is_success = true });
    }
}