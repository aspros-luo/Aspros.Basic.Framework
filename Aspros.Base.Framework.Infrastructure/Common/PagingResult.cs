namespace Aspros.Base.Framework.Infrastructure;

public sealed class PagingResult<T>
{
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

    public IEnumerable<T> Data { get; }

    public int TotalCount { get; }

    public int PageSize { get; }

    public int TotalPage =>
        TotalCount == 0
            ? 0
            : (TotalCount + PageSize - 1) / PageSize;
}

public sealed class PagingParams
{
    public PagingParams(
        int pageNo,
        int pageSize = 10)
    {
        PageNo = pageNo > 0 ? pageNo : 1;
        PageSize = pageSize > 0 ? pageSize : 10;
    }

    public int PageNo { get; set; }

    public int PageSize { get; set; }

    public int Skip =>
        (PageNo - 1) * PageSize;
}
