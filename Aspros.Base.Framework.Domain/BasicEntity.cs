using Aspros.Base.Framework.Domain.ValueObjects;

namespace Aspros.Base.Framework.Domain;

public class BasicEntity : IAuditableEntity
{
    public long Creator { get; set; } = 0;
    public DateTime GmtCreated { get; set; } = DateTime.Now;
    public long Modifier { get; set; } = 0;
    public DateTime GmtModified { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// 状态，0：正常；-1：删除；-2：停用。
    /// </summary>
    public EntityStatus Status { get; protected set; } = EntityStatus.Normal;

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
