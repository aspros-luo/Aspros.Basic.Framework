using System.ComponentModel.DataAnnotations;

namespace Aspros.Base.Framework.Domain.ValueObjects;

/// <summary>
/// Common lifecycle status for framework entities.
/// Kept in the ValueObjects namespace to avoid polluting the root Domain namespace.
/// </summary>
public enum EntityStatus
{
    [Display(Description = "正常")]
    Normal = 0,

    [Display(Description = "删除")]
    Deleted = -1,

    [Display(Description = "停用")]
    Invalid = -2
}
