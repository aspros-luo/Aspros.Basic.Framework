namespace Aspros.Base.Framework.Domain.Kernel;

/// <summary>
/// 值对象基础类型。
/// 值对象没有唯一标识，通过内部属性判断相等性。
/// </summary>
public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is not ValueObject other)
            return false;

        return GetEqualityComponents()
            .SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Aggregate(0, (hash, obj) => HashCode.Combine(hash, obj));
    }
}
