using Aspros.Base.Framework.Infrastructure;
using Xr.Category.Domain;

namespace Xr.Category.Application;

public sealed class CategoryService(
    ICategoryRepository repository,
    IUnitOfWork unitOfWork) : ICategoryService, IScoped
{
    private readonly ICategoryRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));

    private readonly IUnitOfWork _unitOfWork =
        unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async Task<CategoryDto?> FindByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var category = await _repository.FindActiveByIdAsync(
            id,
            cancellationToken);

        return category is null ? null : ToDto(category);
    }

    public async Task<IReadOnlyList<CategoryDto>> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        if (pageSize is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        var offset = checked((page - 1) * pageSize);
        var categories = await _repository.ListActiveAsync(
            offset,
            pageSize,
            cancellationToken);

        return categories.Select(ToDto).ToArray();
    }

    public async Task<CategoryDto> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var category = new Category(
            request.Code,
            request.Name,
            request.ParentId,
            request.SortOrder,
            request.IsEnabled);

        await _unitOfWork.RegisterNew(category);
        await _unitOfWork.CommitAsync(cancellationToken);

        return ToDto(category);
    }

    public async Task<bool> UpdateAsync(
        long id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var category = await _repository.FindActiveByIdAsync(
            id,
            cancellationToken);

        if (category is null)
        {
            return false;
        }

        category.Update(
            request.Code,
            request.Name,
            request.ParentId,
            request.SortOrder,
            request.IsEnabled);

        await _unitOfWork.RegisterDirty(category);
        await _unitOfWork.CommitAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var category = await _repository.FindActiveByIdAsync(
            id,
            cancellationToken);

        if (category is null)
        {
            return false;
        }

        await _unitOfWork.RegisterDeleted(category);
        await _unitOfWork.CommitAsync(cancellationToken);

        return true;
    }

    private static CategoryDto ToDto(Category category) => new(
        category.Id,
        category.Code,
        category.Name,
        category.ParentId,
        category.SortOrder,
        category.IsEnabled,
        category.Deleted,
        category.CreateTime,
        category.UpdateTime);
}
