namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Custom display metadata used by enum/name conversion helpers.
/// 用于枚举/名称转换工具的自定义显示元数据。
/// </summary>
[AttributeUsage(
    AttributeTargets.Method |
    AttributeTargets.Property |
    AttributeTargets.Field |
    AttributeTargets.Parameter,
    AllowMultiple = false)]
public class DisplayForAttribute(string name) : Attribute
{
    /// <summary>
    /// Display text.
    /// 显示文本。
    /// </summary>
    public string Name { get; } = name;
}