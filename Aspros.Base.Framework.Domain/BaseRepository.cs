namespace Aspros.Base.Framework.Domain;

/// <summary>
/// Base implementation for aggregate-root repositories.
/// 聚合根仓储的基础实现。
///
/// <typeparam name="TAggregateRoot">
/// The aggregate-root entity type handled by this repository.
/// 当前仓储处理的聚合根类型。
/// </typeparam>
/// </summary>
public abstract class BaseRepository<TAggregateRoot> : IRepository<TAggregateRoot>
    where TAggregateRoot : class, IAggregateRoot
{
    /// <summary>
    /// Creates a repository backed by an entity-set provider.
/// 使用实体集合提供者创建仓储。
///
/// <para>
/// The Domain repository does not know whether the provider is EF Core, Dapper,
/// or another persistence implementation.
/// Domain 仓储不关心底层到底是 EF Core、Dapper 还是其他持久化实现。
/// </para>
/// </summary>
    protected BaseRepository(IEntitySetProvider setProvider)
    {
        ArgumentNullException.ThrowIfNull(setProvider);
        Entities = setProvider.Query<TAggregateRoot>();
    }

    /// <summary>
    /// The query source for this aggregate root.
/// 当前聚合根的查询源。
/// </summary>
    protected IQueryable<TAggregateRoot> Entities { get; }

    /// <summary>
    /// Returns the query source without executing it.
/// 返回查询源本身，不立即执行数据库查询。
/// </summary>
    public IQueryable<TAggregateRoot> GetAll() => Entities;
}
