using Aspros.Base.Framework.RAG.Models;
using Microsoft.EntityFrameworkCore;

namespace Rag.KnowledgeBase.Api;

public sealed record DemoRetrievedChunk(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentTitle,
    string Content,
    string? SourceLocation,
    float Score);

public sealed class DemoRetrievalService(
    DemoRagDbContext dbContext,
    DemoAiClient aiClient,
    DemoVectorStore vectorStore)
{
    public async Task<IReadOnlyList<DemoRetrievedChunk>> SearchAsync(
        Guid knowledgeBaseId,
        string query,
        int take,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var knowledgeBaseExists = await dbContext.Set<Aspros.Base.Framework.RAG.Models.KnowledgeBase>()
            .AnyAsync(x => x.Id == knowledgeBaseId &&
                           x.TenantId == "demo-tenant" &&
                           !x.IsArchived,
                cancellationToken);
        if (!knowledgeBaseExists)
        {
            return [];
        }

        var queryVectors = await aiClient.GenerateEmbeddingsAsync([query], cancellationToken);
        var boundedTake = Math.Clamp(take, 1, 20);
        var vectorMatches = await vectorStore.SearchAsync(
            "demo-tenant",
            knowledgeBaseId,
            aiClient.EmbeddingModelKey,
            queryVectors[0],
            Math.Clamp(boundedTake * 4, 10, 50),
            cancellationToken);
        if (vectorMatches.Count == 0)
        {
            return [];
        }

        // Keep generation in the candidate key. During blue-green rebuilds, Qdrant
        // intentionally contains the same chunk in both old and new generations; collapsing
        // by ChunkId before checking SQL could retain an inactive generation's score and
        // accidentally discard the active generation's valid match.
        var scores = vectorMatches
            .GroupBy(x => (x.ChunkId, x.IndexGenerationId))
            .ToDictionary(group => group.Key, group => group.OrderByDescending(x => x.Score).First());
        var chunkIds = scores.Keys.Select(key => key.ChunkId).Distinct().ToArray();

        // Qdrant is a candidate generator only. SQL verifies that each result still belongs
        // to the current document version and its currently active index generation.
        var rows = await (
            from chunk in dbContext.Set<DocumentChunk>().AsNoTracking()
            join document in dbContext.Set<Document>().AsNoTracking()
                on chunk.DocumentId equals document.Id
            where chunkIds.Contains(chunk.Id) &&
                  chunk.TenantId == "demo-tenant" &&
                  chunk.KnowledgeBaseId == knowledgeBaseId &&
                  document.TenantId == "demo-tenant" &&
                  document.KnowledgeBaseId == knowledgeBaseId &&
                  document.Status == DocumentStatus.Active &&
                  document.CurrentVersionId == chunk.DocumentVersionId &&
                  document.ActiveIndexGenerationId != null
            select new
            {
                chunk.Id,
                chunk.DocumentId,
                document.Title,
                chunk.Content,
                chunk.SourceLocation,
                document.ActiveIndexGenerationId
            }).ToListAsync(cancellationToken);

        return rows
            .Where(row => row.ActiveIndexGenerationId is Guid activeGenerationId &&
                          scores.ContainsKey((row.Id, activeGenerationId)))
            .Select(row =>
            {
                var activeGenerationId = row.ActiveIndexGenerationId!.Value;
                return new DemoRetrievedChunk(
                    row.Id,
                    row.DocumentId,
                    row.Title,
                    row.Content,
                    row.SourceLocation,
                    scores[(row.Id, activeGenerationId)].Score);
            })
            .OrderByDescending(x => x.Score)
            .Take(boundedTake)
            .ToArray();
    }
}
