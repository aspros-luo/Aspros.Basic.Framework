using Aspros.Base.Framework.RAG.Abstractions;
using Aspros.Base.Framework.RAG.EntityFrameworkCore;
using Aspros.Base.Framework.RAG.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aspros.Basic.Framework.IntegrationTests;

/// <summary>
/// Provider-level concurrency checks. Uses a dedicated database so these tests never
/// delete or mutate the shared Framework/Category integration-test database.
/// </summary>
public sealed class RagMySqlProviderTests
{
    public static bool MySqlAvailable =>
        !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("ConnectionStrings__RagTestDatabase"));

    [Fact(
        Skip = "A dedicated RAG MySQL test database is not configured.",
        SkipUnless = nameof(MySqlAvailable),
        SkipType = typeof(RagMySqlProviderTests))]
    public async Task TryAcquireNextAsync_ConcurrentWorkersGrantOnlyOneLease()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__RagTestDatabase")!;
        var options = new DbContextOptionsBuilder<RagMySqlTestDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0)))
            .Options;

        await using (var initialization = new RagMySqlTestDbContext(options))
        {
            await initialization.Database.EnsureCreatedAsync();
            await ClearRagTablesAsync(initialization);
        }

        var (knowledgeBase, document, version) = CreateDocumentGraph();
        var job = IngestionJob.Create(
            "tenant-a",
            knowledgeBase.Id,
            document.Id,
            version.Id,
            $"mysql-concurrency:{Guid.NewGuid():N}");

        await using (var seed = new RagMySqlTestDbContext(options))
        {
            seed.AddRange(knowledgeBase, document, version, job);
            await seed.SaveChangesAsync();
        }

        const int workerCount = 8;
        var startGate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<IngestionJobLease?> AcquireAsync(int workerNumber)
        {
            await using var db = new RagMySqlTestDbContext(options);
            var queue = new EfCoreIngestionJobQueue<RagMySqlTestDbContext>(db);
            await startGate.Task;
            return await queue.TryAcquireNextAsync(
                $"mysql-worker-{workerNumber}",
                TimeSpan.FromMinutes(2));
        }

        var acquisitions = Enumerable.Range(1, workerCount)
            .Select(AcquireAsync)
            .ToArray();

        startGate.SetResult();
        var leases = await Task.WhenAll(acquisitions);

        var winner = Assert.Single(leases.Where(lease => lease is not null))!;
        Assert.Equal(job.Id, winner.JobId);
        Assert.Equal(1, winner.AttemptCount);

        await using var verification = new RagMySqlTestDbContext(options);
        var persisted = await verification.Set<IngestionJob>()
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(IngestionJobStatus.Processing, persisted.Status);
        Assert.Equal(1, persisted.AttemptCount);
        Assert.Equal(winner.WorkerId, persisted.LeaseOwner);
        Assert.Equal(winner.LeaseToken, persisted.LeaseToken);
    }

    [Fact(
        Skip = "A dedicated RAG MySQL test database is not configured.",
        SkipUnless = nameof(MySqlAvailable),
        SkipType = typeof(RagMySqlProviderTests))]
    public async Task ExpiredLease_CanBeReclaimed_AndStaleWorkerCannotMutateIt()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__RagTestDatabase")!;
        var options = new DbContextOptionsBuilder<RagMySqlTestDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0)))
            .Options;
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow.AddSeconds(1));

        await using (var initialization = new RagMySqlTestDbContext(options))
        {
            await initialization.Database.EnsureCreatedAsync();
            await ClearRagTablesAsync(initialization);
        }

        var (knowledgeBase, document, version) = CreateDocumentGraph();
        var job = IngestionJob.Create(
            "tenant-a",
            knowledgeBase.Id,
            document.Id,
            version.Id,
            $"mysql-lease-recovery:{Guid.NewGuid():N}",
            maxAttempts: 3,
            now: clock.GetUtcNow());

        await using (var seed = new RagMySqlTestDbContext(options))
        {
            seed.AddRange(knowledgeBase, document, version, job);
            await seed.SaveChangesAsync();
        }

        IngestionJobLease firstLease;
        await using (var crashedWorkerDb = new RagMySqlTestDbContext(options))
        {
            var crashedWorkerQueue =
                new EfCoreIngestionJobQueue<RagMySqlTestDbContext>(crashedWorkerDb, clock);
            firstLease = (await crashedWorkerQueue.TryAcquireNextAsync(
                "mysql-worker-before-crash",
                TimeSpan.FromMinutes(1)))!;
            Assert.NotNull(firstLease);
        }

        // Simulate process loss: no heartbeat or completion arrives before the lease expires.
        clock.Advance(TimeSpan.FromMinutes(2));

        await using (var recoveryWorkerDb = new RagMySqlTestDbContext(options))
        {
            var recoveryQueue =
                new EfCoreIngestionJobQueue<RagMySqlTestDbContext>(recoveryWorkerDb, clock);
            var replacementLease = await recoveryQueue.TryAcquireNextAsync(
                "mysql-recovery-worker",
                TimeSpan.FromMinutes(1));

            Assert.NotNull(replacementLease);
            Assert.NotEqual(firstLease.LeaseToken, replacementLease!.LeaseToken);
            Assert.Equal(2, replacementLease.AttemptCount);

            Assert.False(await recoveryQueue.RenewAsync(firstLease, TimeSpan.FromMinutes(1)));
            Assert.False(await recoveryQueue.FailAsync(
                firstLease,
                "stale worker must not mutate a reclaimed job",
                clock.GetUtcNow().AddMinutes(1)));
            Assert.False(await recoveryQueue.CompleteAsync(firstLease));
            Assert.True(await recoveryQueue.CompleteAsync(replacementLease));
        }

        await using var verification = new RagMySqlTestDbContext(options);
        var persisted = await verification.Set<IngestionJob>()
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(IngestionJobStatus.Succeeded, persisted.Status);
        Assert.Equal(2, persisted.AttemptCount);
        Assert.Null(persisted.LeaseOwner);
        Assert.Equal(Guid.Empty, persisted.LeaseToken);
    }

    private static async Task ClearRagTablesAsync(DbContext db)
    {
        // Delete in FK dependency order. This database is explicitly dedicated to RAG tests.
        foreach (var table in new[]
        {
            "rag_document_chunks",
            "rag_ingestion_jobs",
            "rag_index_generations",
            "rag_document_versions",
            "rag_documents",
            "rag_knowledge_bases"
        })
        {
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM `{table}`;");
        }
    }

    private static (KnowledgeBase KnowledgeBase, Document Document, DocumentVersion Version)
        CreateDocumentGraph()
    {
        var knowledgeBase = KnowledgeBase.Create("tenant-a", "engineering");
        var document = Document.Create(
            "tenant-a",
            knowledgeBase.Id,
            "MySQL concurrency fixture",
            $"file:///mysql-tests/{Guid.NewGuid():N}.md",
            $"sha256:{Guid.NewGuid():N}");
        var version = DocumentVersion.Create(
            document.Id,
            1,
            document.ContentHash,
            "text/markdown",
            document.SourceUri,
            "plain-text-v1");
        document.SetCurrentVersion(version);

        return (knowledgeBase, document, version);
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    private sealed class RagMySqlTestDbContext(
        DbContextOptions<RagMySqlTestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyRagModel();
        }
    }
}
