using Aspros.Base.Framework.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Validation.WebApi.Data;
using Validation.WebApi.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=validation.db"));

builder.Services.AddScoped<Microsoft.EntityFrameworkCore.DbContext>(provider =>
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
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var orderId = await sender.Send(
        new CreateOrderCommand(request.ProductName),
        cancellationToken);

    return Results.Created($"/orders/{orderId}", new { orderId });
});

app.MapGet("/orders/{id:guid}", async (
    Guid id,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var order = await sender.Send(
        new GetOrderQuery(id),
        cancellationToken);

    return order is null
        ? Results.NotFound()
        : Results.Ok(order);
});

app.Run();

public sealed record CreateOrderRequest(string ProductName);
