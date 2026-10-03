namespace Aspros.Base.Framework.Domain;

public class TenantEntity : BasicEntity, ITenantEntity
{
    public long TenantId { get; set; } = 0;
}
