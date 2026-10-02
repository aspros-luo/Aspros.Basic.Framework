using Aspros.Base.Framework.Application.Abstractions.Messaging;

namespace Validation.WebApi.Events;

public sealed class ValidationIntegrationEventPublisher : IIntegrationEventPublisher
{
    public List<string> PublishedEvents { get; } = [];

    public Task PublishAsync<TEvent>(
        string name,
        TEvent eventData,
        CancellationToken cancellationToken = default)
    {
        PublishedEvents.Add(name);
        return Task.CompletedTask;
    }
}
