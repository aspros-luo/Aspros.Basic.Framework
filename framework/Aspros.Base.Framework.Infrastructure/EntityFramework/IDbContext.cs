namespace Aspros.Base.Framework.Infrastructure.EntityFramework;

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Aspros.Base.Framework.Domain.Repositories;

/// <summary>
/// 基础 DbContext 统一抽象接口，并实现 Domain 层的 IEntitySetProvider 保证仓储纯净解耦
/// </summary>
public interface IDbContext : IEntitySetProvider, IDisposable, IAsyncDisposable
{
    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
    ChangeTracker ChangeTracker { get; }
    DbConnection DatabaseConnection { get; }
    IDbContextTransaction? CurrentTransaction { get; }

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
