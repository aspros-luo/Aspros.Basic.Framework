using System.ComponentModel.DataAnnotations;

namespace Xr.Category.Application;

public sealed record CreateCategoryRequest
{
    [Required]
    [StringLength(64)]
    public string Code { get; init; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Name { get; init; } = string.Empty;

    public long? ParentId { get; init; }

    public int SortOrder { get; init; }

    public bool IsEnabled { get; init; } = true;
}

public sealed record UpdateCategoryRequest
{
    [Required]
    [StringLength(64)]
    public string Code { get; init; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Name { get; init; } = string.Empty;

    public long? ParentId { get; init; }

    public int SortOrder { get; init; }

    public bool IsEnabled { get; init; } = true;
}

public sealed record CategoryDto(
    long Id,
    string Code,
    string Name,
    long? ParentId,
    int SortOrder,
    bool IsEnabled,
    bool Deleted,
    DateTime CreateTime,
    DateTime UpdateTime);

public interface ICategoryService
{
    Task<CategoryDto?> FindByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryDto>> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<CategoryDto> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        long id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default);
}

public sealed record CategoryDapperSearchRequest
{
    public string? Keyword { get; init; }

    public long? ParentId { get; init; }

    public bool? IsEnabled { get; init; }

    [Range(1, 100000)]
    public int Page { get; init; } = 1;

    [Range(1, 200)]
    public int PageSize { get; init; } = 20;
}

public sealed class CategoryDapperSearchItem
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public long? ParentId { get; set; }

    public string? ParentName { get; set; }

    public int SortOrder { get; set; }

    public bool IsEnabled { get; set; }

    public long ChildCount { get; set; }
}

public sealed record CategoryDapperSearchResult(
    IReadOnlyList<CategoryDapperSearchItem> Items,
    long TotalCount,
    int Page,
    int PageSize);

public interface ICategoryDapperQueryService
{
    Task<CategoryDapperSearchResult> SearchAsync(
        CategoryDapperSearchRequest request,
        CancellationToken cancellationToken = default);
}
