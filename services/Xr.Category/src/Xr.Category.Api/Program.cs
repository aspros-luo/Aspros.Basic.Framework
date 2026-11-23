using Aspros.Base.Framework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CategoryEntity = Xr.Category.Domain.Category;
using Xr.Category.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Register the DbContext before scanning its assembly.
// AddAsprosDbContext maps IDbContext to the same scoped CategoryDbContext instance
// used by repositories and the UnitOfWork.
builder.Services.AddCategoryInfrastructure(builder.Configuration);

builder.Services.AddAsprosFramework(
    typeof(Xr.Category.Application.AssemblyMarker).Assembly,
    typeof(Xr.Category.Infrastructure.AssemblyMarker).Assembly);

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

if (app.Environment.IsDevelopment())
{
    app.MapPost("/_dev/validation/unit-of-work", async (
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken) =>
    {
        var runId = Guid.NewGuid().ToString("N");
        var commitCodes = new[]
        {
            $"UOW-COMMIT-{runId}-A",
            $"UOW-COMMIT-{runId}-B"
        };
        var rollbackCodes = new[]
        {
            $"UOW-ROLLBACK-{runId}-A",
            $"UOW-ROLLBACK-{runId}-B"
        };

        // Each transaction gets its own request-like DI scope. The UnitOfWork
        // and CategoryDbContext resolved inside that scope share one DbContext.
        await using (var commitScope = scopeFactory.CreateAsyncScope())
        {
            var unitOfWork = commitScope.ServiceProvider
                .GetRequiredService<IUnitOfWork>();

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            await unitOfWork.RegisterRangeNew(new[]
            {
                new CategoryEntity(
                    commitCodes[0], $"UoW commit A {runId}", null, 10, true),
                new CategoryEntity(
                    commitCodes[1], $"UoW commit B {runId}", null, 20, true)
            });
            await unitOfWork.CommitAsync(cancellationToken);
        }

        var rollbackFlushedRowCount = 0;

        await using (var rollbackScope = scopeFactory.CreateAsyncScope())
        {
            var unitOfWork = rollbackScope.ServiceProvider
                .GetRequiredService<IUnitOfWork>();

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            await unitOfWork.RegisterRangeNew(new[]
            {
                new CategoryEntity(
                    rollbackCodes[0], $"UoW rollback A {runId}", null, 10, true),
                new CategoryEntity(
                    rollbackCodes[1], $"UoW rollback B {runId}", null, 20, true)
            });

            // RegisterNew only stages entities. Flush SQL inside the open
            // transaction so RollbackAsync must undo actual database writes.
            rollbackFlushedRowCount =
                await unitOfWork.DbContext.SaveChangesAsync(cancellationToken);

            await unitOfWork.RollbackAsync(cancellationToken);
        }

        int committedRowCount;
        int rolledBackRowCount;

        // Verify with a fresh scope/DbContext, not the writer's ChangeTracker.
        await using (var verificationScope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = verificationScope.ServiceProvider
                .GetRequiredService<CategoryDbContext>();

            committedRowCount = await dbContext.Categories
                .AsNoTracking()
                .CountAsync(x => commitCodes.Contains(x.Code), cancellationToken);

            rolledBackRowCount = await dbContext.Categories
                .AsNoTracking()
                .CountAsync(x => rollbackCodes.Contains(x.Code), cancellationToken);
        }

        var commitPassed = committedRowCount == commitCodes.Length;
        var rollbackPassed =
            rollbackFlushedRowCount == rollbackCodes.Length &&
            rolledBackRowCount == 0;
        var passed = commitPassed && rollbackPassed;

        var result = new
        {
            passed,
            runId,
            commit = new
            {
                expectedRows = commitCodes.Length,
                actualRows = committedRowCount,
                codes = commitCodes,
                passed = commitPassed
            },
            rollback = new
            {
                expectedFlushedRows = rollbackCodes.Length,
                flushedRows = rollbackFlushedRowCount,
                expectedPersistedRowsAfterRollback = 0,
                actualPersistedRowsAfterRollback = rolledBackRowCount,
                codes = rollbackCodes,
                passed = rollbackPassed
            }
        };

        return Results.Json(
            result,
            statusCode: passed
                ? StatusCodes.Status200OK
                : StatusCodes.Status500InternalServerError);
    });
}

app.MapControllers();

app.Run();

public partial class Program
{
}
