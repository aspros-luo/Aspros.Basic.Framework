namespace Aspros.Base.Framework.Infrastructure
{
    public interface IEventBus
    {
        Task PublishAsync<T>(T @event) where T : IEvent;
    }
}
