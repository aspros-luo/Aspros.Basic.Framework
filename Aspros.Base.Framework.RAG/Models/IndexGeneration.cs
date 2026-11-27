namespace Aspros.Base.Framework.RAG.Models;

/// <summary>
/// One immutable indexing generation for a document version and embedding profile.
/// A new generation is built and validated before the host switches its active pointer.
/// </summary>
public sealed class IndexGeneration
{
    private IndexGeneration() { }

    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public Guid KnowledgeBaseId { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid DocumentVersionId { get; private set; }
    public Guid IngestionJobId { get; private set; }
    public IngestionJob IngestionJob { get; private set; } = null!;
    public string EmbeddingModelKey { get; private set; } = string.Empty;
    public int EmbeddingDimension { get; private set; }
    public string ChunkingProfileVersion { get; private set; } = string.Empty;
    public IndexGenerationStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ActivatedAtUtc { get; private set; }
    public DateTimeOffset? RetiredAtUtc { get; private set; }

    public static IndexGeneration Create(
        IngestionJob ingestionJob,
        string embeddingModelKey,
        int embeddingDimension,
        string chunkingProfileVersion,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(ingestionJob);
        ArgumentException.ThrowIfNullOrWhiteSpace(embeddingModelKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(chunkingProfileVersion);

        if (embeddingDimension < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(embeddingDimension));
        }

        return new IndexGeneration
        {
            Id = Guid.NewGuid(),
            TenantId = ingestionJob.TenantId,
            KnowledgeBaseId = ingestionJob.KnowledgeBaseId,
            DocumentId = ingestionJob.DocumentId,
            DocumentVersionId = ingestionJob.DocumentVersionId,
            IngestionJobId = ingestionJob.Id,
            IngestionJob = ingestionJob,
            EmbeddingModelKey = embeddingModelKey.Trim(),
            EmbeddingDimension = embeddingDimension,
            ChunkingProfileVersion = chunkingProfileVersion.Trim(),
            Status = IndexGenerationStatus.Building,
            CreatedAtUtc = now ?? DateTimeOffset.UtcNow
        };
    }

    public void Activate(DateTimeOffset? now = null)
    {
        if (Status != IndexGenerationStatus.Building)
        {
            throw new InvalidOperationException("Only a building index generation can be activated.");
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;
        Status = IndexGenerationStatus.Active;
        ActivatedAtUtc = timestamp;
    }

    public void Retire(DateTimeOffset? now = null)
    {
        if (Status != IndexGenerationStatus.Active)
        {
            throw new InvalidOperationException("Only an active index generation can be retired.");
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;
        Status = IndexGenerationStatus.Retired;
        RetiredAtUtc = timestamp;
    }

    public void MarkFailed()
    {
        if (Status == IndexGenerationStatus.Active)
        {
            throw new InvalidOperationException("An active index generation cannot be marked failed.");
        }

        Status = IndexGenerationStatus.Failed;
    }
}
