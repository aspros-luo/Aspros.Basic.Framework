namespace Aspros.Base.Framework.RAG.Abstractions;

/// <summary>
/// Proof of ownership for one leased ingestion job. The token changes on every claim,
/// so a worker holding an expired lease cannot complete a job claimed by another worker.
/// </summary>
public sealed record IngestionJobLease(
    Guid JobId,
    Guid LeaseToken,
    string WorkerId,
    int AttemptCount,
    DateTimeOffset LeaseExpiresAtUtc);
