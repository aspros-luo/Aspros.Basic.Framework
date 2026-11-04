namespace Aspros.Base.Framework.Domain.Entities;

/// <summary>
/// 实体唯一标识基类
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class Entity<TKey>
{
    public virtual TKey Id { get; protected set; } = default!;

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TKey> other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        if (EqualityComparer<TKey>.Default.Equals(Id, default)) return false;
        return EqualityComparer<TKey>.Default.Equals(Id, other.Id);
    }

    public override int GetHashCode() => EqualityComparer<TKey>.Default.GetHashCode(Id!);
}

/// <summary>
/// 默认基于 long/Guid 的标准实体
/// </summary>
public abstract class Entity : Entity<long>
{
}

/// <summary>
/// 软删除契约标记
/// </summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedTime { get; set; }
}

/// <summary>
/// 租户隔离契约标记
/// </summary>
public interface ITenantEntity
{
    long TenantId { get; set; }
}

/// <summary>
/// 审计跟踪契约
/// </summary>
public interface IAuditedEntity
{
    DateTimeOffset CreatedTime { get; set; }
    string? CreatedBy { get; set; }
    DateTimeOffset? UpdatedTime { get; set; }
    string? UpdatedBy { get; set; }
}
