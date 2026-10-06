namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Executes an Action once when disposed.
/// 在 Dispose 时执行一次 Action。
///
/// <para>
/// <see cref="Interlocked.Exchange(ref int, int)"/> makes repeated Dispose calls harmless
/// even when multiple callers race to dispose the same instance.
/// 使用 Interlocked 保证重复 Dispose 不会重复执行 Action，即使多个线程同时调用 Dispose。
/// </para>
/// </summary>
public sealed class DisposableAction(Action action) : IDisposable
{
    private readonly Action _action =
        action ?? throw new ArgumentNullException(nameof(action));

    private int _disposed;

    /// <summary>
    /// Executes the configured action only on the first call.
    /// 只有第一次调用时才执行配置的 Action。
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _action();
        }
    }
}