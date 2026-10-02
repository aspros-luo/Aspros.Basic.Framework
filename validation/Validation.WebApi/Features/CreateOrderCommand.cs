using Aspros.Base.Framework.Application.Abstractions;
using Aspros.Base.Framework.Application.Abstractions.Persistence;
using Validation.WebApi.Data;
using Validation.WebApi.Models;

namespace Validation.WebApi.Features;

public sealed record CreateOrderCommand(string ProductName) : ICommand<Guid>;

public sealed class CreateOrderCommandHandler(
    AppDbContext db,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateOrderCommand, Guid>
{
    public Task<Guid> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExecuteInTransactionAsync(
            ct =>
            {
                var order = new ValidationOrder(Guid.NewGuid(), command.ProductName);
                db.Orders.Add(order);
                return Task.FromResult(order.Id);
            },
            cancellationToken);
    }
}
