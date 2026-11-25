using ClickHouse.Driver;
using DotNetCore.CAP;

namespace Xr.Category.Api.Messaging;

/// <summary>
/// Writes accepted Category behavior events to the analytical ClickHouse store.
/// CAP delivery is at-least-once; analytics queries deduplicate by EventId.
/// </summary>
public sealed class CategoryBehaviorRecordedSubscriber(
    IClickHouseClient clickHouse,
    ILogger<CategoryBehaviorRecordedSubscriber> logger) : ICapSubscribe
{
    private const string EventsTable =
        "aspros_analytics.category_behavior_events";

    private readonly IClickHouseClient _clickHouse =
        clickHouse ?? throw new ArgumentNullException(nameof(clickHouse));

    private readonly ILogger<CategoryBehaviorRecordedSubscriber> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    [CapSubscribe(
        CategoryBehaviorRecordedTopics.Name,
        Group = CategoryBehaviorRecordedTopics.Group)]
    public async Task HandleAsync(
        CategoryBehaviorRecordedMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var insertedRows = await _clickHouse.InsertBinaryAsync(
            EventsTable,
            ["event_id", "run_id", "user_id", "category_id", "event_type", "event_time"],
            new[]
            {
                new object[]
                {
                    message.EventId,
                    message.CorrelationId,
                    message.UserId,
                    message.CategoryId,
                    message.EventType,
                    message.EventTimeUtc
                }
            },
            cancellationToken: cancellationToken);

        if (insertedRows != 1)
        {
            throw new InvalidOperationException(
                $"Expected one ClickHouse row for event {message.EventId}, but inserted {insertedRows}.");
        }

        _logger.LogInformation(
            "Category behavior event {EventId} for category {CategoryId} written to ClickHouse. CorrelationId: {CorrelationId}.",
            message.EventId,
            message.CategoryId,
            message.CorrelationId);
    }
}
