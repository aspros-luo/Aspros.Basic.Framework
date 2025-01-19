namespace Aspros.Base.Framework.Infrastructure
{
    public class DisposableAction(Action action) : IDisposable
    {
        readonly Action _action = action ?? throw new ArgumentNullException("action");

        public void Dispose() => _action();
    }
}