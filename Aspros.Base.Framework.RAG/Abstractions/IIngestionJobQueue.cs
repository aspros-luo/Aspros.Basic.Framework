namespace Aspros.Base.Framework.RAG.Abstractions;

public interface IIngestionJobQueue
{
    Task<IngestionJobLease?> TryAcquireNextAsync(
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task<bool> RenewAsync(
        IngestionJobLease lease,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task<bool> CompleteAsync(
        IngestionJobLease lease,
        CancellationToken cancellationToken = default);

    Task<bool> FailAsync(
        IngestionJobLease lease,
        string error,
        DateTimeOffset retryAtUtc,
        bool terminal = false,
        CancellationToken cancellationToken = default);
}
