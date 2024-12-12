namespace Aspros.Base.Framework.Domain;

public class BaseEntity
{
    public long Creator { get; set; } = 0;
    public DateTime CreateTime { get; set; } = DateTime.Now;
    public long Updater { get; set; } = 0;
    public DateTime UpdateTime { get; set; } = DateTime.Now;
    public bool Deleted { get; set; } = false;
}
