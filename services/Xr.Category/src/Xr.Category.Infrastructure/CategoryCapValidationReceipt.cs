namespace Xr.Category.Infrastructure;

public sealed class CategoryCapValidationReceipt
{
    public string EventId { get; set; } = string.Empty;

    public string RunId { get; set; } = string.Empty;

    public string Scenario { get; set; } = string.Empty;

    public DateTime ProcessedAt { get; set; }
}
