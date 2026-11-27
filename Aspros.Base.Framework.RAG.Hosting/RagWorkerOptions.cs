namespace Aspros.Base.Framework.RAG.Hosting;

/// <summary>
/// Controls the optional background ingestion worker.
/// </summary>
public sealed class RagWorkerOptions
{
    /// <summary>
    /// Stable identifier for this worker process. A unique value is generated when omitted.
    /// </summary>
    public string? WorkerId { get; set; }

    public int MaxConcurrency { get; set; } = 2;

    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(2);

    public TimeSpan RenewalInterval { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);

    internal void Validate()
    {
        if (MaxConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxConcurrency));
        }

        if (LeaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(LeaseDuration));
        }

        if (RenewalInterval <= TimeSpan.Zero || RenewalInterval >= LeaseDuration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RenewalInterval),
                "Renewal interval must be greater than zero and shorter than the lease duration.");
        }

        if (PollingInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(PollingInterval));
        }

        if (RetryDelay < TimeSpan.Zero || MaxRetryDelay < RetryDelay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RetryDelay),
                "Retry delays must be non-negative and the maximum must not be shorter than the base delay.");
        }

        if (!string.IsNullOrWhiteSpace(WorkerId) && WorkerId.Length > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(WorkerId));
        }
    }
}
