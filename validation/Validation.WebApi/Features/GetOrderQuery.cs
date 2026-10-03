using Aspros.Base.Framework.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Validation.WebApi.Data;

namespace Validation.WebApi.Features;

public sealed record GetOrderQuery(Guid OrderId) : IQuery<OrderResponse?>;

public sealed record OrderResponse(
    Guid Id,
    string ProductName,
    bool Confirmed,
    string[] AuditMessages);

public sealed class GetOrderQueryHandler(AppDbContext db)
    : IQueryHandler<GetOrderQuery, OrderResponse?>
{
    public async Task<OrderResponse?> HandleAsync(
        GetOrderQuery query,
        CancellationToken cancellationToken = default)
    {
        var order = await db.Orders
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == query.OrderId, cancellationToken);

        if (order is null)
            return null;

        var audits = await db.Audits
            .AsNoTracking()
            .Where(x => x.OrderId == query.OrderId)
            .Select(x => x.Message)
            .ToArrayAsync(cancellationToken);

        return new OrderResponse(
            order.Id,
            order.ProductName,
            order.Confirmed,
            audits);
    }
}
