namespace Aspros.Base.Framework.RAG.Models;

public sealed class DocumentVersion
{
    private DocumentVersion() { }

    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public int VersionNumber { get; private set; }
    public string ContentHash { get; private set; } = string.Empty;
    public string MediaType { get; private set; } = string.Empty;
    public string SourceUri { get; private set; } = string.Empty;
    public string ChunkingProfileVersion { get; private set; } = string.Empty;
    public DocumentVersionStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static DocumentVersion Create(
        Guid documentId,
        int versionNumber,
        string contentHash,
        string mediaType,
        string sourceUri,
        string chunkingProfileVersion,
        DateTimeOffset? now = null)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException("Document ID is required.", nameof(documentId));
        }

        if (versionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(versionNumber));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(chunkingProfileVersion);

        return new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            VersionNumber = versionNumber,
            ContentHash = contentHash.Trim(),
            MediaType = mediaType.Trim(),
            SourceUri = sourceUri.Trim(),
            ChunkingProfileVersion = chunkingProfileVersion.Trim(),
            Status = DocumentVersionStatus.Ready,
            CreatedAtUtc = now ?? DateTimeOffset.UtcNow
        };
    }

    public void MarkSuperseded()
    {
        Status = DocumentVersionStatus.Superseded;
    }
}
