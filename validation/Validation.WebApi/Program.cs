using Aspros.Base.Framework.Application.Abstractions.Persistence;
using Aspros.Base.Framework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Validation.WebApi.Data;
using Validation.WebApi.Events;
using Validation.WebApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=validation.db"));

builder.Services.AddScoped<DbContext>(provider =>
    provider.GetRequiredService<AppDbContext>());

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
    AppDbContext db,
    IUnitOfWork unitOfWork,
    CancellationToken cancellationToken) =>
{
    var orderId = await unitOfWork.ExecuteInTransactionAsync(
        ct =>
        {
            var order = new ValidationOrder(Guid.NewGuid(), request.ProductName);
            db.Orders.Add(order);
            return Task.FromResult(order.Id);
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

public sealed record CreateOrderRequest(string ProductName);
