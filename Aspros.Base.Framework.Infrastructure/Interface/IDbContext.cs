using Aspros.Base.Framework.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Minimal database abstraction exposed to the Framework.
/// Framework 对数据库暴露的最小抽象。
///
/// <para>
/// Domain code depends on IEntitySetProvider instead of EF Core directly.
/// Domain 层只依赖 IEntitySetProvider，不直接依赖 EF Core。
/// Infrastructure provides the actual EF Core implementation.
/// Infrastructure 层再负责提供真正的 EF Core 实现。
/// </para>
/// </summary>
public interface IDbContext : IScoped, IDisposable, IEntitySetProvider
{
    /// <summary>
    /// EF Core database facade.
/// EF Core 数据库级操作入口。
/// </summary>
    DatabaseFacade Database { get; }

    /// <summary>
    /// Gets the EF Core tracking entry for an entity.
/// 获取实体对应的 EF Core ChangeTracker Entry。
/// </summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity)
        where TEntity : class;

    /// <summary>
    /// Provides access to EF Core's change tracker.
/// 提供 EF Core ChangeTracker，用于查看和控制实体状态。
/// </summary>
    ChangeTracker ChangeTracker { get; }

    /// <summary>
    /// Clears all currently tracked entities.
/// 清除当前 DbContext 中所有被跟踪的实体。
///
/// <para>
/// The UnitOfWork uses this after rollback so reverted changes cannot be
/// accidentally persisted by a later SaveChanges call.
/// UnitOfWork 会在回滚后调用它，防止已经回滚的修改被后续 SaveChanges 再次提交。
/// </para>
/// </summary>
    void ClearTrackedChanges() => ChangeTracker.Clear();

    /// <summary>
    /// Persists the currently tracked changes.
/// 保存当前 ChangeTracker 中的修改。
/// </summary>
    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the EF Core entity set for the specified entity type.
/// 获取指定实体类型对应的 EF Core DbSet。
/// </summary>
    DbSet<TEntity> Set<TEntity>()
        where TEntity : class;

    /// <summary>
    /// Bridges the Domain IEntitySetProvider abstraction to EF Core.
/// 将 Domain 层的 IEntitySetProvider 抽象适配到 EF Core。
/// </summary>
    IQueryable<TEntity> IEntitySetProvider.Query<TEntity>()
        => Set<TEntity>();
}