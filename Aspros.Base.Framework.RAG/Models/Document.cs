namespace Aspros.Base.Framework.RAG.Models;

/// <summary>
/// Relational source-of-truth metadata for an ingested document.
/// Binary content remains in object or controlled file storage.
/// </summary>
public sealed class Document
{
    private Document() { }

    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public Guid KnowledgeBaseId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string SourceUri { get; private set; } = string.Empty;
    public string ContentHash { get; private set; } = string.Empty;
    public int CurrentVersionNumber { get; private set; }
    public Guid? CurrentVersionId { get; private set; }
    public Guid? ActiveIndexGenerationId { get; private set; }
    public DocumentStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public static Document Create(
        string tenantId,
        Guid knowledgeBaseId,
        string title,
        string sourceUri,
        string contentHash,
        DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        var timestamp = now ?? DateTimeOffset.UtcNow;
        return new Document
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Trim(),
            KnowledgeBaseId = knowledgeBaseId,
            Title = title.Trim(),
            SourceUri = sourceUri.Trim(),
            ContentHash = contentHash.Trim(),
            Status = DocumentStatus.Active,
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp
        };
    }

    public void SetCurrentVersion(DocumentVersion version, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(version);

        if (version.DocumentId != Id)
        {
            throw new InvalidOperationException("The document version belongs to another document.");
        }

        if (Status == DocumentStatus.Deleted)
        {
            throw new InvalidOperationException("A deleted document cannot be updated.");
        }

        if (version.Status != DocumentVersionStatus.Ready)
        {
            throw new InvalidOperationException("Only a ready document version can become current.");
        }

        CurrentVersionId = version.Id;
        CurrentVersionNumber = version.VersionNumber;
        ContentHash = version.ContentHash;
        // The previous generation belongs to the previous version. The caller must
        // publish the version and its validated generation in one relational transaction.
        ActiveIndexGenerationId = null;
        UpdatedAtUtc = now ?? DateTimeOffset.UtcNow;
    }

    public void SetActiveIndexGeneration(IndexGeneration generation, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(generation);

        if (generation.DocumentId != Id)
        {
            throw new InvalidOperationException("The index generation belongs to another document.");
        }

        if (generation.Status != IndexGenerationStatus.Active)
        {
            throw new InvalidOperationException("Only an active index generation can be selected.");
        }

        if (CurrentVersionId is null || generation.DocumentVersionId != CurrentVersionId.Value)
        {
            throw new InvalidOperationException("The index generation does not belong to the current document version.");
        }

        if (generation.TenantId != TenantId || generation.KnowledgeBaseId != KnowledgeBaseId)
        {
            throw new InvalidOperationException("The index generation belongs to another tenant or knowledge base.");
        }

        if (Status == DocumentStatus.Deleted)
        {
            throw new InvalidOperationException("A deleted document cannot activate an index generation.");
        }

        ActiveIndexGenerationId = generation.Id;
        UpdatedAtUtc = now ?? DateTimeOffset.UtcNow;
    }

    public void MarkDeleted(DateTimeOffset? now = null)
    {
        if (Status == DocumentStatus.Deleted)
        {
            return;
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;
        Status = DocumentStatus.Deleted;
        ActiveIndexGenerationId = null;
        DeletedAtUtc = timestamp;
        UpdatedAtUtc = timestamp;
    }
}
