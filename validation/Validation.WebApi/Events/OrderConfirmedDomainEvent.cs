using Aspros.Base.Framework.Domain.Kernel;

namespace Validation.WebApi.Events;

public sealed class OrderConfirmedDomainEvent(Guid orderId, string productName) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public string ProductName { get; } = productName;
}
