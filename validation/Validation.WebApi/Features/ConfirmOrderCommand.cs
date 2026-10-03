using Aspros.Base.Framework.Application.Abstractions;
using Aspros.Base.Framework.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using Validation.WebApi.Data;
using Validation.WebApi.Models;

namespace Validation.WebApi.Features;

public sealed record ConfirmOrderCommand(Guid OrderId) : ICommand<bool>;

public sealed class ConfirmOrderCommandHandler(
    AppDbContext db,
    IUnitOfWork unitOfWork) : ICommandHandler<ConfirmOrderCommand, bool>
{
    public Task<bool> HandleAsync(
        ConfirmOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var order = await db.Orders.SingleOrDefaultAsync(
                    x => x.Id == command.OrderId,
                    ct);

                if (order is null)
                    return false;

                order.Confirm();
                return true;
            },
            cancellationToken);
    }
}
