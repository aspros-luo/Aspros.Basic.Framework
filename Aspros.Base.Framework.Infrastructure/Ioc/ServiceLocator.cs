namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Legacy compatibility shim. New code should use constructor injection.
/// </summary>
[Obsolete("ServiceLocator is retained for compatibility only. Use constructor dependency injection instead.")]
public static class ServiceLocator
{
    public static IServiceProvider Instance { get; set; } = null!;
}
