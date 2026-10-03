using Aspros.Base.Framework.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Validation.WebApi.Events;
using Validation.WebApi.Features;
using Validation.WebApi.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=validation.db"));

builder.Services.AddScoped<Microsoft.EntityFrameworkCore.DbContext>(provider =>
    provider.GetRequiredService<AppDbContext>());

builder.Services.AutoInject();
builder.Services.AddSingleton<Aspros.Base.Framework.Application.Abstractions.Messaging.IIntegrationEventPublisher, ValidationIntegrationEventPublisher>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.MapPost("/orders", async (
    CreateOrderRequest? request,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    if (request is null || string.IsNullOrWhiteSpace(request.ProductName))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["productName"] = ["ProductName is required."]
        });
    }

    var orderId = await sender.Send(
        new CreateOrderCommand(request.ProductName),
        cancellationToken);

    return Results.Created($"/orders/{orderId}", new { orderId });
});

app.MapPost("/orders/{id:guid}/confirm", async (
    Guid id,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var confirmed = await sender.Send(
        new ConfirmOrderCommand(id),
        cancellationToken);

    return confirmed
        ? Results.Ok(new { orderId = id, confirmed = true })
        : Results.NotFound();
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

app.MapPost("/validation/fail-next-integration-event", (
    ValidationIntegrationEventPublisher publisher) =>
{
    publisher.FailNextPublish = true;
    return Results.Ok(new { failNextPublish = true });
});

app.MapGet("/validation/integration-events", (
    ValidationIntegrationEventPublisher publisher) =>
    Results.Ok(publisher.PublishedEvents));

app.Run();

public sealed record CreateOrderRequest(string ProductName);
