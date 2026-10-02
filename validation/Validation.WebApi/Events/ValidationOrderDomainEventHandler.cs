using Aspros.Base.Framework.Application.Abstractions.Events;
using Validation.WebApi.Data;
using Validation.WebApi.Models;

namespace Validation.WebApi.Events;

public sealed class ValidationOrderDomainEventHandler(AppDbContext db)
    : IDomainEventHandler<OrderCreatedDomainEvent>
{
    public Task HandleAsync(
        OrderCreatedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        db.Audits.Add(new ValidationAudit
        {
            OrderId = domainEvent.OrderId,
            Message = $"Order created: {domainEvent.ProductName}"
        });

        return Task.CompletedTask;
    }
}
