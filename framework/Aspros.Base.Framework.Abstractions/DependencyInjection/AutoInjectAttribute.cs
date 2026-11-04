namespace Aspros.Base.Framework.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 声明式服务自动依赖注入特性，配合 AddAutoInject() 或 AutoInject() 实现自动化程序集扫描注册。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AutoInjectAttribute(ServiceLifetime lifetime = ServiceLifetime.Scoped) : Attribute
{
    /// <summary>
    /// 服务生命周期 (默认 Scoped)
    /// </summary>
    public ServiceLifetime Lifetime { get; } = lifetime;

    /// <summary>
    /// 显式指定的暴露服务接口类型 (若未指定，则默认实现的所有直接接口)
    /// </summary>
    public Type? ServiceType { get; set; }

    /// <summary>
    /// 是否仅注册为自身类型 (AsSelf)
    /// </summary>
    public bool AsSelf { get; set; }

    /// <summary>
    /// 是否支持多个同类型实现注入 (默认 true)
    /// </summary>
    public bool AllowMultiple { get; set; } = true;
}
