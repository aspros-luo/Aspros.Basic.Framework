namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Time provider helpers used by Snowflake ID generation and tests.
/// Snowflake ID 生成及测试使用的时间提供器辅助方法。
/// </summary>
public static class TimeExtensions
{
    private static Func<long> currentTimeFunc = InternalCurrentTimeMillis;

    /// <summary>
    /// Returns current Unix epoch time in milliseconds.
    /// 返回当前 Unix Epoch 毫秒时间戳。
    /// </summary>
    public static long CurrentTimeMillis() => currentTimeFunc();

    /// <summary>
    /// Temporarily replaces the clock with a custom provider and restores the previous provider on Dispose.
    /// 临时替换时间提供器，并在 Dispose 时恢复之前的时间提供器。
    ///
    /// <para>
    /// This is primarily intended for deterministic tests.
    /// 主要用于让测试可以固定时间，从而稳定验证 Snowflake ID 等时间相关逻辑。
    /// </para>
    /// </summary>
    public static IDisposable StubCurrentTime(Func<long> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        var previous = currentTimeFunc;
        currentTimeFunc = func;

        return new DisposableAction(() =>
        {
            currentTimeFunc = previous;
        });
    }

    /// <summary>
    /// Temporarily fixes the clock to one timestamp and restores the previous provider on Dispose.
    /// 临时固定为指定时间戳，并在 Dispose 时恢复之前的时间提供器。
    /// </summary>
    public static IDisposable StubCurrentTime(long millis)
    {
        var previous = currentTimeFunc;
        currentTimeFunc = () => millis;

        return new DisposableAction(() =>
        {
            currentTimeFunc = previous;
        });
    }

    private static readonly DateTime Jan1st1970 =
        new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static long InternalCurrentTimeMillis()
    {
        return (long)(DateTime.UtcNow - Jan1st1970).TotalMilliseconds;
    }
}