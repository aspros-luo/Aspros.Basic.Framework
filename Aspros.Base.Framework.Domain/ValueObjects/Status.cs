using System.ComponentModel.DataAnnotations;

namespace Aspros.SaaS.System.Domain
{
    public enum Status
    {
        [Display(Description = "正常")]
        Normal = 0,
        [Display(Description = "删除")]
        Deleted = -1,
        [Display(Description = "停用")]
        Invalid = -2,
    }
}
