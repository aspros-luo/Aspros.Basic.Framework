namespace Aspros.Base.Framework.Domain;

/// <summary>
/// 统一的审计字段契约。
/// 兼容 Framework 旧版 BaseEntity / BasicEntity 两套字段命名，基础设施只依赖这一套语义。
/// </summary>
public interface IAuditableEntity
{
    long CreatedBy { get; set; }
    DateTime CreatedAt { get; set; }
    long ModifiedBy { get; set; }
    DateTime ModifiedAt { get; set; }
    bool IsDeleted { get; set; }
}
