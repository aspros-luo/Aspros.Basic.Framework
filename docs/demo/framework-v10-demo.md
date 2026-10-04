# Framework v10 Demo

This example shows the intended application flow without introducing a second runtime.

## 1. Domain

```csharp
using Aspros.Base.Framework.Domain.Kernel;

public sealed class Order : AggregateRoot<long>
{
    public decimal Total { get; private set; }
    public bool IsPaid { get; private set; }

    public Order(long id, decimal total) : base(id)
    {
        Total = total;
    }

    public void Pay()
    {
        if (IsPaid)
            throw new InvalidOperationException("Order is already paid.");

        IsPaid = true;
        AddDomainEvent(new OrderPaid(Id));
    }
}

public sealed class OrderPaid(long orderId) : DomainEvent
{
    public long OrderId { get; } = orderId;
}
```

## 2. Domain Event Handler

```csharp
public sealed class OrderPaidHandler(IIntegrationEventPublisher publisher)
    : IDomainEventHandler<OrderPaid>
{
    public Task HandleAsync(
        OrderPaid domainEvent,
        CancellationToken cancellationToken = default)
        => publisher.PublishAsync(
            "order.paid",
            new { domainEvent.OrderId },
            cancellationToken);
}
```

The Handler stays in Application. The broker/Outbox implementation stays in Infrastructure.

## 3. Command

```csharp
public sealed record PayOrderCommand(long OrderId) : ICommand<bool>;

public sealed class PayOrderHandler(
    IOrderRepository orders,
    ITransactionalUnitOfWork unitOfWork)
    : ICommandHandler<PayOrderCommand, bool>
{
    public async Task<bool> HandleAsync(
        PayOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        return await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var order = await orders.GetByIdAsync(command.OrderId, ct)
                    ?? throw new KeyNotFoundException("Order not found.");

                order.Pay();
                await unitOfWork.RegisterDirty(order);
                return true;
            },
            cancellationToken);
    }
}
```

## 4. HTTP

```csharp
[ApiController]
[Route("orders")]
public sealed class OrdersController(ISender sender) : ControllerBase
{
    [HttpPost("{id:long}/pay")]
    public Task<bool> Pay(
        long id,
        CancellationToken cancellationToken)
        => sender.Send(new PayOrderCommand(id), cancellationToken);
}
```

## 5. DI

```csharp
services.AddScoped<IOrderRepository, OrderRepository>();
services.AddScoped<IDomainEventHandler<OrderPaid>, OrderPaidHandler>();
services.AddScoped<ITransactionalUnitOfWork, EfUnitOfWork>();
services.AddMediatR(config =>
    config.RegisterServicesFromAssembly(typeof(PayOrderHandler).Assembly));
```

AutoInject can be used instead, but it is only a convenience layer.

## 6. Flow

Simple change:

```text
Controller
   ↓
MediatR
   ↓
Handler
   ↓
RegisterDirty
   ↓
CommitAsync
```

Atomic multi-step change with Domain Events:

```text
Controller
   ↓
MediatR
   ↓
Handler
   ↓
ExecuteInTransactionAsync
   ↓
SaveChanges
   ↓
Domain Event
   ↓
Integration Event / Outbox
   ↓
SaveChanges
   ↓
Commit
```

The intended model is simple by default and explicit when consistency requires it.