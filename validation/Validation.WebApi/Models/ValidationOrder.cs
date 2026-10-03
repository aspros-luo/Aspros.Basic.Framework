using Aspros.Base.Framework.Domain.Kernel;
using Validation.WebApi.Events;

namespace Validation.WebApi.Models;

public sealed class ValidationOrder : AggregateRoot<Guid>
{
    public string ProductName { get; private set; }
    public bool Confirmed { get; private set; }

    private ValidationOrder() : base(Guid.Empty)
    {
        ProductName = string.Empty;
    }

    public ValidationOrder(Guid id, string productName) : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ProductName = productName;
    }

    public void Confirm()
    {
        if (Confirmed)
            return;

        Confirmed = true;
        AddDomainEvent(new OrderConfirmedDomainEvent(Id, ProductName));
    }
}

public sealed class ValidationAudit
{
    public int Id { get; set; }
    public Guid OrderId { get; set; }
    public string Message { get; set; } = string.Empty;
}
