using Aspros.Base.Framework.RAG.Abstractions;
using Aspros.Base.Framework.RAG.Hosting;
using Aspros.Base.Framework.RAG.Models;
using Microsoft.EntityFrameworkCore;

namespace Rag.KnowledgeBase.Api;

public sealed class DemoIngestionJobHandler(
    DemoRagDbContext dbContext,
    DemoDocumentStorage storage,
    ILogger<DemoIngestionJobHandler> logger) : IIngestionJobHandler
{
    private const int MaxChunkCharacters = 1200;

    public async Task HandleAsync(IngestionJobLease lease, CancellationToken cancellationToken = default)
    {
        var job = await dbContext.Set<IngestionJob>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == lease.JobId, cancellationToken)
            ?? throw new IngestionJobPermanentException("The ingestion job no longer exists.");

        if (job.JobType != IngestionJobType.IngestDocument)
        {
            throw new IngestionJobPermanentException($"Unsupported demo job type: {job.JobType}.");
        }

        var version = await dbContext.Set<DocumentVersion>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == job.DocumentVersionId, cancellationToken)
            ?? throw new IngestionJobPermanentException("The document version no longer exists.");

        var document = await dbContext.Set<Document>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == job.DocumentId, cancellationToken)
            ?? throw new IngestionJobPermanentException("The document no longer exists.");

        if (document.Status == DocumentStatus.Deleted)
        {
            throw new IngestionJobPermanentException("The document has been deleted.");
        }

        var text = await storage.ReadTextAsync(version.SourceUri, cancellationToken);
        var chunks = SplitIntoChunks(text);
        if (chunks.Count == 0)
        {
            throw new IngestionJobPermanentException("The document contains no extractable text.");
        }

        // Replacing all chunks for this version in one relational transaction makes retries
        // safe if a worker stops after writing data but before acknowledging its lease.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var existing = await dbContext.Set<DocumentChunk>()
            .Where(x => x.DocumentVersionId == version.Id)
            .ToListAsync(cancellationToken);
        dbContext.RemoveRange(existing);

        for (var index = 0; index < chunks.Count; index++)
        {
            var content = chunks[index];
            var contentHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));

            dbContext.Add(DocumentChunk.Create(
                job.TenantId,
                job.KnowledgeBaseId,
                job.DocumentId,
                version.Id,
                index,
                content,
                contentHash,
                sourceLocation: $"chunk:{index + 1}"));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Ingested document {DocumentId}, version {VersionNumber}: persisted {ChunkCount} text chunks. Embedding/indexing is not enabled in this demo phase.",
            document.Id,
            version.VersionNumber,
            chunks.Count);
    }

    private static List<string> SplitIntoChunks(string source)
    {
        var paragraphs = source
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var chunks = new List<string>();
        var current = new System.Text.StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            for (var offset = 0; offset < paragraph.Length; offset += MaxChunkCharacters)
            {
                var part = paragraph.Substring(offset, Math.Min(MaxChunkCharacters, paragraph.Length - offset));
                if (current.Length > 0 && current.Length + Environment.NewLine.Length + part.Length > MaxChunkCharacters)
                {
                    chunks.Add(current.ToString());
                    current.Clear();
                }

                if (current.Length > 0)
                {
                    current.AppendLine();
                }

                current.Append(part);
            }
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString());
        }

        return chunks;
    }
}
