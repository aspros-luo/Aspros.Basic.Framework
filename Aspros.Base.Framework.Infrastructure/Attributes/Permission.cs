namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Marks an MVC action with a permission code that the permission middleware should validate.
/// 在 MVC Action 上标记权限编码，由权限中间件负责校验。
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class Permission(string code) : Attribute
{
    /// <summary>
    /// Permission code used by the external permission service.
    /// 交给权限服务校验的权限编码。
    /// </summary>
    public string Code { get; set; } = code;
}