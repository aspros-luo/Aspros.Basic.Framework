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

        const string tenantId = "demo-tenant";
        var knowledgeBaseExists = await dbContext.Set<KnowledgeBase>()
            .AnyAsync(x => x.Id == knowledgeBaseId &&
                           x.TenantId == tenantId &&
                           !x.IsArchived,
                cancellationToken);
        if (!knowledgeBaseExists)
        {
            return [];
        }

        var queryVectors = await aiClient.GenerateEmbeddingsAsync([query], cancellationToken);
        var boundedTake = Math.Clamp(take, 1, 20);
        var vectorMatches = await vectorStore.SearchAsync(
            tenantId,
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
            .ToDictionary(group => group.Key, group => group.Max(x => x.Score));

        return await ResolveActiveMatchesAsync(
            dbContext,
            tenantId,
            knowledgeBaseId,
            scores,
            boundedTake,
            cancellationToken);
    }

    /// <summary>
    /// Resolves vector candidates against relational source-of-truth state.
    /// A candidate is valid only when its knowledge base is available, its document and
    /// chunk still point to the same current version, and the document's selected generation
    /// is an Active generation belonging to that exact tenant/knowledge base/document/version.
    /// </summary>
    public static async Task<IReadOnlyList<DemoRetrievedChunk>> ResolveActiveMatchesAsync(
        DemoRagDbContext dbContext,
        string tenantId,
        Guid knowledgeBaseId,
        IReadOnlyDictionary<(Guid ChunkId, Guid GenerationId), float> scores,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(scores);

        if (scores.Count == 0)
        {
            return [];
        }

        var chunkIds = scores.Keys.Select(key => key.ChunkId).Distinct().ToArray();
        var rows = await (
            from chunk in dbContext.Set<DocumentChunk>().AsNoTracking()
            join document in dbContext.Set<Document>().AsNoTracking()
                on chunk.DocumentId equals document.Id
            where chunkIds.Contains(chunk.Id) &&
                  chunk.TenantId == tenantId &&
                  chunk.KnowledgeBaseId == knowledgeBaseId &&
                  document.TenantId == tenantId &&
                  document.KnowledgeBaseId == knowledgeBaseId &&
                  document.Status == DocumentStatus.Active &&
                  document.CurrentVersionId == chunk.DocumentVersionId &&
                  document.ActiveIndexGenerationId != null &&
                  dbContext.Set<KnowledgeBase>().Any(knowledgeBase =>
                      knowledgeBase.Id == knowledgeBaseId &&
                      knowledgeBase.TenantId == tenantId &&
                      !knowledgeBase.IsArchived) &&
                  dbContext.Set<IndexGeneration>().Any(generation =>
                      generation.Id == document.ActiveIndexGenerationId &&
                      generation.Status == IndexGenerationStatus.Active &&
                      generation.TenantId == tenantId &&
                      generation.KnowledgeBaseId == knowledgeBaseId &&
                      generation.DocumentId == document.Id &&
                      generation.DocumentVersionId == chunk.DocumentVersionId)
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
                    scores[(row.Id, activeGenerationId)]);
            })
            .OrderByDescending(x => x.Score)
            .Take(Math.Clamp(take, 1, 20))
            .ToArray();
    }
}
