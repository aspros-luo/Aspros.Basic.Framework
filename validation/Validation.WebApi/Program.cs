using Aspros.Base.Framework.Infrastructure;
using Aspros.Base.Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Validation.WebApi.Data;
using Validation.WebApi.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=validation.db"));

builder.Services.AddScoped<DbContext>(provider =>
    provider.GetRequiredService<AppDbContext>());

builder.Services.AddScoped<ValidationOrderDomainEventHandler>();

builder.Services.AddSingleton<ValidationIntegrationEventPublisher>();

builder.Services.AutoInject();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.MapPost("/orders", async (
    CreateOrderRequest request,
    IUnitOfWork unitOfWork,
    CancellationToken cancellationToken) =>
{
    var orderId = await unitOfWork.ExecuteInTransactionAsync(
        async ct =>
        {
            var order = new ValidationOrder(Guid.NewGuid(), request.ProductName);
            var db = (AppDbContext)scopeDb(unitOfWork);
            db.Orders.Add(order);
            return order.Id;
        },
        cancellationToken);

    return Results.Created($"/orders/{orderId}", new { orderId });
});

app.MapGet("/orders/{id:guid}", async (
    Guid id,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var order = await db.Orders
        .AsNoTracking()
        .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    return order is null
        ? Results.NotFound()
        : Results.Ok(order);
});

app.Run();

static DbContext scopeDb(IUnitOfWork unitOfWork)
    => unitOfWork switch
    {
        EfUnitOfWork ef => GetDbContext(ef),
        _ => throw new InvalidOperationException("Validation requires EfUnitOfWork.")
    };

static DbContext GetDbContext(EfUnitOfWork unitOfWork)
{
    throw new NotSupportedException(
        "The validation endpoint intentionally requires direct DbContext injection; this helper should never be used.");
}

public sealed record CreateOrderRequest(string ProductName);
