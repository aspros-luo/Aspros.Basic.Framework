namespace Aspros.Base.Framework.Infrastructure
{
    public interface IEventHandler<in T> where T : IEvent
    {
        Task HandleAsync(T @event);
    }
}
