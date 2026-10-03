namespace Aspros.Base.Framework.Domain;

/// <summary>
/// 基础仓储查询实现。
/// 不依赖 Infrastructure；具体 DbContext 由 Infrastructure 或业务项目提供 IQueryable。
/// </summary>
public abstract class BaseRepository<TAggregateRoot>(IQueryable<TAggregateRoot> entities)
    : IRepository<TAggregateRoot>
    where TAggregateRoot : class, IAggregateRoot
{
    protected IQueryable<TAggregateRoot> Entities { get; } = entities
        ?? throw new ArgumentNullException(nameof(entities));

    public IQueryable<TAggregateRoot> GetAll() => Entities;
}
