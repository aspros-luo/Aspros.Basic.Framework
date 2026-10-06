namespace Aspros.Base.Framework.Domain;

/// <summary>
/// Defines the read-side contract for an aggregate-root repository.
/// 定义聚合根仓储的基础读取契约。
///
/// <typeparam name="TAggregateRoot">
/// The aggregate-root type managed by this repository.
/// 此仓储负责的聚合根类型。
///
/// The <c>out</c> keyword makes this type parameter covariant.
/// <c>out</c> 表示这个泛型参数是“协变”的。
/// Because the repository only exposes <typeparamref name="TAggregateRoot"/>
/// as an output/read type, a repository of a more specific aggregate type
/// can be used where a repository of a less specific aggregate type is expected.
/// 因为这里的 TAggregateRoot 只用于返回/读取，而没有作为方法参数输入，
/// 所以更具体的仓储类型可以安全地赋值给更抽象的仓储类型。
/// </typeparam>
/// </summary>
public interface IRepository<out TAggregateRoot>
    where TAggregateRoot : class, IAggregateRoot
{
    /// <summary>
    /// Returns a queryable sequence of all aggregate roots.
/// 返回当前仓储对应聚合根的可查询集合。
///
/// <para>
/// This method returns <see cref="IQueryable{T}"/> rather than executing the query.
/// 返回 IQueryable 意味着这里通常只构造查询，不会立即访问数据库；
/// 真正执行一般发生在 First/Single/ToList/Count/Async 等终结操作中。
/// </para>
/// </summary>
    IQueryable<TAggregateRoot> GetAll();
}

/// <summary>
/// Provides access to an entity set without coupling the Domain project to EF Core.
/// 提供实体集合访问能力，使 Domain 层不需要直接依赖 EF Core。
/// </summary>
public interface IEntitySetProvider
{
    /// <summary>
    /// Creates a queryable source for the specified entity type.
/// 获取指定实体类型的可查询数据源。
/// </summary>
    IQueryable<TEntity> Query<TEntity>()
        where TEntity : class;
}
