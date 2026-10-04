namespace Aspros.Base.Framework.Infrastructure;

public sealed class DisposableAction(Action action) : IDisposable
{
    private readonly Action _action =
        action ?? throw new ArgumentNullException(nameof(action));

    private int _disposed;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _action();
        }
    }
}
