using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;
using Xr.Category.Application;
using Xr.Category.Infrastructure;

namespace Xr.Category.Api.Messaging;

/// <summary>
/// Publishes accepted Category behavior events through the MySQL-backed CAP outbox.
/// The event is accepted only while its target category exists, is active and enabled.
/// </summary>
public sealed class CategoryBehaviorTrackingService(
    CategoryDbContext dbContext,
    ICapPublisher capPublisher) : ICategoryBehaviorTrackingService
{
    private static readonly HashSet<string> SupportedEventTypes =
        new(StringComparer.Ordinal)
        {
            "view",
            "click",
            "purchase"
        };

    private readonly CategoryDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private readonly ICapPublisher _capPublisher =
        capPublisher ?? throw new ArgumentNullException(nameof(capPublisher));

    public async Task<CategoryBehaviorEventAccepted?> TrackAsync(
        long categoryId,
        TrackCategoryBehaviorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (categoryId <= 0)
        {
            return null;
        }

        if (request.EventId is null || request.EventId == Guid.Empty)
        {
            throw new ArgumentException(
                "A non-empty event ID is required.",
                nameof(request));
        }

        var userId = request.UserId?.Trim();
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException(
                "A user ID is required.",
                nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.EventType) ||
            !SupportedEventTypes.Contains(request.EventType))
        {
            throw new ArgumentException(
                "Supported event types are view, click and purchase.",
                nameof(request));
        }

        // Keep category validation and CAP outbox persistence in the same
        // database transaction. A rejected category never produces an event.
        await using var transaction = _dbContext.Database.BeginTransaction(
            _capPublisher,
            autoCommit: false);

        try
        {
            var isTrackable = await _dbContext.Categories
                .AsNoTracking()
                .AnyAsync(
                    category =>
                        category.Id == categoryId &&
                        !category.Deleted &&
                        category.IsEnabled,
                    cancellationToken);

            if (!isTrackable)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            var eventId = request.EventId.Value.ToString("N");
            var correlationId = Guid.NewGuid().ToString("N");
            var eventTimeUtc = DateTime.UtcNow;

            await _capPublisher.PublishAsync(
                CategoryBehaviorRecordedTopics.Name,
                new CategoryBehaviorRecordedMessage(
                    eventId,
                    correlationId,
                    userId,
                    checked((ulong)categoryId),
                    request.EventType,
                    eventTimeUtc),
                cancellationToken: cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new CategoryBehaviorEventAccepted(
                eventId,
                correlationId,
                categoryId,
                "accepted");
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // Preserve the original publishing or database exception.
            }

            throw;
        }
    }
}
