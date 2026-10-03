using Aspros.Base.Framework.Application.Abstractions.Messaging;

namespace Validation.WebApi.Events;

public sealed class ValidationIntegrationEventPublisher : IIntegrationEventPublisher
{
    public List<string> PublishedEvents { get; } = [];

    public bool FailNextPublish { get; set; }

    public Task PublishAsync<TEvent>(
        string name,
        TEvent eventData,
        CancellationToken cancellationToken = default)
    {
        if (FailNextPublish)
        {
            FailNextPublish = false;
            throw new InvalidOperationException("Validation integration publisher failure.");
        }

        PublishedEvents.Add(name);
        return Task.CompletedTask;
    }
}
