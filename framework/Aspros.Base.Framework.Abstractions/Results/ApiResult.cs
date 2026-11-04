namespace Aspros.Base.Framework.Results;

/// <summary>
/// 全局微服务统一 API 响应信封模型
/// </summary>
/// <typeparam name="TData">业务数据负载类型</typeparam>
public record ApiResult<TData>
{
    public int Code { get; init; } = 200;
    public string Message { get; init; } = "OK";
    public TData? Data { get; init; }
    public bool Success => Code is >= 200 and < 300;
    public string? TraceId { get; init; }
    public long Timestamp { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static ApiResult<TData> Ok(TData data, string message = "OK") => new()
    {
        Code = 200,
        Message = message,
        Data = data
    };

    public static ApiResult<TData> Fail(int code, string message) => new()
    {
        Code = code,
        Message = message,
        Data = default
    };
}

/// <summary>
/// 标准分页返回封装
/// </summary>
public record PagedResult<TItem>
{
    public IReadOnlyList<TItem> Items { get; init; } = [];
    public long TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}
