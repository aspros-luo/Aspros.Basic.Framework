namespace Xr.Category.Api.Messaging;

public static class CategoryCapValidationTopics
{
    public const string Name = "xr.category.validation";

    public const string Group = "xr-category-validation";
}

public sealed record CategoryCapValidationMessage(
    string EventId,
    string RunId,
    string Scenario,
    bool FailFirstDelivery);

public sealed record CategoryCapValidationRun(
    string RunId,
    string CommittedCategoryCode,
    string RolledBackCategoryCode,
    string RetryEventId,
    string DuplicateEventId,
    string RolledBackEventId);
