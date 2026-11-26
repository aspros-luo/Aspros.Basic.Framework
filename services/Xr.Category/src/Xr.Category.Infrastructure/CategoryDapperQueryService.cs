using Aspros.Base.Framework.Infrastructure;
using Xr.Category.Application;

namespace Xr.Category.Infrastructure;

/// <summary>
/// Executes the read-heavy category search through the Framework Dapper adapter.
/// SQL stays in Infrastructure; the Application layer receives a query result contract.
/// </summary>
public sealed class CategoryDapperQueryService(
    IDapperExecutor dapperExecutor) : ICategoryDapperQueryService, IScoped
{
    private const string SearchSql = """
        SELECT
            c.id AS Id,
            c.code AS Code,
            c.name AS Name,
            c.parent_id AS ParentId,
            parent.name AS ParentName,
            c.sort_order AS SortOrder,
            c.is_enabled AS IsEnabled,
            COUNT(child.id) AS ChildCount
        FROM category AS c
        LEFT JOIN category AS parent
            ON parent.id = c.parent_id
            AND parent.is_deleted = 0
        LEFT JOIN category AS child
            ON child.parent_id = c.id
            AND child.is_deleted = 0
        WHERE c.is_deleted = 0
            AND (
                @Keyword IS NULL
                OR c.name LIKE CONCAT('%', @Keyword, '%')
                OR c.code LIKE CONCAT('%', @Keyword, '%')
            )
            AND (@ParentId IS NULL OR c.parent_id = @ParentId)
            AND (@IsEnabled IS NULL OR c.is_enabled = @IsEnabled)
        GROUP BY
            c.id,
            c.code,
            c.name,
            c.parent_id,
            parent.name,
            c.sort_order,
            c.is_enabled
        ORDER BY c.sort_order ASC, c.id ASC
        LIMIT @Offset, @PageSize;
        """;

    private const string CountSql = """
        SELECT COUNT(*)
        FROM category AS c
        WHERE c.is_deleted = 0
            AND (
                @Keyword IS NULL
                OR c.name LIKE CONCAT('%', @Keyword, '%')
                OR c.code LIKE CONCAT('%', @Keyword, '%')
            )
            AND (@ParentId IS NULL OR c.parent_id = @ParentId)
            AND (@IsEnabled IS NULL OR c.is_enabled = @IsEnabled);
        """;

    private readonly IDapperExecutor _dapperExecutor =
        dapperExecutor ?? throw new ArgumentNullException(nameof(dapperExecutor));

    public async Task<CategoryDapperSearchResult> SearchAsync(
        CategoryDapperSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Page));
        }

        if (request.PageSize is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(request.PageSize));
        }

        var parameters = new
        {
            Keyword = string.IsNullOrWhiteSpace(request.Keyword)
                ? null
                : request.Keyword.Trim(),
            request.ParentId,
            request.IsEnabled,
            Offset = checked((request.Page - 1) * request.PageSize),
            request.PageSize
        };

        var items = (await _dapperExecutor.QueryAsync<CategoryDapperSearchItem>(
            SearchSql,
            parameters,
            cancellationToken)).ToArray();

        var totalCount = await _dapperExecutor.QuerySingleOrDefaultAsync<long>(
            CountSql,
            parameters,
            cancellationToken);

        return new CategoryDapperSearchResult(
            items,
            totalCount,
            request.Page,
            request.PageSize);
    }
}
