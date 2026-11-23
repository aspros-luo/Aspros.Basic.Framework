using Aspros.Base.Framework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using CategoryEntity = Xr.Category.Domain.Category;

namespace Xr.Category.Infrastructure;

/// <summary>
/// Database context owned by the Category service.
/// </summary>
public sealed class CategoryDbContext(
    DbContextOptions<CategoryDbContext> options)
    : DbContext(options), IDbContext
{
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.AddEntityConfigurationsFromAssembly(
            typeof(CategoryDbContext).Assembly);
    }
}
