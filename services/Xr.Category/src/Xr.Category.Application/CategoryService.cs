using Aspros.Base.Framework.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Xr.Category.Domain;
using CategoryEntity = Xr.Category.Domain.Category;

namespace Xr.Category.Application;

public sealed class CategoryService(
    ICategoryRepository repository,
    IUnitOfWork unitOfWork,
    IDistributedCache cache,
    ILogger<CategoryService> logger) : ICategoryService, IScoped
{
    private static readonly TimeSpan CategoryDetailCacheDuration =
        TimeSpan.FromMinutes(5);

    private const string CategoryDetailCacheKeyPrefix = "category:detail:";

    private readonly ICategoryRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));

    private readonly IUnitOfWork _unitOfWork =
        unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    private readonly IDistributedCache _cache =
        cache ?? throw new ArgumentNullException(nameof(cache));

    private readonly ILogger<CategoryService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<CategoryDto?> FindByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var cachedCategory = await TryGetCachedCategoryAsync(
            id,
            cancellationToken);

        if (cachedCategory is not null)
        {
            return cachedCategory;
        }

        var category = await _repository.FindActiveByIdAsync(
            id,
            cancellationToken);

        if (category is null)
        {
            return null;
        }

        var result = ToDto(category);
        await TrySetCachedCategoryAsync(id, result, cancellationToken);

        return result;
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

        var category = new CategoryEntity(
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

        await RemoveCachedCategoryAsync(id, cancellationToken);

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

        await RemoveCachedCategoryAsync(id, cancellationToken);

        return true;
    }

    private async Task<CategoryDto?> TryGetCachedCategoryAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var cacheKey = GetCategoryDetailCacheKey(id);

        try
        {
            var cachedJson = await _cache.GetStringAsync(
                cacheKey,
                cancellationToken);

            if (cachedJson is null)
            {
                return null;
            }

            try
            {
                var cachedCategory =
                    JsonSerializer.Deserialize<CategoryDto>(cachedJson);

                if (cachedCategory is not null)
                {
                    return cachedCategory;
                }
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Invalid cached category payload for category {CategoryId}; removing it.",
                    id);
            }

            await _cache.RemoveAsync(cacheKey, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Redis cache read failed for category {CategoryId}; falling back to MySQL.",
                id);
        }

        return null;
    }

    private async Task TrySetCachedCategoryAsync(
        long id,
        CategoryDto category,
        CancellationToken cancellationToken)
    {
        try
        {
            await _cache.SetStringAsync(
                GetCategoryDetailCacheKey(id),
                JsonSerializer.Serialize(category),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CategoryDetailCacheDuration
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Redis cache write failed for category {CategoryId}; returning the MySQL result.",
                id);
        }
    }

    private async Task RemoveCachedCategoryAsync(
        long id,
        CancellationToken cancellationToken)
    {
        try
        {
            await _cache.RemoveAsync(
                GetCategoryDetailCacheKey(id),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Redis cache invalidation failed for category {CategoryId}; the cache entry may remain until its TTL expires.",
                id);
        }
    }

    private static string GetCategoryDetailCacheKey(long id)
        => $"{CategoryDetailCacheKeyPrefix}{id}";

    private static CategoryDto ToDto(CategoryEntity category) => new(
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
