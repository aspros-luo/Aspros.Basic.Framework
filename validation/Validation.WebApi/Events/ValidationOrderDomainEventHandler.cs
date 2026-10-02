using Aspros.Base.Framework.Application.Abstractions.Events;
using Aspros.Base.Framework.Application.Abstractions.Messaging;
using Aspros.Base.Framework.Infrastructure;
using Validation.WebApi.Data;
using Validation.WebApi.Models;

namespace Validation.WebApi.Events;

public sealed class ValidationOrderDomainEventHandler(
    AppDbContext db,
    IIntegrationEventPublisher integrationEventPublisher)
    : IDomainEventHandler<OrderCreatedDomainEvent>, IScoped
{
    public async Task HandleAsync(
        OrderCreatedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        db.Audits.Add(new ValidationAudit
        {
            OrderId = domainEvent.OrderId,
            Message = $"Order created: {domainEvent.ProductName}"
        });

        await integrationEventPublisher.PublishAsync(
            "validation.order.created",
            new
            {
                domainEvent.OrderId,
                domainEvent.ProductName
            },
            cancellationToken);
    }
}
