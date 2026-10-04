namespace Aspros.Base.Framework.Infrastructure;

public static class TimeExtensions
{
    private static Func<long> currentTimeFunc = InternalCurrentTimeMillis;

    public static long CurrentTimeMillis() => currentTimeFunc();

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
