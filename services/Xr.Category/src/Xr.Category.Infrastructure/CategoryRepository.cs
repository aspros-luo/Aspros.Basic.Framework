using Aspros.Base.Framework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using CategoryEntity = Xr.Category.Domain.Category;

namespace Xr.Category.Infrastructure;

/// <summary>
/// Category-specific EF Core queries. The service owns the concrete query implementation;
/// the Framework repository contract remains the small IQueryable-based abstraction.
/// </summary>
public sealed class CategoryRepository(CategoryDbContext dbContext)
    : ICategoryRepository, IScoped
{
    private readonly CategoryDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public IQueryable<CategoryEntity> GetAll() =>
        _dbContext.Set<CategoryEntity>();

    public Task<CategoryEntity?> FindActiveByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
        => _dbContext.Set<CategoryEntity>().FirstOrDefaultAsync(
            category => category.Id == id && !category.Deleted,
            cancellationToken);

    public async Task<IReadOnlyList<CategoryEntity>> ListActiveAsync(
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default)
        => await _dbContext.Set<CategoryEntity>()
            .Where(category => !category.Deleted)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Id)
            .Skip(offset)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
}
