namespace Aspros.Base.Framework.Domain;

public class BasicEntity : IAuditableEntity
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

    long IAuditableEntity.CreatedBy
    {
        get => Creator;
        set => Creator = value;
    }

    DateTime IAuditableEntity.CreatedAt
    {
        get => GmtCreated;
        set => GmtCreated = value;
    }

    long IAuditableEntity.ModifiedBy
    {
        get => Modifier;
        set => Modifier = value;
    }

    DateTime IAuditableEntity.ModifiedAt
    {
        get => GmtModified;
        set => GmtModified = value;
    }

    bool IAuditableEntity.IsDeleted
    {
        get => IsDeleted;
        set => IsDeleted = value;
    }
}
