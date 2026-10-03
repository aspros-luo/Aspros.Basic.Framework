using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Aspros.Base.Framework.Application.Common;

public static class EnumExtensions
{
    public static object GetKeyValue(this Enum value)
    {
        var name = value.ToString();
        var description = value
            .GetType()
            .GetMember(name)
            .FirstOrDefault()
            ?.GetCustomAttribute<DisplayAttribute>(false)
            ?.Description ?? name;

        return new { key = value, name, desc = description };
    }
}
