using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Extension methods for reading display metadata from enum values.
/// 用于读取枚举显示元数据的扩展方法。
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// Returns DisplayAttribute.Name, then Description, then the enum member name.
    /// 优先返回 DisplayAttribute.Name，其次 Description，最后返回枚举成员名称。
    /// </summary>
    public static string GetDisplayName(this Enum val)
    {
        ArgumentNullException.ThrowIfNull(val);

        var display = GetDisplayAttribute(val);

        return display?.Name
               ?? display?.Description
               ?? val.ToString();
    }

    /// <summary>
    /// Returns an underscore-case enum key followed by its display description.
    /// 返回下划线格式的枚举 Key，并拼接显示描述。
    /// </summary>
    public static string GetFullName(this Enum val)
    {
        ArgumentNullException.ThrowIfNull(val);

        var display = GetDisplayAttribute(val);
        var description = display?.Description
                          ?? display?.Name
                          ?? val.ToString();

        return $"{val.ToString().ToUnderscoreCase()}:{description}";
    }

    /// <summary>
    /// Builds an anonymous object commonly used by option-list APIs.
    /// 构造一个常用于下拉选项/字典接口的匿名对象。
    /// </summary>
    public static object GetKeyValue(this Enum key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var display = GetDisplayAttribute(key);

        return new
        {
            key,
            name = key.ToString(),
            desc = display?.Description
                   ?? display?.Name
                   ?? key.ToString()
        };
    }

    /// <summary>
    /// Uses DisplayForAttribute when present and returns a name/value pair.
    /// 优先读取 DisplayForAttribute，并返回 name/value 键值对。
    /// </summary>
    public static object GetNameKeyValue(this Enum val)
    {
        ArgumentNullException.ThrowIfNull(val);

        var key = val.ToString().ToUnderscoreCase();

        var value = val.GetType()
            .GetMember(val.ToString())
            .FirstOrDefault()
            ?.GetCustomAttribute<DisplayForAttribute>(false)
            ?.Name
            ?? val.ToString();

        return new { key, value };
    }

    /// <summary>
    /// Finds DisplayAttribute on the concrete enum member through reflection.
    /// 通过反射在具体枚举成员上查找 DisplayAttribute。
    /// </summary>
    private static DisplayAttribute? GetDisplayAttribute(Enum value)
    {
        return value.GetType()
            .GetMember(value.ToString())
            .FirstOrDefault()
            ?.GetCustomAttribute<DisplayAttribute>(false);
    }
}