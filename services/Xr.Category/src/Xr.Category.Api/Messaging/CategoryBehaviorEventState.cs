using System.Collections.Concurrent;

namespace Xr.Category.Api.Messaging;

/// <summary>
/// In-memory diagnostics for the Development-only ClickHouse CAP validation.
/// ClickHouse remains the source of truth for persisted event rows.
/// </summary>
public sealed class CategoryBehaviorEventState
{
    private readonly ConcurrentDictionary<string, CategoryBehaviorEventRun> _runs =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, int> _deliveryAttempts =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, int> _successfulWrites =
        new(StringComparer.Ordinal);

    public void RegisterRun(CategoryBehaviorEventRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        _runs[run.RunId] = run;
    }

    public CategoryBehaviorEventRun? FindRun(string runId)
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

    public void RecordSuccessfulWrite(string eventId)
        => _successfulWrites.AddOrUpdate(
            eventId,
            1,
            static (_, current) => current + 1);

    public int GetSuccessfulWrites(string eventId)
        => _successfulWrites.TryGetValue(eventId, out var writes)
            ? writes
            : 0;
}
