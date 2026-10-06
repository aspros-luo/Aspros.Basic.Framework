namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Legacy compatibility shim. New code should use constructor injection.
/// 历史兼容层。新代码应该优先使用构造函数依赖注入。
///
/// <para>
/// Keeping this type avoids breaking older applications during migration,
/// but global service access hides dependencies and makes testing harder.
/// 保留它是为了迁移期间不立即破坏旧业务；但全局取服务会隐藏真实依赖，
/// 也会增加单元测试和生命周期管理的复杂度。
/// </para>
/// </summary>
[Obsolete("ServiceLocator is retained for compatibility only. Use constructor dependency injection instead.")]
public static class ServiceLocator
{
    /// <summary>
    /// The legacy global service provider.
    /// 历史兼容用的全局 IServiceProvider。
    /// </summary>
    public static IServiceProvider Instance { get; set; } = null!;
}