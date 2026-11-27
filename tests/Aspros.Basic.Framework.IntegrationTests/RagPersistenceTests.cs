using Aspros.Base.Framework.RAG.Abstractions;
using Aspros.Base.Framework.RAG.EntityFrameworkCore;
using Aspros.Base.Framework.RAG.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aspros.Basic.Framework.IntegrationTests;

public sealed class RagPersistenceTests
{
    [Fact]
    public async Task ApplyRagModel_CreatesRelationalSchemaAndPersistsCanonicalChunkText()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);

        await using (var db = new RagTestDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();

            var (knowledgeBase, document, version) = CreateDocumentGraph();
            var chunk = DocumentChunk.Create(
                "tenant-a",
                knowledgeBase.Id,
                document.Id,
                version.Id,
                0,
                "The canonical text remains in the relational database.",
                "sha256:chunk-1",
                pageNumber: 1,
                sectionTitle: "Persistence");

            db.AddRange(knowledgeBase, document, version, chunk);
            await db.SaveChangesAsync();
        }

        await using (var db = new RagTestDbContext(options))
        {
            var chunk = await db.Set<DocumentChunk>().SingleAsync();
            Assert.Equal("The canonical text remains in the relational database.", chunk.Content);
            Assert.Equal(1, chunk.PageNumber);
            Assert.Equal("tenant-a", chunk.TenantId);
        }
    }

    [Fact]
    public async Task TryAcquireNextAsync_GrantsOnlyOneActiveLease_AndCompletionIsIdempotentlyRejected()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        var jobId = await SeedJobAsync(options);

        await using var db = new RagTestDbContext(options);
        var queue = new EfCoreIngestionJobQueue<RagTestDbContext>(db);

        var first = await queue.TryAcquireNextAsync("worker-1", TimeSpan.FromMinutes(2));
        var second = await queue.TryAcquireNextAsync("worker-2", TimeSpan.FromMinutes(2));

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Equal(jobId, first!.JobId);
        Assert.Equal(1, first.AttemptCount);
        Assert.True(await queue.CompleteAsync(first));
        Assert.False(await queue.CompleteAsync(first));

        var persisted = await db.Set<IngestionJob>().AsNoTracking().SingleAsync();
        Assert.Equal(IngestionJobStatus.Succeeded, persisted.Status);
        Assert.Null(persisted.LeaseOwner);
    }

    [Fact]
    public async Task ExpiredLease_CanBeReclaimed_AndStaleWorkerCannotCompleteIt()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await SeedJobAsync(options);

        IngestionJobLease first;
        await using (var db = new RagTestDbContext(options))
        {
            var queue = new EfCoreIngestionJobQueue<RagTestDbContext>(db);
            first = (await queue.TryAcquireNextAsync("worker-old", TimeSpan.FromMilliseconds(1)))!;
            Assert.NotNull(first);
        }

        await Task.Delay(25);

        await using (var db = new RagTestDbContext(options))
        {
            var queue = new EfCoreIngestionJobQueue<RagTestDbContext>(db);
            var replacement = await queue.TryAcquireNextAsync("worker-new", TimeSpan.FromMinutes(2));

            Assert.NotNull(replacement);
            Assert.NotEqual(first.LeaseToken, replacement!.LeaseToken);
            Assert.Equal(2, replacement.AttemptCount);
            Assert.False(await queue.CompleteAsync(first));
            Assert.True(await queue.CompleteAsync(replacement));
        }
    }

    [Fact]
    public async Task FailAsync_RequeuesRetryableJob_AndHonorsAttemptLimit()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await SeedJobAsync(options, maxAttempts: 2);

        await using var db = new RagTestDbContext(options);
        var queue = new EfCoreIngestionJobQueue<RagTestDbContext>(db);
        var lease = await queue.TryAcquireNextAsync("worker", TimeSpan.FromMinutes(1));

        Assert.NotNull(lease);
        Assert.True(await queue.FailAsync(
            lease!,
            "transient error",
            DateTimeOffset.UtcNow.AddMilliseconds(-1)));

        var retry = await queue.TryAcquireNextAsync("worker", TimeSpan.FromMinutes(1));
        Assert.NotNull(retry);
        Assert.Equal(2, retry!.AttemptCount);

        Assert.True(await queue.FailAsync(
            retry,
            "retry exhausted",
            DateTimeOffset.UtcNow,
            terminal: false));

        var persisted = await db.Set<IngestionJob>().AsNoTracking().SingleAsync();
        Assert.Equal(IngestionJobStatus.Failed, persisted.Status);
        Assert.Equal("retry exhausted", persisted.LastError);
        Assert.Null(await queue.TryAcquireNextAsync("another-worker", TimeSpan.FromMinutes(1)));
    }

    private static async Task<Guid> SeedJobAsync(
        DbContextOptions<RagTestDbContext> options,
        int maxAttempts = 5)
    {
        await using var db = new RagTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var (knowledgeBase, document, version) = CreateDocumentGraph();
        var job = IngestionJob.Create(
            "tenant-a",
            knowledgeBase.Id,
            document.Id,
            version.Id,
            maxAttempts: maxAttempts);

        db.AddRange(knowledgeBase, document, version, job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    private static (KnowledgeBase KnowledgeBase, Document Document, DocumentVersion Version)
        CreateDocumentGraph()
    {
        var knowledgeBase = KnowledgeBase.Create("tenant-a", "engineering");
        var document = Document.Create(
            "tenant-a",
            knowledgeBase.Id,
            "Architecture notes",
            "file:///knowledge/architecture.md",
            "sha256:document-v1");
        document.SetCurrentVersion(1, "sha256:document-v1");

        var version = DocumentVersion.Create(
            document.Id,
            1,
            "sha256:document-v1",
            "text/markdown",
            document.SourceUri,
            "plain-text-v1");

        return (knowledgeBase, document, version);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static DbContextOptions<RagTestDbContext> CreateOptions(SqliteConnection connection) =>
        new DbContextOptionsBuilder<RagTestDbContext>()
            .UseSqlite(connection)
            .Options;

    private sealed class RagTestDbContext(DbContextOptions<RagTestDbContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyRagModel();
        }
    }
}
