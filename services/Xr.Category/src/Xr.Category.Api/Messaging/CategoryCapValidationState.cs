using System.Collections.Concurrent;

namespace Xr.Category.Api.Messaging;

/// <summary>
/// In-memory diagnostics for the Development-only CAP validation flow.
/// Business idempotency is still enforced by the MySQL receipt table.
/// </summary>
public sealed class CategoryCapValidationState
{
    private readonly ConcurrentDictionary<string, CategoryCapValidationRun> _runs =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, int> _deliveryAttempts =
        new(StringComparer.Ordinal);

    public void RegisterRun(CategoryCapValidationRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        _runs[run.RunId] = run;
    }

    public CategoryCapValidationRun? FindRun(string runId)
        => _runs.TryGetValue(runId, out var run) ? run : null;

    public int RecordDeliveryAttempt(string eventId)
        => _deliveryAttempts.AddOrUpdate(
            eventId,
            1,
            static (_, current) => current + 1);

    public int GetDeliveryAttempts(string eventId)
        => _deliveryAttempts.TryGetValue(eventId, out var attempts)
            ? attempts
            : 0;
}
