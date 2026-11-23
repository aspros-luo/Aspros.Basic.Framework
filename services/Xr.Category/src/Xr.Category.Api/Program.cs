using Aspros.Base.Framework.Infrastructure;
using Xr.Category.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAsprosFramework(
    typeof(Xr.Category.Application.AssemblyMarker).Assembly,
    typeof(Xr.Category.Infrastructure.AssemblyMarker).Assembly);

builder.Services.AddCategoryInfrastructure(builder.Configuration);
builder.Services.AddControllers();

var app = builder.Build();

app.MapGet("/health/live", () => Results.Ok(new
{
    service = "xr-category",
    status = "ok"
}));

app.MapGet("/health/ready", async (
    CategoryDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var canConnect = await dbContext.Database.CanConnectAsync(
            cancellationToken);

        if (!canConnect)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(new { service = "xr-category", database = "ready" });
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapControllers();

app.Run();

public partial class Program
{
}
