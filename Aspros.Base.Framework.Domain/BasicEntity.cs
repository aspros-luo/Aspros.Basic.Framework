
namespace Aspros.Base.Framework.Domain;

public class BasicEntity
{
    public long Creator { get; set; } = 0;
    public DateTime GmtCreated { get; set; } = DateTime.Now;
    public long Modifier { get; set; } = 0;
    public DateTime GmtModified { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; } = false;
    /// <summary>
    /// 状态，1：正常；-1：删除；-2：屏蔽
    /// </summary>
    public Status Status { get; protected set; } = Status.Normal;
}
