using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Aspros.Base.Framework.Infrastructure;

public static class EnumExtensions
{
    public static string GetDisplayName(this Enum val)
    {
        ArgumentNullException.ThrowIfNull(val);

        var display = GetDisplayAttribute(val);

        return display?.Name
               ?? display?.Description
               ?? val.ToString();
    }

    public static string GetFullName(this Enum val)
    {
        ArgumentNullException.ThrowIfNull(val);

        var display = GetDisplayAttribute(val);
        var description = display?.Description
                          ?? display?.Name
                          ?? val.ToString();

        return $"{val.ToString().ToUnderscoreCase()}:{description}";
    }

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

    private static DisplayAttribute? GetDisplayAttribute(Enum value)
    {
        return value.GetType()
            .GetMember(value.ToString())
            .FirstOrDefault()
            ?.GetCustomAttribute<DisplayAttribute>(false);
    }
}
