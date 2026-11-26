using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;
using Xr.Category.Infrastructure;

namespace Xr.Category.Api.Messaging;

public sealed class CategoryCapValidationSubscriber(
    CategoryDbContext dbContext,
    CategoryCapValidationState state,
    ILogger<CategoryCapValidationSubscriber> logger) : ICapSubscribe
{
    private readonly CategoryDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private readonly CategoryCapValidationState _state =
        state ?? throw new ArgumentNullException(nameof(state));

    private readonly ILogger<CategoryCapValidationSubscriber> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    [CapSubscribe(
        CategoryCapValidationTopics.Name,
        Group = CategoryCapValidationTopics.Group)]
    public async Task HandleAsync(
        CategoryCapValidationMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var deliveryAttempt = _state.RecordDeliveryAttempt(message.EventId);

        // Deliberately fail the first delivery for the retry scenario.
        // CAP should redeliver the message; no business receipt is written yet.
        if (message.FailFirstDelivery && deliveryAttempt == 1)
        {
            _logger.LogWarning(
                "Intentionally failing the first CAP delivery for event {EventId}.",
                message.EventId);

            throw new InvalidOperationException(
                "Intentional first-delivery failure for CAP retry validation.");
        }

        // One INSERT IGNORE statement is an atomic idempotent effect in MySQL.
        // The unique event_id key ensures duplicate deliveries create one receipt.
        var insertedRows = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT IGNORE INTO category_cap_validation_receipt
                 (event_id, run_id, scenario, processed_at)
             VALUES
                 ({message.EventId}, {message.RunId}, {message.Scenario}, {DateTime.UtcNow})
             """,
            cancellationToken);

        _logger.LogInformation(
            "CAP validation message {EventId} handled on delivery {DeliveryAttempt}. " +
            "Idempotent effect applied: {EffectApplied}.",
            message.EventId,
            deliveryAttempt,
            insertedRows == 1);
    }
}
