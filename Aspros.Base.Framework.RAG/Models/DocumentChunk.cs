namespace Aspros.Base.Framework.RAG.Models;

/// <summary>
/// Canonical chunk text and source position. Vector stores only hold derived embeddings
/// and identifiers; they are never the only copy of this content.
/// </summary>
public sealed class DocumentChunk
{
    private DocumentChunk() { }

    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public Guid KnowledgeBaseId { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid DocumentVersionId { get; private set; }
    public int ChunkIndex { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string ContentHash { get; private set; } = string.Empty;
    public int? PageNumber { get; private set; }
    public string? SectionTitle { get; private set; }
    public string? SourceLocation { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static DocumentChunk Create(
        string tenantId,
        Guid knowledgeBaseId,
        Guid documentId,
        Guid documentVersionId,
        int chunkIndex,
        string content,
        string contentHash,
        int? pageNumber = null,
        string? sectionTitle = null,
        string? sourceLocation = null,
        DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        if (chunkIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkIndex));
        }

        if (pageNumber is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        return new DocumentChunk
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Trim(),
            KnowledgeBaseId = knowledgeBaseId,
            DocumentId = documentId,
            DocumentVersionId = documentVersionId,
            ChunkIndex = chunkIndex,
            Content = content,
            ContentHash = contentHash.Trim(),
            PageNumber = pageNumber,
            SectionTitle = sectionTitle,
            SourceLocation = sourceLocation,
            CreatedAtUtc = now ?? DateTimeOffset.UtcNow
        };
    }
}
