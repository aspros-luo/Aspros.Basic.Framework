using Aspros.Base.Framework.RAG.Abstractions;

namespace Aspros.Base.Framework.RAG.Hosting;

/// <summary>
/// Handles one leased ingestion job. Implementations should load canonical job metadata by
/// lease.JobId from the application-owned relational DbContext and should be idempotent because
/// a job can be delivered again after a worker crashes or loses its lease.
/// </summary>
public interface IIngestionJobHandler
{
    Task HandleAsync(
        IngestionJobLease lease,
        CancellationToken cancellationToken = default);
}
