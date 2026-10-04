using Aspros.Base.Framework.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Aspros.Base.Framework.Infrastructure;

public interface IDbContext : IScoped, IDisposable, IEntitySetProvider
{
    DatabaseFacade Database { get; }

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity)
        where TEntity : class;

    ChangeTracker ChangeTracker { get; }

    void ClearTrackedChanges() => ChangeTracker.Clear();

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);

    DbSet<TEntity> Set<TEntity>()
        where TEntity : class;

    IQueryable<TEntity> IEntitySetProvider.Query<TEntity>()
        => Set<TEntity>();
}
