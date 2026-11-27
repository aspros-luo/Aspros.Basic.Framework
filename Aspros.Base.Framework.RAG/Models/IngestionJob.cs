namespace Aspros.Base.Framework.RAG.Models;

public sealed class IngestionJob
{
    private IngestionJob() { }

    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public Guid KnowledgeBaseId { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid DocumentVersionId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public IngestionJobType JobType { get; private set; }
    public IngestionJobStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; }
    public string? LeaseOwner { get; private set; }
    public Guid LeaseToken { get; private set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public static IngestionJob Create(
        string tenantId,
        Guid knowledgeBaseId,
        Guid documentId,
        Guid documentVersionId,
        string idempotencyKey,
        IngestionJobType jobType = IngestionJobType.IngestDocument,
        int maxAttempts = 5,
        DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        if (knowledgeBaseId == Guid.Empty || documentId == Guid.Empty || documentVersionId == Guid.Empty)
        {
            throw new ArgumentException("Knowledge base, document, and document version IDs are required.");
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;
        return new IngestionJob
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Trim(),
            KnowledgeBaseId = knowledgeBaseId,
            DocumentId = documentId,
            DocumentVersionId = documentVersionId,
            IdempotencyKey = idempotencyKey.Trim(),
            JobType = jobType,
            Status = IngestionJobStatus.Pending,
            MaxAttempts = maxAttempts,
            LeaseToken = Guid.Empty,
            NextAttemptAtUtc = timestamp,
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp
        };
    }
}
