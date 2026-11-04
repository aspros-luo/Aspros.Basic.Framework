namespace Aspros.Base.Framework.WorkContext;

/// <summary>
/// 全链路统一工作上下文，隔离租户标识、当前用户身份与调用链追踪元数据。
/// </summary>
public interface IWorkContext
{
    /// <summary>
    /// 多租户标识 (TenantId)
    /// </summary>
    long? TenantId { get; }

    /// <summary>
    /// 当前认证用户唯一标识 (UserId / sub)
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// 用户姓名或账户名
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// 分布式链路追踪跟踪号 (TraceId)
    /// </summary>
    string? TraceId { get; }

    /// <summary>
    /// 跨微服务关联标识 (CorrelationId)
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// 调用方部署可用区 (如 zone-east-1a)
    /// </summary>
    string? CallerZone { get; }

    /// <summary>
    /// 原始 Bearer Token
    /// </summary>
    string? BearerToken { get; }

    /// <summary>
    /// 自定义上下文扩展参数
    /// </summary>
    IReadOnlyDictionary<string, string> Items { get; }
}

/// <summary>
/// 工作上下文访问器，底层基于 AsyncLocal&lt;IWorkContext&gt; 维持协程线程安全。
/// </summary>
public interface IWorkContextAccessor
{
    /// <summary>
    /// 获取当前执行环境上下文
    /// </summary>
    IWorkContext Current { get; }

    /// <summary>
    /// 在当前执行流中设置上下文
    /// </summary>
    void SetCurrent(IWorkContext context);
}

/// <summary>
/// 工作上下文默认只读不可变记录
/// </summary>
public sealed record WorkContextRecord : IWorkContext
{
    public long? TenantId { get; init; }
    public string? UserId { get; init; }
    public string? UserName { get; init; }
    public string? TraceId { get; init; }
    public string? CorrelationId { get; init; }
    public string? CallerZone { get; init; }
    public string? BearerToken { get; init; }
    public IReadOnlyDictionary<string, string> Items { get; init; } = new Dictionary<string, string>();

    public static readonly WorkContextRecord Empty = new();
}

/// <summary>
/// 非 HTTP 异步流 (如 MQ 消费者、定时任务) 派生工作上下文作用域
/// </summary>
public interface IWorkContextScope : IDisposable
{
    IWorkContext Context { get; }
}
