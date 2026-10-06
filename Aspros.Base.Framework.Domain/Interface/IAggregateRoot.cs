namespace Aspros.Base.Framework.Domain
{
    /// <summary>
    /// Marker interface identifying an aggregate root.
    /// 用于标记聚合根的空接口。
    ///
    /// <para>
    /// It intentionally has no members: the Domain layer only needs a type-level
    /// distinction so generic repository contracts can constrain aggregate roots.
    /// 它故意不包含成员；Domain 层只需要一个“类型标记”，让 Repository 等泛型契约
    /// 可以约束参数必须是聚合根。
    /// </para>
    /// </summary>
    public interface IAggregateRoot
    {
    }
}