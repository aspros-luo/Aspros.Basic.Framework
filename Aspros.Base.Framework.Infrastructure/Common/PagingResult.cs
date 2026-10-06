namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Generic paged-query result.
/// 通用分页查询结果。
/// </summary>
public sealed class PagingResult<T>
{
    /// <summary>
    /// Creates a paging result and normalizes invalid total/page-size values.
    /// 创建分页结果，并对非法的总数和页大小进行安全归一化。
    /// </summary>
    public PagingResult(
        IEnumerable<T> data,
        int totalCount,
        int pageSize)
    {
        ArgumentNullException.ThrowIfNull(data);

        Data = data;
        TotalCount = Math.Max(0, totalCount);
        PageSize = pageSize > 0 ? pageSize : 10;
    }

    /// <summary>
    /// Current page data.
    /// 当前页数据。
    /// </summary>
    public IEnumerable<T> Data { get; }

    /// <summary>
    /// Total number of matching records.
    /// 符合查询条件的总记录数。
    /// </summary>
    public int TotalCount { get; }

    /// <summary>
    /// Page size after normalization.
    /// 归一化后的每页数量。
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    /// Total number of pages.
    /// 总页数。
    ///
    /// <para>
    /// The formula avoids floating-point arithmetic:
    /// <c>(TotalCount + PageSize - 1) / PageSize</c>.
    /// 使用整数运算计算向上取整，不需要浮点数：
    /// <c>(TotalCount + PageSize - 1) / PageSize</c>。
    /// </para>
    /// </summary>
    public int TotalPage =>
        TotalCount == 0
            ? 0
            : (TotalCount + PageSize - 1) / PageSize;
}

/// <summary>
/// Input parameters for a paged query.
/// 分页查询的输入参数。
/// </summary>
public sealed class PagingParams
{
    /// <summary>
    /// Creates normalized page-number and page-size values.
    /// 创建经过归一化的页码和页大小。
    /// </summary>
    public PagingParams(
        int pageNo,
        int pageSize = 10)
    {
        PageNo = pageNo > 0 ? pageNo : 1;
        PageSize = pageSize > 0 ? pageSize : 10;
    }

    public int PageNo { get; set; }

    public int PageSize { get; set; }

    /// <summary>
    /// Number of records to skip before reading the requested page.
    /// 查询当前页前需要跳过的记录数。
    /// </summary>
    public int Skip =>
        (PageNo - 1) * PageSize;
}