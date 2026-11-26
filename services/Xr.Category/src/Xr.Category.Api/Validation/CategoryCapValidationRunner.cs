using Aspros.Base.Framework.Infrastructure;
using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xr.Category.Api.Messaging;
using Xr.Category.Infrastructure;
using CategoryEntity = Xr.Category.Domain.Category;

namespace Xr.Category.Api.Validation;

public sealed class CategoryCapValidationRunner(
    IServiceScopeFactory scopeFactory,
    CategoryCapValidationState state)
{
    private readonly IServiceScopeFactory _scopeFactory =
        scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

    private readonly CategoryCapValidationState _state =
        state ?? throw new ArgumentNullException(nameof(state));

    public async Task<CategoryCapValidationStartResponse> StartAsync(
        CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid().ToString("N");
        var run = new CategoryCapValidationRun(
            runId,
            $"CAP-COMMIT-{runId}",
            $"CAP-ROLLBACK-{runId}",
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"));

        _state.RegisterRun(run);

        await CommitBusinessChangeAndMessageAsync(
            run.CommittedCategoryCode,
            runId,
            new CategoryCapValidationMessage(
                run.RetryEventId,
                runId,
                "retry",
                FailFirstDelivery: true),
            cancellationToken);

        await RollbackBusinessChangeAndMessageAsync(
            run.RolledBackCategoryCode,
            runId,
            new CategoryCapValidationMessage(
                run.RolledBackEventId,
                runId,
                "rollback",
                FailFirstDelivery: false),
            cancellationToken);

        await PublishDuplicateMessagesAsync(
            new CategoryCapValidationMessage(
                run.DuplicateEventId,
                runId,
                "duplicate",
                FailFirstDelivery: false),
            cancellationToken);

        return new CategoryCapValidationStartResponse(
            run.RunId,
            run.CommittedCategoryCode,
            run.RolledBackCategoryCode,
            run.RetryEventId,
            run.DuplicateEventId,
            run.RolledBackEventId);
    }

    public async Task<CategoryCapValidationStatusResponse?> GetStatusAsync(
        string runId,
        CancellationToken cancellationToken)
    {
        var run = _state.FindRun(runId);
        if (run is null)
        {
            return null;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CategoryDbContext>();

        var committedBusinessRows = await dbContext.Categories
            .AsNoTracking()
            .CountAsync(
                category => category.Code == run.CommittedCategoryCode,
                cancellationToken);

        var rolledBackBusinessRows = await dbContext.Categories
            .AsNoTracking()
            .CountAsync(
                category => category.Code == run.RolledBackCategoryCode,
                cancellationToken);

        var receiptEventIds = await dbContext.CapValidationReceipts
            .AsNoTracking()
            .Where(receipt => receipt.RunId == run.RunId)
            .Select(receipt => receipt.EventId)
            .ToListAsync(cancellationToken);

        var retryAttempts = _state.GetDeliveryAttempts(run.RetryEventId);
        var duplicateAttempts = _state.GetDeliveryAttempts(run.DuplicateEventId);
        var rollbackAttempts = _state.GetDeliveryAttempts(run.RolledBackEventId);

        var retryReceiptCount = receiptEventIds.Count(
            eventId => eventId == run.RetryEventId);
        var duplicateReceiptCount = receiptEventIds.Count(
            eventId => eventId == run.DuplicateEventId);
        var rollbackReceiptCount = receiptEventIds.Count(
            eventId => eventId == run.RolledBackEventId);

        var businessCommitPassed = committedBusinessRows == 1;
        var businessRollbackPassed = rolledBackBusinessRows == 0;
        var retryPassed = retryAttempts >= 2 && retryReceiptCount == 1;
        var duplicatePassed = duplicateAttempts >= 2 && duplicateReceiptCount == 1;
        var outboxRollbackPassed =
            rollbackAttempts == 0 && rollbackReceiptCount == 0;

        var completed = retryAttempts >= 2 && duplicateAttempts >= 2;

        var passed =
            completed &&
            businessCommitPassed &&
            businessRollbackPassed &&
            retryPassed &&
            duplicatePassed &&
            outboxRollbackPassed;

        return new CategoryCapValidationStatusResponse(
            completed,
            passed,
            run.RunId,
            committedBusinessRows,
            rolledBackBusinessRows,
            retryAttempts,
            retryReceiptCount,
            duplicateAttempts,
            duplicateReceiptCount,
            rollbackAttempts,
            rollbackReceiptCount);
    }

    private async Task CommitBusinessChangeAndMessageAsync(
        string categoryCode,
        string runId,
        CategoryCapValidationMessage message,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CategoryDbContext>();
        var unitOfWork = scope.ServiceProvider
            .GetRequiredService<IUnitOfWork>();
        var capPublisher = scope.ServiceProvider
            .GetRequiredService<ICapPublisher>();

        // CAP wraps the same EF Core database transaction and buffers messages
        // until the database commit succeeds.
        await using var transaction = dbContext.Database.BeginTransaction(
            capPublisher,
            autoCommit: false);

        unitOfWork.BeginTransaction(transaction);

        try
        {
            await unitOfWork.RegisterNew(new CategoryEntity(
                categoryCode,
                $"CAP committed category {runId}",
                parentId: null,
                sortOrder: 10,
                isEnabled: true));

            await capPublisher.PublishAsync(
                CategoryCapValidationTopics.Name,
                message,
                cancellationToken: cancellationToken);

            // UoW flushes business data; CAP owns the final commit and dispatch.
            await unitOfWork.CommitAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // Preserve the original business or persistence exception.
            }

            throw;
        }
    }

    private async Task RollbackBusinessChangeAndMessageAsync(
        string categoryCode,
        string runId,
        CategoryCapValidationMessage message,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CategoryDbContext>();
        var unitOfWork = scope.ServiceProvider
            .GetRequiredService<IUnitOfWork>();
        var capPublisher = scope.ServiceProvider
            .GetRequiredService<ICapPublisher>();

        await using var transaction = dbContext.Database.BeginTransaction(
            capPublisher,
            autoCommit: false);

        unitOfWork.BeginTransaction(transaction);

        try
        {
            await unitOfWork.RegisterNew(new CategoryEntity(
                categoryCode,
                $"CAP rolled back category {runId}",
                parentId: null,
                sortOrder: 20,
                isEnabled: true));

            await capPublisher.PublishAsync(
                CategoryCapValidationTopics.Name,
                message,
                cancellationToken: cancellationToken);

            // CommitAsync only flushes because this is a caller-owned CAP transaction.
            await unitOfWork.CommitAsync(cancellationToken);

            // Roll back both the category row and CAP's local published-message row.
            await transaction.RollbackAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // Preserve the original business or persistence exception.
            }

            throw;
        }
    }

    private async Task PublishDuplicateMessagesAsync(
        CategoryCapValidationMessage message,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CategoryDbContext>();
        var capPublisher = scope.ServiceProvider
            .GetRequiredService<ICapPublisher>();

        await using var transaction = dbContext.Database.BeginTransaction(
            capPublisher,
            autoCommit: false);

        await capPublisher.PublishAsync(
            CategoryCapValidationTopics.Name,
            message,
            cancellationToken: cancellationToken);

        // Two independent CAP messages deliberately carry the same business EventId.
        await capPublisher.PublishAsync(
            CategoryCapValidationTopics.Name,
            message,
            cancellationToken: cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}

public sealed record CategoryCapValidationStartResponse(
    string RunId,
    string CommittedCategoryCode,
    string RolledBackCategoryCode,
    string RetryEventId,
    string DuplicateEventId,
    string RolledBackEventId);

public sealed record CategoryCapValidationStatusResponse(
    bool Completed,
    bool Passed,
    string RunId,
    int CommittedBusinessRows,
    int RolledBackBusinessRows,
    int RetryDeliveryAttempts,
    int RetryReceiptRows,
    int DuplicateDeliveryAttempts,
    int DuplicateReceiptRows,
    int RolledBackMessageDeliveryAttempts,
    int RolledBackMessageReceiptRows);
