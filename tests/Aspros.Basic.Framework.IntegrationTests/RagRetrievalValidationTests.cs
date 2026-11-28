using Aspros.Base.Framework.RAG.EntityFrameworkCore;
using Aspros.Base.Framework.RAG.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rag.KnowledgeBase.Api;
using Xunit;

namespace Aspros.Basic.Framework.IntegrationTests;

public sealed class RagRetrievalValidationTests
{
    [Fact]
    public async Task ResolveActiveMatchesAsync_ReturnsCandidateForCurrentActiveGeneration()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        var seeded = await SeedDocumentAsync(db);

        var results = await DemoRetrievalService.ResolveActiveMatchesAsync(
            db,
            "tenant-a",
            seeded.KnowledgeBaseId,
            new Dictionary<(Guid ChunkId, Guid GenerationId), float>
            {
                [(seeded.ChunkId, seeded.GenerationId)] = 0.91f
            },
            take: 10,
            cancellationToken: TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal(seeded.ChunkId, result.ChunkId);
        Assert.Equal(0.91f, result.Score);
    }

    [Fact]
    public async Task ResolveActiveMatchesAsync_PrefersCurrentGenerationWhenOldAndNewVectorsCoexist()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        var seeded = await SeedDocumentAsync(db);
        var document = await db.Set<Document>().SingleAsync(x => x.Id == seeded.DocumentId);

        // Model the blue-green overlap window: old vectors may remain in Qdrant
        // after the document has switched to its new active generation.
        var oldJob = IngestionJob.Create(
            "tenant-a",
            seeded.KnowledgeBaseId,
            seeded.DocumentId,
            document.CurrentVersionId!.Value,
            $"index-old:{Guid.NewGuid():N}",
            IngestionJobType.RebuildIndex);
        var oldGeneration = IndexGeneration.Create(
            oldJob,
            "previous-embedding-v1",
            768,
            "plain-text-v1");
        oldGeneration.Activate();
        oldGeneration.Retire();
        db.AddRange(oldJob, oldGeneration);
        await db.SaveChangesAsync();

        var results = await DemoRetrievalService.ResolveActiveMatchesAsync(
            db,
            "tenant-a",
            seeded.KnowledgeBaseId,
            new Dictionary<(Guid ChunkId, Guid GenerationId), float>
            {
                // The stale generation deliberately has the higher score.
                [(seeded.ChunkId, oldGeneration.Id)] = 0.99f,
                [(seeded.ChunkId, seeded.GenerationId)] = 0.71f
            },
            take: 10,
            cancellationToken: TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal(seeded.ChunkId, result.ChunkId);
        Assert.Equal(0.71f, result.Score);
    }

    [Fact]
    public async Task ResolveActiveMatchesAsync_RejectsGenerationWhoseStatusIsNotActive()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        var seeded = await SeedDocumentAsync(db);

        // Simulate stale/corrupt relational state: the document pointer still references
        // this generation, but its authoritative generation status is no longer Active.
        await db.Set<IndexGeneration>()
            .Where(x => x.Id == seeded.GenerationId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.Status, IndexGenerationStatus.Retired));

        var results = await DemoRetrievalService.ResolveActiveMatchesAsync(
            db,
            "tenant-a",
            seeded.KnowledgeBaseId,
            new Dictionary<(Guid ChunkId, Guid GenerationId), float>
            {
                [(seeded.ChunkId, seeded.GenerationId)] = 0.99f
            },
            take: 10,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(results);
    }

    [Fact]
    public async Task ResolveActiveMatchesAsync_RejectsCandidatesFromArchivedKnowledgeBase()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        var seeded = await SeedDocumentAsync(db);

        await db.Set<KnowledgeBase>()
            .Where(x => x.Id == seeded.KnowledgeBaseId)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.IsArchived, true));

        var results = await DemoRetrievalService.ResolveActiveMatchesAsync(
            db,
            "tenant-a",
            seeded.KnowledgeBaseId,
            new Dictionary<(Guid ChunkId, Guid GenerationId), float>
            {
                [(seeded.ChunkId, seeded.GenerationId)] = 0.99f
            },
            take: 10,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(results);
    }

    [Fact]
    public async Task ResolveActiveMatchesAsync_RejectsChunkFromNonCurrentDocumentVersion()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        var seeded = await SeedDocumentAsync(db);

        var replacementVersion = DocumentVersion.Create(
            seeded.DocumentId,
            2,
            "sha256:version-2",
            "text/plain",
            "file:///knowledge/current.txt",
            "plain-text-v1");
        var document = await db.Set<Document>().SingleAsync(x => x.Id == seeded.DocumentId);
        document.SetCurrentVersion(replacementVersion);
        db.Add(replacementVersion);
        await db.SaveChangesAsync();

        var results = await DemoRetrievalService.ResolveActiveMatchesAsync(
            db,
            "tenant-a",
            seeded.KnowledgeBaseId,
            new Dictionary<(Guid ChunkId, Guid GenerationId), float>
            {
                [(seeded.ChunkId, seeded.GenerationId)] = 0.99f
            },
            take: 10,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(results);
    }

    private static async Task<(Guid KnowledgeBaseId, Guid DocumentId, Guid ChunkId, Guid GenerationId)>
        SeedDocumentAsync(DemoRagDbContext db)
    {
        var knowledgeBase = KnowledgeBase.Create("tenant-a", "engineering");
        var document = Document.Create(
            "tenant-a",
            knowledgeBase.Id,
            "Architecture notes",
            "file:///knowledge/architecture.md",
            "sha256:version-1");
        var version = DocumentVersion.Create(
            document.Id,
            1,
            "sha256:version-1",
            "text/markdown",
            document.SourceUri,
            "plain-text-v1");
        document.SetCurrentVersion(version);

        var job = IngestionJob.Create(
            document.TenantId,
            knowledgeBase.Id,
            document.Id,
            version.Id,
            $"index:{Guid.NewGuid():N}",
            IngestionJobType.RebuildIndex);
        var generation = IndexGeneration.Create(
            job,
            "local-embedding-v1",
            768,
            "plain-text-v1");
        generation.Activate();
        document.SetActiveIndexGeneration(generation);

        var chunk = DocumentChunk.Create(
            document.TenantId,
            knowledgeBase.Id,
            document.Id,
            version.Id,
            0,
            "The current document content.",
            "sha256:chunk-1");

        db.AddRange(knowledgeBase, document, version, job, generation, chunk);
        await db.SaveChangesAsync();

        return (knowledgeBase.Id, document.Id, chunk.Id, generation.Id);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static DemoRagDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<DemoRagDbContext>()
            .UseSqlite(connection)
            .Options;
        return new DemoRagDbContext(options);
    }
}
