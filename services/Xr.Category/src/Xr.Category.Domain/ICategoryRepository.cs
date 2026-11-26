using Aspros.Base.Framework.Domain;

namespace Xr.Category.Domain;

public interface ICategoryRepository : IRepository<Category>
{
    Task<Category?> FindActiveByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Category>> ListActiveAsync(
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default);
}
