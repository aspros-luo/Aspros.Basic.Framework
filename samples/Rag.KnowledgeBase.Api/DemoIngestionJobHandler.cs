using Aspros.Base.Framework.RAG.Abstractions;
using Aspros.Base.Framework.RAG.Hosting;
using Aspros.Base.Framework.RAG.Models;
using Aspros.Base.Framework.RAG.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Rag.KnowledgeBase.Api;

public sealed class DemoIngestionJobHandler(
    DemoRagDbContext dbContext,
    DemoDocumentStorage storage,
    DemoAiClient aiClient,
    DemoVectorStore vectorStore,
    EfCoreIndexGenerationActivator<DemoRagDbContext> generationActivator,
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

        // The relational database remains canonical. Vector points contain only identifiers
        // and embeddings; retrieval always resolves chunk text and active-generation state in SQL.
        var persistedChunks = await dbContext.Set<DocumentChunk>()
            .AsNoTracking()
            .Where(x => x.DocumentVersionId == version.Id)
            .OrderBy(x => x.ChunkIndex)
            .ToListAsync(cancellationToken);

        var vectors = await aiClient.GenerateEmbeddingsAsync(
            persistedChunks.Select(x => x.Content).ToArray(),
            cancellationToken);
        var dimension = vectors[0].Length;

        var generation = await dbContext.Set<IndexGeneration>()
            .SingleOrDefaultAsync(x => x.IngestionJobId == job.Id, cancellationToken);
        if (generation is null)
        {
            generation = IndexGeneration.Create(
                job,
                aiClient.EmbeddingModelKey,
                dimension,
                version.ChunkingProfileVersion);
            // The job was deliberately loaded without tracking. Attach it as an existing
            // principal before adding the generation, otherwise EF would try to INSERT it again.
            dbContext.Attach(job);
            dbContext.Add(generation);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (generation.EmbeddingModelKey != aiClient.EmbeddingModelKey ||
                 generation.EmbeddingDimension != dimension)
        {
            throw new IngestionJobPermanentException(
                "The embedding model changed while this ingestion job was being retried. Create a new ingestion job to rebuild the index.");
        }

        await vectorStore.UpsertAsync(
            job.TenantId,
            job.KnowledgeBaseId,
            job.DocumentId,
            version.Id,
            generation.Id,
            persistedChunks.Select(x => x.Id).ToArray(),
            vectors,
            cancellationToken);

        var activated = await generationActivator.ActivateAsync(
            document.Id,
            generation.Id,
            cancellationToken);
        if (!activated)
        {
            throw new InvalidOperationException(
                "The index generation could not be activated because the document changed concurrently. The worker can retry safely.");
        }

        logger.LogInformation(
            "Indexed document {DocumentId}, version {VersionNumber}: persisted {ChunkCount} chunks and activated generation {GenerationId} using {EmbeddingModel} ({Dimensions} dimensions).",
            document.Id,
            version.VersionNumber,
            persistedChunks.Count,
            generation.Id,
            generation.EmbeddingModelKey,
            generation.EmbeddingDimension);
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
