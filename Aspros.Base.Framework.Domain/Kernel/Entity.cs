namespace Aspros.Base.Framework.Domain.Kernel;

/// <summary>
/// DDD 实体基类。
/// 实体通过唯一标识定义自身，而不是通过属性值判断身份。
/// </summary>
public abstract class Entity<TId>
{
    public TId Id { get; }

    protected Entity(TId id)
    {
        Id = id;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj is not Entity<TId> entity)
        {
            return false;
        }

        if (GetType() != entity.GetType())
        {
            return false;
        }

        return EqualityComparer<TId>.Default.Equals(Id, entity.Id);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(GetType(), Id);
    }
}
