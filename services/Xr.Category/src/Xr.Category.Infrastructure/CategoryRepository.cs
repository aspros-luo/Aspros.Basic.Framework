using Aspros.Base.Framework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xr.Category.Domain;

namespace Xr.Category.Infrastructure;

public sealed class CategoryRepository(IDbContext dbContext)
    : BaseRepository<Category>(dbContext), ICategoryRepository, IScoped
{
    public Task<Category?> FindActiveByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
        => Entities.FirstOrDefaultAsync(
            category => category.Id == id && !category.Deleted,
            cancellationToken);

    public async Task<IReadOnlyList<Category>> ListActiveAsync(
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default)
        => await Entities
            .Where(category => !category.Deleted)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Id)
            .Skip(offset)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
}
