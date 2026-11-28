using Aspros.Base.Framework.RAG.Abstractions;
using Aspros.Base.Framework.RAG.EntityFrameworkCore;
using Aspros.Base.Framework.RAG.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aspros.Basic.Framework.IntegrationTests;

public sealed class RagPersistenceTests
{
    [Fact]
    public async Task AddRagEntityFrameworkCore_RegistersQueueAndIndexActivatorForApplicationDbContext()
    {
        await using var connection = await OpenConnectionAsync();
        var services = new ServiceCollection();
        services.AddDbContext<RagTestDbContext>(options => options.UseSqlite(connection));
        services.AddRagEntityFrameworkCore<RagTestDbContext>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        Assert.IsType<EfCoreIngestionJobQueue<RagTestDbContext>>(
            scope.ServiceProvider.GetRequiredService<IIngestionJobQueue>());
        Assert.IsType<EfCoreIndexGenerationActivator<RagTestDbContext>>(
            scope.ServiceProvider.GetRequiredService<EfCoreIndexGenerationActivator<RagTestDbContext>>());
    }

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
            var generation = IndexGeneration.Create(
            CreateIndexJob(knowledgeBase, document, version),
            "local-embedding-v1",
            768,
            "plain-text-v1");
            generation.Activate();
            document.SetActiveIndexGeneration(generation);

            db.AddRange(knowledgeBase, document, version, chunk, generation);
            await db.SaveChangesAsync();
        }

        await using (var db = new RagTestDbContext(options))
        {
            var chunk = await db.Set<DocumentChunk>().SingleAsync();
            Assert.Equal("The canonical text remains in the relational database.", chunk.Content);
            Assert.Equal(1, chunk.PageNumber);
            Assert.Equal("tenant-a", chunk.TenantId);

            var document = await db.Set<Document>().SingleAsync();
            var generation = await db.Set<IndexGeneration>().SingleAsync();
            Assert.Equal(generation.Id, document.ActiveIndexGenerationId);
            Assert.Equal(IndexGenerationStatus.Active, generation.Status);
        }
    }

    [Fact]
    public async Task DocumentCannotBePersistedUnderAnotherTenantsKnowledgeBase()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using var db = new RagTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var knowledgeBase = KnowledgeBase.Create("tenant-a", "private");
        var crossTenantDocument = Document.Create(
            "tenant-b",
            knowledgeBase.Id,
            "forbidden",
            "file:///forbidden.md",
            "sha256:forbidden");

        db.AddRange(knowledgeBase, crossTenantDocument);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ChunkCannotReferenceAnotherDocumentsVersion()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using var db = new RagTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var knowledgeBase = KnowledgeBase.Create("tenant-a", "engineering");
        var firstDocument = Document.Create(
            "tenant-a", knowledgeBase.Id, "first", "file:///first.md", "sha256:first");
        var secondDocument = Document.Create(
            "tenant-a", knowledgeBase.Id, "second", "file:///second.md", "sha256:second");
        var secondVersion = DocumentVersion.Create(
            secondDocument.Id, 1, "sha256:second", "text/markdown",
            secondDocument.SourceUri, "plain-text-v1");
        var invalidChunk = DocumentChunk.Create(
            "tenant-a", knowledgeBase.Id, firstDocument.Id, secondVersion.Id,
            0, "This chunk points to a different document version.", "sha256:invalid");

        db.AddRange(knowledgeBase, firstDocument, secondDocument, secondVersion, invalidChunk);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void DocumentKeepsPreviousActiveIndexUntilNewVersionGenerationIsActivated()
    {
        var (knowledgeBase, document, firstVersion) = CreateDocumentGraph();
        var firstGeneration = IndexGeneration.Create(
            CreateIndexJob(knowledgeBase, document, firstVersion),
            "local-embedding-v1",
            768,
            "plain-text-v1");
        firstGeneration.Activate();
        document.SetActiveIndexGeneration(firstGeneration);

        var secondVersion = DocumentVersion.Create(
            document.Id,
            2,
            "sha256:document-v2",
            "text/markdown",
            document.SourceUri,
            "plain-text-v1");
        document.SetCurrentVersion(secondVersion);

        // A failed rebuild must not make the last known-good generation unavailable.
        Assert.Equal(firstGeneration.Id, document.ActiveIndexGenerationId);
        var failedGeneration = IndexGeneration.Create(
            CreateIndexJob(knowledgeBase, document, secondVersion),
            "local-embedding-v1",
            768,
            "plain-text-v1");
        failedGeneration.MarkFailed();
        Assert.Equal(firstGeneration.Id, document.ActiveIndexGenerationId);

        var replacementGeneration = IndexGeneration.Create(
            CreateIndexJob(knowledgeBase, document, secondVersion),
            "local-embedding-v1",
            768,
            "plain-text-v1");
        replacementGeneration.Activate();
        document.SetActiveIndexGeneration(replacementGeneration);
        firstGeneration.Retire();

        Assert.Equal(replacementGeneration.Id, document.ActiveIndexGenerationId);
        Assert.Equal(IndexGenerationStatus.Retired, firstGeneration.Status);
        Assert.Equal(IndexGenerationStatus.Active, replacementGeneration.Status);
    }

    [Fact]
    public async Task IndexGenerationSwitch_PreservesLastKnownGoodIndexUntilReplacementCommits()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using var db = new RagTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var (knowledgeBase, document, firstVersion) = CreateDocumentGraph();
        var firstGeneration = IndexGeneration.Create(
            CreateIndexJob(knowledgeBase, document, firstVersion),
            "local-embedding-v1",
            768,
            "plain-text-v1");
        firstGeneration.Activate();
        document.SetActiveIndexGeneration(firstGeneration);

        db.AddRange(knowledgeBase, document, firstVersion, firstGeneration);
        await db.SaveChangesAsync();

        var secondVersion = DocumentVersion.Create(
            document.Id,
            2,
            "sha256:document-v2",
            "text/markdown",
            document.SourceUri,
            "plain-text-v1");
        document.SetCurrentVersion(secondVersion);

        var failedGeneration = IndexGeneration.Create(
            CreateIndexJob(knowledgeBase, document, secondVersion),
            "local-embedding-v1",
            768,
            "plain-text-v1");
        failedGeneration.MarkFailed();

        db.AddRange(secondVersion, failedGeneration);
        await db.SaveChangesAsync();

        Assert.Equal(firstGeneration.Id, document.ActiveIndexGenerationId);
        Assert.Equal(IndexGenerationStatus.Active, firstGeneration.Status);

        var replacementGeneration = IndexGeneration.Create(
            CreateIndexJob(knowledgeBase, document, secondVersion),
            "local-embedding-v1",
            768,
            "plain-text-v1");
        replacementGeneration.Activate();

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            document.SetActiveIndexGeneration(replacementGeneration);
            firstGeneration.Retire();
            db.Add(replacementGeneration);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        var persistedDocument = await db.Set<Document>()
            .AsNoTracking()
            .SingleAsync();
        var persistedGenerations = await db.Set<IndexGeneration>()
            .AsNoTracking()
            .OrderBy(generation => generation.CreatedAtUtc)
            .ToListAsync();

        Assert.Equal(replacementGeneration.Id, persistedDocument.ActiveIndexGenerationId);
        Assert.Contains(persistedGenerations, generation =>
            generation.Id == firstGeneration.Id &&
            generation.Status == IndexGenerationStatus.Retired);
        Assert.Contains(persistedGenerations, generation =>
            generation.Id == failedGeneration.Id &&
            generation.Status == IndexGenerationStatus.Failed);
        Assert.Contains(persistedGenerations, generation =>
            generation.Id == replacementGeneration.Id &&
            generation.Status == IndexGenerationStatus.Active);
    }

    [Fact]
    public async Task IndexGeneration_IsUniquePerIngestionJob_ButExplicitRebuildCanCreateANewGeneration()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using var db = new RagTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var (knowledgeBase, document, version) = CreateDocumentGraph();
        var firstJob = CreateIndexJob(knowledgeBase, document, version);
        var firstGeneration = IndexGeneration.Create(
            firstJob, "local-embedding-v1", 768, "plain-text-v1");
        var duplicateForSameJob = IndexGeneration.Create(
            firstJob, "local-embedding-v1", 768, "plain-text-v1");

        db.AddRange(knowledgeBase, document, version, firstGeneration);
        await db.SaveChangesAsync();

        db.Add(duplicateForSameJob);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        // A deliberate rebuild is a new operation, so it receives a new job and generation
        // even when the document version and embedding profile are unchanged.
        var rebuildJob = CreateIndexJob(knowledgeBase, document, version);
        var rebuildGeneration = IndexGeneration.Create(
            rebuildJob, "local-embedding-v1", 768, "plain-text-v1");
        db.ChangeTracker.Clear();
        db.Add(rebuildGeneration);
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.Set<IndexGeneration>().CountAsync());
    }

    [Fact]
    public async Task IndexGenerationActivator_SwitchesPointerAndRetiresPreviousGenerationAtomically()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using var db = new RagTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var (knowledgeBase, document, version) = CreateDocumentGraph();
        var firstJob = CreateIndexJob(knowledgeBase, document, version);
        var firstGeneration = IndexGeneration.Create(
            firstJob, "local-embedding-v1", 768, "plain-text-v1");
        firstGeneration.Activate();
        document.SetActiveIndexGeneration(firstGeneration);

        var replacementJob = CreateIndexJob(knowledgeBase, document, version);
        var replacementGeneration = IndexGeneration.Create(
            replacementJob, "local-embedding-v1", 768, "plain-text-v1");

        db.AddRange(knowledgeBase, document, version, firstGeneration, replacementGeneration);
        await db.SaveChangesAsync();

        var activator = new EfCoreIndexGenerationActivator<RagTestDbContext>(db);
        Assert.True(await activator.ActivateAsync(document.Id, replacementGeneration.Id));
        // Retry after a worker crash between activation and job completion is safe.
        Assert.True(await activator.ActivateAsync(document.Id, replacementGeneration.Id));

        var persistedDocument = await db.Set<Document>()
            .AsNoTracking()
            .SingleAsync();
        var generations = await db.Set<IndexGeneration>()
            .AsNoTracking()
            .OrderBy(item => item.CreatedAtUtc)
            .ToListAsync();

        Assert.Equal(replacementGeneration.Id, persistedDocument.ActiveIndexGenerationId);
        Assert.Contains(generations, item =>
            item.Id == firstGeneration.Id && item.Status == IndexGenerationStatus.Retired);
        Assert.Contains(generations, item =>
            item.Id == replacementGeneration.Id && item.Status == IndexGenerationStatus.Active);
    }

    [Fact]
    public async Task IngestionJob_IdempotencyKeyIsUniqueWithinTenant()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using var db = new RagTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var (knowledgeBase, document, version) = CreateDocumentGraph();
        var first = IngestionJob.Create(
            "tenant-a", knowledgeBase.Id, document.Id, version.Id, "same-request-key");
        var duplicate = IngestionJob.Create(
            "tenant-a", knowledgeBase.Id, document.Id, version.Id, "same-request-key",
            IngestionJobType.RebuildIndex);

        db.AddRange(knowledgeBase, document, version, first);
        await db.SaveChangesAsync();
        db.Add(duplicate);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
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
    public async Task TryAcquireNextAsync_ConcurrentWorkersGrantOnlyOneLeaseAcrossIndependentDbContexts()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"aspros-rag-concurrency-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = 30
        }.ToString();
        var options = CreateFileOptions(connectionString);

        try
        {
            var jobId = await SeedJobAsync(options);
            const int workerCount = 8;
            var startGate = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

            async Task<IngestionJobLease?> AcquireAsync(int workerNumber)
            {
                await using var db = new RagTestDbContext(options);
                var queue = new EfCoreIngestionJobQueue<RagTestDbContext>(db);
                await startGate.Task;
                return await queue.TryAcquireNextAsync(
                    $"concurrent-worker-{workerNumber}",
                    TimeSpan.FromMinutes(2));
            }

            var acquisitions = Enumerable.Range(1, workerCount)
                .Select(AcquireAsync)
                .ToArray();

            startGate.SetResult();
            var leases = await Task.WhenAll(acquisitions);

            var winners = leases.Where(lease => lease is not null).ToArray();
            var winner = Assert.Single(winners)!;
            Assert.Equal(jobId, winner.JobId);
            Assert.Equal(1, winner.AttemptCount);

            await using var verificationDb = new RagTestDbContext(options);
            var persisted = await verificationDb.Set<IngestionJob>()
                .AsNoTracking()
                .SingleAsync();

            Assert.Equal(IngestionJobStatus.Processing, persisted.Status);
            Assert.Equal(1, persisted.AttemptCount);
            Assert.Equal(winner.WorkerId, persisted.LeaseOwner);
            Assert.Equal(winner.LeaseToken, persisted.LeaseToken);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }

            if (File.Exists($"{databasePath}-shm"))
            {
                File.Delete($"{databasePath}-shm");
            }

            if (File.Exists($"{databasePath}-wal"))
            {
                File.Delete($"{databasePath}-wal");
            }
        }
    }

    [Fact]
    public async Task RenewAsync_ExtendsAnOwnedLease()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await SeedJobAsync(options);

        await using var db = new RagTestDbContext(options);
        var queue = new EfCoreIngestionJobQueue<RagTestDbContext>(db);
        var lease = await queue.TryAcquireNextAsync("worker", TimeSpan.FromMinutes(1));

        Assert.NotNull(lease);
        Assert.True(await queue.RenewAsync(lease!, TimeSpan.FromMinutes(5)));
        Assert.True(await queue.CompleteAsync(lease!));
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
            Assert.False(await queue.RenewAsync(first, TimeSpan.FromMinutes(2)));
            Assert.False(await queue.FailAsync(
                first,
                "stale worker must not mutate a reclaimed job",
                DateTimeOffset.UtcNow.AddMinutes(1)));
            Assert.False(await queue.CompleteAsync(first));
            Assert.True(await queue.CompleteAsync(replacement));
        }
    }

    [Fact]
    public async Task ExpiredLeaseOnFinalAttempt_IsMarkedFailedInsteadOfRemainingProcessingForever()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await SeedJobAsync(options, maxAttempts: 1);

        await using (var db = new RagTestDbContext(options))
        {
            var queue = new EfCoreIngestionJobQueue<RagTestDbContext>(db);
            var lease = await queue.TryAcquireNextAsync("worker-crash", TimeSpan.FromMilliseconds(1));
            Assert.NotNull(lease);
        }

        await Task.Delay(25);

        await using (var db = new RagTestDbContext(options))
        {
            var queue = new EfCoreIngestionJobQueue<RagTestDbContext>(db);
            Assert.Null(await queue.TryAcquireNextAsync("recovery-worker", TimeSpan.FromMinutes(1), cancellationToken));

            var persisted = await db.Set<IngestionJob>().AsNoTracking().SingleAsync();
            Assert.Equal(IngestionJobStatus.Failed, persisted.Status);
            Assert.Null(persisted.LeaseOwner);
            Assert.Contains("maximum number of attempts", persisted.LastError);
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

    [Fact]
    public async Task FailAsync_MarksBuildingGenerationFailedOnlyWhenJobBecomesTerminal()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        var seeded = await SeedJobWithGenerationAsync(options, maxAttempts: 2);

        await using var db = new RagTestDbContext(options);
        var queue = new EfCoreIngestionJobQueue<RagTestDbContext>(db);

        var firstLease = await queue.TryAcquireNextAsync("worker", TimeSpan.FromMinutes(1));
        Assert.NotNull(firstLease);
        Assert.Equal(seeded.JobId, firstLease!.JobId);

        Assert.True(await queue.FailAsync(
            firstLease,
            "temporary vector-store outage",
            DateTimeOffset.UtcNow.AddMilliseconds(-1)));

        var generationAfterRetryableFailure = await db.Set<IndexGeneration>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == seeded.GenerationId, cancellationToken);
        Assert.Equal(IndexGenerationStatus.Building, generationAfterRetryableFailure.Status);

        var finalLease = await queue.TryAcquireNextAsync("worker", TimeSpan.FromMinutes(1));
        Assert.NotNull(finalLease);
        Assert.Equal(2, finalLease!.AttemptCount);

        Assert.True(await queue.FailAsync(
            finalLease,
            "retry limit reached",
            DateTimeOffset.UtcNow,
            terminal: false));

        var persistedJob = await db.Set<IngestionJob>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == seeded.JobId);
        var persistedGeneration = await db.Set<IndexGeneration>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == seeded.GenerationId);

        Assert.Equal(IngestionJobStatus.Failed, persistedJob.Status);
        Assert.Equal(IndexGenerationStatus.Failed, persistedGeneration.Status);
    }

    [Fact]
    public async Task ExpiredFinalLease_MarksAssociatedBuildingGenerationFailed()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        var seeded = await SeedJobWithGenerationAsync(options, maxAttempts: 1);

        await using (var db = new RagTestDbContext(options))
        {
            var queue = new EfCoreIngestionJobQueue<RagTestDbContext>(db);
            var lease = await queue.TryAcquireNextAsync("worker-crash", TimeSpan.FromMilliseconds(1));
            Assert.NotNull(lease);
        }

        await Task.Delay(25);

        await using (var db = new RagTestDbContext(options))
        {
            var queue = new EfCoreIngestionJobQueue<RagTestDbContext>(db);
            Assert.Null(await queue.TryAcquireNextAsync("recovery-worker", TimeSpan.FromMinutes(1)));

            var persistedJob = await db.Set<IngestionJob>()
                .AsNoTracking()
                .SingleAsync(item => item.Id == seeded.JobId);
            var persistedGeneration = await db.Set<IndexGeneration>()
                .AsNoTracking()
                .SingleAsync(item => item.Id == seeded.GenerationId);

            Assert.Equal(IngestionJobStatus.Failed, persistedJob.Status);
            Assert.Equal(IndexGenerationStatus.Failed, persistedGeneration.Status);
        }
    }

    [Fact]
    public async Task QueuePoll_ReconcilesPreviouslyFailedJobWithStaleBuildingGeneration()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var options = CreateOptions(connection);
        var seeded = await SeedJobWithGenerationAsync(options, maxAttempts: 5);

        await using var db = new RagTestDbContext(options);
        await db.Set<IngestionJob>()
            .Where(job => job.Id == seeded.JobId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(job => job.Status, IngestionJobStatus.Failed)
                .SetProperty(job => job.CompletedAtUtc, DateTimeOffset.UtcNow), cancellationToken);

        var generationBeforeReconciliation = await db.Set<IndexGeneration>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == seeded.GenerationId);
        Assert.Equal(IndexGenerationStatus.Building, generationBeforeReconciliation.Status);

        var queue = new EfCoreIngestionJobQueue<RagTestDbContext>(db);
        Assert.Null(await queue.TryAcquireNextAsync("recovery-worker", TimeSpan.FromMinutes(1)));

        var generationAfterReconciliation = await db.Set<IndexGeneration>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == seeded.GenerationId);
        Assert.Equal(IndexGenerationStatus.Failed, generationAfterReconciliation.Status);
    }

    private static async Task<(Guid JobId, Guid GenerationId)> SeedJobWithGenerationAsync(
        DbContextOptions<RagTestDbContext> options,
        int maxAttempts)
    {
        await using var db = new RagTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var (knowledgeBase, document, version) = CreateDocumentGraph();
        var job = IngestionJob.Create(
            document.TenantId,
            knowledgeBase.Id,
            document.Id,
            version.Id,
            $"index:{Guid.NewGuid():N}",
            IngestionJobType.RebuildIndex,
            maxAttempts);
        var generation = IndexGeneration.Create(
            job,
            "local-embedding-v1",
            768,
            "plain-text-v1");

        db.AddRange(knowledgeBase, document, version, job, generation);
        await db.SaveChangesAsync();
        return (job.Id, generation.Id);
    }

    private static IngestionJob CreateIndexJob(
        KnowledgeBase knowledgeBase,
        Document document,
        DocumentVersion version)
    {
        return IngestionJob.Create(
            document.TenantId,
            knowledgeBase.Id,
            document.Id,
            version.Id,
            $"index:{Guid.NewGuid():N}",
            IngestionJobType.RebuildIndex);
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
            $"ingest:{document.Id:N}:{version.Id:N}",
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
        var version = DocumentVersion.Create(
            document.Id,
            1,
            "sha256:document-v1",
            "text/markdown",
            document.SourceUri,
            "plain-text-v1");
        document.SetCurrentVersion(version);

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

    private static DbContextOptions<RagTestDbContext> CreateFileOptions(string connectionString) =>
        new DbContextOptionsBuilder<RagTestDbContext>()
            .UseSqlite(connectionString)
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
