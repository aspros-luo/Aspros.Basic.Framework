namespace Aspros.Base.Framework.Domain;

public class BaseEntity : IAuditableEntity
{
    public long Creator { get; set; } = 0;
    public DateTime CreateTime { get; set; } = DateTime.Now;
    public long Updater { get; set; } = 0;
    public DateTime UpdateTime { get; set; } = DateTime.Now;
    public bool Deleted { get; set; } = false;

    long IAuditableEntity.CreatedBy
    {
        get => Creator;
        set => Creator = value;
    }

    DateTime IAuditableEntity.CreatedAt
    {
        get => CreateTime;
        set => CreateTime = value;
    }

    long IAuditableEntity.ModifiedBy
    {
        get => Updater;
        set => Updater = value;
    }

    DateTime IAuditableEntity.ModifiedAt
    {
        get => UpdateTime;
        set => UpdateTime = value;
    }

    bool IAuditableEntity.IsDeleted
    {
        get => Deleted;
        set => Deleted = value;
    }
}
