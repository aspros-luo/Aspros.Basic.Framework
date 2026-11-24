using ClickHouse.Driver;
using DotNetCore.CAP;

namespace Xr.Category.Api.Messaging;

public sealed class CategoryBehaviorEventSubscriber(
    ClickHouseClient clickHouse,
    CategoryBehaviorEventState state,
    ILogger<CategoryBehaviorEventSubscriber> logger) : ICapSubscribe
{
    private readonly ClickHouseClient _clickHouse =
        clickHouse ?? throw new ArgumentNullException(nameof(clickHouse));

    private readonly CategoryBehaviorEventState _state =
        state ?? throw new ArgumentNullException(nameof(state));

    private readonly ILogger<CategoryBehaviorEventSubscriber> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    [CapSubscribe(
        CategoryBehaviorEventTopics.Name,
        Group = CategoryBehaviorEventTopics.Group)]
    public async Task HandleAsync(
        CategoryBehaviorEventMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var deliveryAttempt = _state.RecordDeliveryAttempt(message.EventId);

        // Deliberately fail the first delivery to verify CAP retry behavior.
        if (message.FailFirstDelivery && deliveryAttempt == 1)
        {
            _logger.LogWarning(
                "Intentionally failing the first behavior-event delivery for {EventId}.",
                message.EventId);

            throw new InvalidOperationException(
                "Intentional first-delivery failure for ClickHouse CAP validation.");
        }

        await _clickHouse.InsertBinaryAsync(
            "aspros_analytics.category_behavior_events",
            ["event_id", "run_id", "user_id", "category_id", "event_type", "event_time"],
            new[]
            {
                new object[]
                {
                    message.EventId,
                    message.RunId,
                    message.UserId,
                    message.CategoryId,
                    message.EventType,
                    message.EventTimeUtc
                }
            });

        _state.RecordSuccessfulWrite(message.EventId);

        _logger.LogInformation(
            "Behavior event {EventId} inserted into ClickHouse on delivery {DeliveryAttempt}.",
            message.EventId,
            deliveryAttempt);
    }
}
