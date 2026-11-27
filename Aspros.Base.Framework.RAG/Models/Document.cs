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

    public void SetCurrentVersion(int versionNumber, string contentHash, DateTimeOffset? now = null)
    {
        if (versionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(versionNumber));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        if (Status == DocumentStatus.Deleted)
        {
            throw new InvalidOperationException("A deleted document cannot be updated.");
        }

        CurrentVersionNumber = versionNumber;
        ContentHash = contentHash.Trim();
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
        DeletedAtUtc = timestamp;
        UpdatedAtUtc = timestamp;
    }
}
