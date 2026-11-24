namespace Xr.Category.Api.Messaging;

public static class CategoryBehaviorEventTopics
{
    public const string Name = "xr.category.behavior-event";

    public const string Group = "xr-category-clickhouse-writer";
}

public sealed record CategoryBehaviorEventMessage(
    string EventId,
    string RunId,
    string UserId,
    ulong CategoryId,
    string EventType,
    DateTime EventTimeUtc,
    bool FailFirstDelivery);

public sealed record CategoryBehaviorEventRun(
    string RunId,
    string UserId,
    ulong CategoryId,
    string RetryEventId,
    string DuplicateEventId);
