namespace Aspros.Base.Framework.Idempotency;

/// <summary>
/// 声明式接口与事务方法防重幂等切面特性。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class IdempotentAttribute : Attribute
{
    /// <summary>
    /// 幂等缓存与租约有效持续时间 (默认 60 秒)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// 请求 Header 中指定的幂等键名 (默认 x-idempotency-key)
    /// </summary>
    public string HeaderKey { get; set; } = "x-idempotency-key";

    /// <summary>
    /// 是否在并发命中锁时返回 409 Conflict，还是挂起等待首次计算结果 (默认 true 返回缓存)
    /// </summary>
    public bool ReturnCachedOnDuplicate { get; set; } = true;
}

/// <summary>
/// 幂等键状态枚举
/// </summary>
public enum IdempotentStatus
{
    Executing = 1,
    Committed = 2,
    Failed = 3
}

/// <summary>
/// 幂等账本存储契约
/// </summary>
public interface IIdempotencyStore
{
    ValueTask<bool> TryAcquireLeaseAsync(string key, TimeSpan timeout, CancellationToken cancellationToken = default);
    ValueTask SaveResultAsync(string key, object result, TimeSpan ttl, CancellationToken cancellationToken = default);
    ValueTask<object?> GetResultAsync(string key, CancellationToken cancellationToken = default);
    ValueTask ReleaseLeaseAsync(string key, CancellationToken cancellationToken = default);
}
