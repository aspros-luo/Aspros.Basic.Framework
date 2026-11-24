using Aspros.Base.Framework.Infrastructure;
using ClickHouse.Driver;
using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xr.Category.Api.Messaging;
using Xr.Category.Api.Analytics;
using Xr.Category.Api.Validation;
using CategoryEntity = Xr.Category.Domain.Category;
using Xr.Category.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Register the DbContext before scanning its assembly.
// AddAsprosDbContext maps IDbContext to the same scoped CategoryDbContext instance
// used by repositories and the UnitOfWork.
builder.Services.AddCategoryInfrastructure(builder.Configuration);

var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Redis must be configured.");

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "xr-category:";
});

var clickHouseConnectionString = builder.Configuration.GetConnectionString("ClickHouse")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:ClickHouse must be configured.");

builder.Services.AddSingleton(
    _ => new ClickHouseClient(clickHouseConnectionString));

builder.Services.AddAsprosFramework(
    typeof(Xr.Category.Application.AssemblyMarker).Assembly,
    typeof(Xr.Category.Infrastructure.AssemblyMarker).Assembly);

var rabbitMqConfiguration = builder.Configuration.GetSection("RabbitMQ");
var rabbitMqHost = rabbitMqConfiguration["HostName"]
    ?? throw new InvalidOperationException("RabbitMQ:HostName must be configured.");
var rabbitMqUser = rabbitMqConfiguration["UserName"]
    ?? throw new InvalidOperationException("RabbitMQ:UserName must be configured.");
var rabbitMqPassword = rabbitMqConfiguration["Password"]
    ?? throw new InvalidOperationException("RabbitMQ:Password must be configured.");
var rabbitMqVirtualHost = rabbitMqConfiguration["VirtualHost"] ?? "/";
var rabbitMqPort = rabbitMqConfiguration.GetValue<int>("Port", 5672);

builder.Services.AddSingleton<CategoryCapValidationState>();
builder.Services.AddTransient<CategoryCapValidationSubscriber>();
builder.Services.AddSingleton<CategoryCapValidationRunner>();
builder.Services.AddSingleton<CategoryBehaviorEventState>();
builder.Services.AddTransient<CategoryBehaviorEventSubscriber>();
builder.Services.AddCategoryPersonalizationValidation();

builder.Services.AddFrameworkCap<CategoryDbContext>(options =>
{
    options.UseRabbitMQ(rabbitMq =>
    {
        rabbitMq.HostName = rabbitMqHost;
        rabbitMq.Port = rabbitMqPort;
        rabbitMq.UserName = rabbitMqUser;
        rabbitMq.Password = rabbitMqPassword;
        rabbitMq.VirtualHost = rabbitMqVirtualHost;
        rabbitMq.PublishConfirms = true;
    });
});

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
    app.MapCategoryPersonalizationValidation();

    app.MapPost("/_dev/validation/clickhouse-cap", async (
        ICapPublisher capPublisher,
        CategoryBehaviorEventState state,
        CancellationToken cancellationToken) =>
    {
        var runId = Guid.NewGuid().ToString("N");
        var userId = "validation-user-1001";
        const ulong categoryId = 1;
        var eventTime = DateTime.UtcNow;
        var run = new CategoryBehaviorEventRun(
            runId,
            userId,
            categoryId,
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"));

        state.RegisterRun(run);

        // The retry event is first delivered unsuccessfully, then inserted after CAP retries.
        await capPublisher.PublishAsync(
            CategoryBehaviorEventTopics.Name,
            new CategoryBehaviorEventMessage(
                run.RetryEventId,
                runId,
                userId,
                categoryId,
                "view",
                eventTime,
                FailFirstDelivery: true),
            cancellationToken: cancellationToken);

        // Two independent CAP messages deliberately carry the same business EventId.
        await capPublisher.PublishAsync(
            CategoryBehaviorEventTopics.Name,
            new CategoryBehaviorEventMessage(
                run.DuplicateEventId,
                runId,
                userId,
                categoryId,
                "view",
                eventTime,
                FailFirstDelivery: false),
            cancellationToken: cancellationToken);

        await capPublisher.PublishAsync(
            CategoryBehaviorEventTopics.Name,
            new CategoryBehaviorEventMessage(
                run.DuplicateEventId,
                runId,
                userId,
                categoryId,
                "view",
                eventTime,
                FailFirstDelivery: false),
            cancellationToken: cancellationToken);

        return Results.Accepted(
            $"/_dev/validation/clickhouse-cap/{runId}",
            new
            {
                runId,
                userId,
                categoryId,
                retryEventId = run.RetryEventId,
                duplicateEventId = run.DuplicateEventId
            });
    });

    app.MapGet("/_dev/validation/clickhouse-cap/{runId}", async (
        string runId,
        ClickHouseClient clickHouse,
        CategoryBehaviorEventState state) =>
    {
        var run = state.FindRun(runId);
        if (run is null)
        {
            return Results.NotFound();
        }

        // Use the server-generated run ID from registered state, never raw route input in SQL.
        var registeredRunId = run.RunId;
        var physicalRows = Convert.ToInt64(await clickHouse.ExecuteScalarAsync(
            $"SELECT count() FROM aspros_analytics.category_behavior_events WHERE run_id = '{registeredRunId}'"));
        var logicalEventCount = Convert.ToInt64(await clickHouse.ExecuteScalarAsync(
            $"SELECT uniqExact(event_id) FROM aspros_analytics.category_behavior_events WHERE run_id = '{registeredRunId}'"));

        var retryAttempts = state.GetDeliveryAttempts(run.RetryEventId);
        var duplicateAttempts = state.GetDeliveryAttempts(run.DuplicateEventId);
        var retryWrites = state.GetSuccessfulWrites(run.RetryEventId);
        var duplicateWrites = state.GetSuccessfulWrites(run.DuplicateEventId);

        var completed =
            retryAttempts >= 2 &&
            duplicateAttempts >= 2 &&
            retryWrites >= 1 &&
            duplicateWrites >= 2;

        // MergeTree is append-only. Duplicate deliveries can create physical rows,
        // so analytical consumers must count distinct business event IDs.
        var passed =
            completed &&
            physicalRows == 3 &&
            logicalEventCount == 2;

        return Results.Json(new
        {
            completed,
            passed,
            runId,
            retryDeliveryAttempts = retryAttempts,
            retrySuccessfulWrites = retryWrites,
            duplicateDeliveryAttempts = duplicateAttempts,
            duplicateSuccessfulWrites = duplicateWrites,
            physicalRows,
            logicalEventCount,
            deduplication = "uniqExact(event_id)"
        });
    });

    app.MapPost("/_dev/validation/clickhouse", async (ClickHouseClient clickHouse) =>
    {
        var runId = Guid.NewGuid().ToString("N");
        var eventId = Guid.NewGuid().ToString("N");
        var eventTime = DateTime.UtcNow;

        var insertedRows = await clickHouse.InsertBinaryAsync(
            "aspros_analytics.category_behavior_events",
            ["event_id", "run_id", "user_id", "category_id", "event_type", "event_time"],
            new[]
            {
                new object[]
                {
                    eventId,
                    runId,
                    "validation-user-1001",
                    1UL,
                    "view",
                    eventTime
                }
            });

        // runId is generated by this endpoint, not supplied by the caller.
        var observedRows = Convert.ToInt64(await clickHouse.ExecuteScalarAsync(
            $"SELECT count() FROM aspros_analytics.category_behavior_events WHERE run_id = '{runId}'"));
        var version = Convert.ToString(
            await clickHouse.ExecuteScalarAsync("SELECT version()"));

        var passed = insertedRows == 1 && observedRows == 1;

        return Results.Json(new
        {
            passed,
            runId,
            eventId,
            clickHouseVersion = version,
            insertedRows,
            observedRows
        }, statusCode: passed
            ? StatusCodes.Status200OK
            : StatusCodes.Status500InternalServerError);
    });

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

    app.MapPost("/_dev/validation/cap", async (
        CategoryCapValidationRunner runner,
        CancellationToken cancellationToken) =>
    {
        var result = await runner.StartAsync(cancellationToken);

        return Results.Accepted(
            $"/_dev/validation/cap/{result.RunId}",
            result);
    });

    app.MapGet("/_dev/validation/cap/{runId}", async (
        string runId,
        CategoryCapValidationRunner runner,
        CancellationToken cancellationToken) =>
    {
        var result = await runner.GetStatusAsync(
            runId,
            cancellationToken);

        IResult response = result is null
            ? Results.NotFound()
            : Results.Ok(result);

        return response;
    });
}

app.MapControllers();

app.Run();

public partial class Program
{
}
