using System.Collections.Concurrent;
using Aspros.Base.Framework.RAG.Abstractions;
using Aspros.Base.Framework.RAG.Hosting;
using Aspros.Base.Framework.RAG.EntityFrameworkCore;
using Aspros.Base.Framework.RAG.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Aspros.Basic.Framework.IntegrationTests;

public sealed class RagWorkerHostingTests
{
    [Fact]
    public async Task AddRagWorker_UsesIndependentScopesForQueueHeartbeatAndHandler()
    {
        var state = new WorkerState();
        var builder = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(state);
                services.AddScoped<ScopedMarker>();
                services.AddScoped<IIngestionJobQueue, RecordingQueue>();
                services.AddRagWorker<RecordingHandler>(options =>
                {
                    options.WorkerId = "hosting-test-worker";
                    options.MaxConcurrency = 1;
                    options.LeaseDuration = TimeSpan.FromSeconds(2);
                    options.RenewalInterval = TimeSpan.FromMilliseconds(50);
                    options.PollingInterval = TimeSpan.FromMilliseconds(10);
                });
            });

        using var host = builder.Build();
        await host.StartAsync();

        try
        {
            await state.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(state.JobId, state.HandledJobId);
            Assert.NotEqual(state.AcquireScopeId, state.HandlerScopeId);
            Assert.NotEqual(state.RenewalScopeId, state.HandlerScopeId);
            Assert.NotEqual(state.CompleteScopeId, state.HandlerScopeId);
            Assert.NotEqual(state.RenewalScopeId, state.AcquireScopeId);
        }
        finally
        {
            await host.StopAsync();
        }
    }


    [Fact]
    public async Task AddRagWorker_WithEfCoreQueue_RenewsLeaseAndCompletesPersistedJob()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<WorkerDbContext>()
            .UseSqlite(connection)
            .Options;
        Guid jobId;

        await using (var db = new WorkerDbContext(dbOptions))
        {
            await db.Database.EnsureCreatedAsync();

            var knowledgeBase = KnowledgeBase.Create("tenant-worker", "worker-test");
            var document = Document.Create(
                "tenant-worker",
                knowledgeBase.Id,
                "Worker integration test",
                "file:///worker-test.md",
                "sha256:worker-test");
            var version = DocumentVersion.Create(
                document.Id,
                1,
                "sha256:worker-test",
                "text/markdown",
                document.SourceUri,
                "plain-text-v1");
            document.SetCurrentVersion(version);

            var job = IngestionJob.Create(
                "tenant-worker",
                knowledgeBase.Id,
                document.Id,
                version.Id,
                "worker-test-job");
            jobId = job.Id;

            db.AddRange(knowledgeBase, document, version, job);
            await db.SaveChangesAsync();
        }

        var state = new RelationalWorkerState(jobId);
        var builder = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddDbContext<WorkerDbContext>(options => options.UseSqlite(connection));
                services.AddRagEntityFrameworkCore<WorkerDbContext>();
                services.AddScoped<EfCoreIngestionJobQueue<WorkerDbContext>>();
                services.Replace(ServiceDescriptor.Scoped<IIngestionJobQueue>(serviceProvider =>
                    new SignalingQueue(
                        serviceProvider.GetRequiredService<EfCoreIngestionJobQueue<WorkerDbContext>>(),
                        state)));
                services.AddSingleton(state);
                services.AddRagWorker<RelationalRecordingHandler>(options =>
                {
                    options.WorkerId = "efcore-worker-test";
                    options.MaxConcurrency = 1;
                    options.LeaseDuration = TimeSpan.FromSeconds(2);
                    options.RenewalInterval = TimeSpan.FromMilliseconds(50);
                    options.PollingInterval = TimeSpan.FromMilliseconds(10);
                });
            });

        using var host = builder.Build();
        await host.StartAsync();

        try
        {
            await state.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await host.StopAsync();
        }

        await using var verificationDb = new WorkerDbContext(dbOptions);
        var persistedJob = await verificationDb.Set<IngestionJob>()
            .AsNoTracking()
            .SingleAsync(job => job.Id == jobId);

        Assert.Equal(IngestionJobType.IngestDocument, state.ObservedJobType);
        Assert.True(state.RenewalObserved);
        Assert.Equal(IngestionJobStatus.Succeeded, persistedJob.Status);
        Assert.Equal(1, persistedJob.AttemptCount);
        Assert.Null(persistedJob.LeaseOwner);
    }

    public sealed class WorkerDbContext(DbContextOptions<WorkerDbContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyRagModel();
        }
    }

    public sealed class RelationalWorkerState(Guid jobId)
    {
        public Guid JobId { get; } = jobId;
        public IngestionJobType? ObservedJobType { get; set; }
        public bool RenewalObserved { get; set; }

        public TaskCompletionSource Renewed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Completed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed class RelationalRecordingHandler(
        WorkerDbContext dbContext,
        RelationalWorkerState state) : IIngestionJobHandler
    {
        public async Task HandleAsync(
            IngestionJobLease lease,
            CancellationToken cancellationToken = default)
        {
            var job = await dbContext.Set<IngestionJob>()
                .AsNoTracking()
                .SingleAsync(item => item.Id == lease.JobId, cancellationToken);
            state.ObservedJobType = job.JobType;
            await state.Renewed.Task.WaitAsync(cancellationToken);
        }
    }

    public sealed class SignalingQueue(
        EfCoreIngestionJobQueue<WorkerDbContext> inner,
        RelationalWorkerState state) : IIngestionJobQueue
    {
        public Task<IngestionJobLease?> TryAcquireNextAsync(
            string workerId,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) =>
            inner.TryAcquireNextAsync(workerId, leaseDuration, cancellationToken);

        public async Task<bool> RenewAsync(
            IngestionJobLease lease,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            var renewed = await inner.RenewAsync(lease, leaseDuration, cancellationToken);
            if (renewed)
            {
                state.RenewalObserved = true;
                state.Renewed.TrySetResult();
            }

            return renewed;
        }

        public async Task<bool> CompleteAsync(
            IngestionJobLease lease,
            CancellationToken cancellationToken = default)
        {
            var completed = await inner.CompleteAsync(lease, cancellationToken);
            if (completed)
            {
                state.Completed.TrySetResult();
            }

            return completed;
        }

        public Task<bool> FailAsync(
            IngestionJobLease lease,
            string error,
            DateTimeOffset retryAtUtc,
            bool terminal = false,
            CancellationToken cancellationToken = default) =>
            inner.FailAsync(lease, error, retryAtUtc, terminal, cancellationToken);
    }


    [Fact]
    public async Task AddRagWorker_CancelsHandlerAndDoesNotCompleteWhenLeaseIsLost()
    {
        var state = new LeaseLossState();
        using var host = new HostBuilder().ConfigureServices(services =>
        {
            services.AddSingleton(state);
            services.AddSingleton<IIngestionJobQueue, LeaseLossQueue>();
            services.AddRagWorker<LeaseLossHandler>(options =>
            {
                options.WorkerId = "lease-loss-test";
                options.MaxConcurrency = 1;
                options.LeaseDuration = TimeSpan.FromSeconds(2);
                options.RenewalInterval = TimeSpan.FromMilliseconds(50);
                options.PollingInterval = TimeSpan.FromMilliseconds(10);
            });
        }).Build();

        await host.StartAsync();
        try
        {
            await state.HandlerCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(100);
            Assert.Equal(0, state.CompleteCalls);
            Assert.Equal(0, state.FailCalls);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task AddRagWorker_RetriesTransientFailureWithBackoff()
    {
        var state = new FailureState();
        using var host = new HostBuilder().ConfigureServices(services =>
        {
            services.AddSingleton(state);
            services.AddSingleton<IIngestionJobQueue, FailureQueue>();
            services.AddRagWorker<TransientFailureHandler>(options =>
            {
                options.WorkerId = "retry-test";
                options.MaxConcurrency = 1;
                options.LeaseDuration = TimeSpan.FromSeconds(2);
                options.RenewalInterval = TimeSpan.FromMilliseconds(50);
                options.PollingInterval = TimeSpan.FromMilliseconds(10);
                options.RetryDelay = TimeSpan.FromMilliseconds(150);
                options.MaxRetryDelay = TimeSpan.FromSeconds(1);
            });
        }).Build();

        await host.StartAsync();
        try
        {
            await state.Failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(state.Terminal);
            Assert.NotNull(state.RetryAtUtc);
            Assert.True(state.RetryAtUtc > DateTimeOffset.UtcNow);
            Assert.Equal(1, state.FailCalls);
            Assert.Equal(0, state.CompleteCalls);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task AddRagWorker_MarksPermanentHandlerFailureAsTerminal()
    {
        var state = new FailureState();
        using var host = new HostBuilder().ConfigureServices(services =>
        {
            services.AddSingleton(state);
            services.AddSingleton<IIngestionJobQueue, FailureQueue>();
            services.AddRagWorker<PermanentFailureHandler>(options =>
            {
                options.WorkerId = "permanent-failure-test";
                options.MaxConcurrency = 1;
                options.LeaseDuration = TimeSpan.FromSeconds(2);
                options.RenewalInterval = TimeSpan.FromMilliseconds(50);
                options.PollingInterval = TimeSpan.FromMilliseconds(10);
            });
        }).Build();

        await host.StartAsync();
        try
        {
            await state.Failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(state.Terminal);
            Assert.Equal(1, state.FailCalls);
            Assert.Equal(0, state.CompleteCalls);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task AddRagWorker_LeavesInFlightJobUncompletedDuringGracefulShutdown()
    {
        var state = new ShutdownState();
        using var host = new HostBuilder().ConfigureServices(services =>
        {
            services.AddSingleton(state);
            services.AddSingleton<IIngestionJobQueue, ShutdownQueue>();
            services.AddRagWorker<ShutdownHandler>(options =>
            {
                options.WorkerId = "shutdown-test";
                options.MaxConcurrency = 1;
                options.LeaseDuration = TimeSpan.FromSeconds(2);
                options.RenewalInterval = TimeSpan.FromMilliseconds(50);
                options.PollingInterval = TimeSpan.FromMilliseconds(10);
            });
        }).Build();

        await host.StartAsync();
        await state.HandlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.StopAsync();

        Assert.True(state.HandlerCancelled.Task.IsCompleted);
        Assert.Equal(0, state.CompleteCalls);
        Assert.Equal(0, state.FailCalls);
    }

    public sealed class LeaseLossState
    {
        private int _claimed;
        public int CompleteCalls;
        public int FailCalls;
        public TaskCompletionSource HandlerCancelled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool TryClaim() => Interlocked.Increment(ref _claimed) == 1;
    }

    public sealed class LeaseLossQueue(LeaseLossState state) : IIngestionJobQueue
    {
        public Task<IngestionJobLease?> TryAcquireNextAsync(string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default) =>
            Task.FromResult<IngestionJobLease?>(state.TryClaim()
                ? new IngestionJobLease(Guid.NewGuid(), Guid.NewGuid(), workerId, 1, DateTimeOffset.UtcNow.Add(leaseDuration))
                : null);
        public Task<bool> RenewAsync(IngestionJobLease lease, TimeSpan leaseDuration, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> CompleteAsync(IngestionJobLease lease, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref state.CompleteCalls);
            return Task.FromResult(true);
        }
        public Task<bool> FailAsync(IngestionJobLease lease, string error, DateTimeOffset retryAtUtc, bool terminal = false, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref state.FailCalls);
            return Task.FromResult(true);
        }
    }

    public sealed class LeaseLossHandler(LeaseLossState state) : IIngestionJobHandler
    {
        public async Task HandleAsync(IngestionJobLease lease, CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                state.HandlerCancelled.TrySetResult();
                throw;
            }
        }
    }

    public sealed class FailureState
    {
        private int _claimed;
        public int CompleteCalls;
        public int FailCalls;
        public bool Terminal;
        public DateTimeOffset? RetryAtUtc;
        public TaskCompletionSource Failed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool TryClaim() => Interlocked.Increment(ref _claimed) == 1;
    }

    public sealed class FailureQueue(FailureState state) : IIngestionJobQueue
    {
        public Task<IngestionJobLease?> TryAcquireNextAsync(string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default) =>
            Task.FromResult<IngestionJobLease?>(state.TryClaim()
                ? new IngestionJobLease(Guid.NewGuid(), Guid.NewGuid(), workerId, 2, DateTimeOffset.UtcNow.Add(leaseDuration))
                : null);
        public Task<bool> RenewAsync(IngestionJobLease lease, TimeSpan leaseDuration, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> CompleteAsync(IngestionJobLease lease, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref state.CompleteCalls);
            return Task.FromResult(true);
        }
        public Task<bool> FailAsync(IngestionJobLease lease, string error, DateTimeOffset retryAtUtc, bool terminal = false, CancellationToken cancellationToken = default)
        {
            state.Terminal = terminal;
            state.RetryAtUtc = retryAtUtc;
            Interlocked.Increment(ref state.FailCalls);
            state.Failed.TrySetResult();
            return Task.FromResult(true);
        }
    }

    public sealed class TransientFailureHandler : IIngestionJobHandler
    {
        public Task HandleAsync(IngestionJobLease lease, CancellationToken cancellationToken = default) =>
            Task.FromException(new InvalidOperationException("Transient test failure."));
    }

    public sealed class PermanentFailureHandler : IIngestionJobHandler
    {
        public Task HandleAsync(IngestionJobLease lease, CancellationToken cancellationToken = default) =>
            Task.FromException(new IngestionJobPermanentException("Permanent test failure."));
    }

    public sealed class ShutdownState
    {
        private int _claimed;
        public int CompleteCalls;
        public int FailCalls;
        public TaskCompletionSource HandlerStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource HandlerCancelled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool TryClaim() => Interlocked.Increment(ref _claimed) == 1;
    }

    public sealed class ShutdownQueue(ShutdownState state) : IIngestionJobQueue
    {
        public Task<IngestionJobLease?> TryAcquireNextAsync(string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default) =>
            Task.FromResult<IngestionJobLease?>(state.TryClaim()
                ? new IngestionJobLease(Guid.NewGuid(), Guid.NewGuid(), workerId, 1, DateTimeOffset.UtcNow.Add(leaseDuration))
                : null);
        public Task<bool> RenewAsync(IngestionJobLease lease, TimeSpan leaseDuration, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> CompleteAsync(IngestionJobLease lease, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref state.CompleteCalls);
            return Task.FromResult(true);
        }
        public Task<bool> FailAsync(IngestionJobLease lease, string error, DateTimeOffset retryAtUtc, bool terminal = false, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref state.FailCalls);
            return Task.FromResult(true);
        }
    }

    public sealed class ShutdownHandler(ShutdownState state) : IIngestionJobHandler
    {
        public async Task HandleAsync(IngestionJobLease lease, CancellationToken cancellationToken = default)
        {
            state.HandlerStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                state.HandlerCancelled.TrySetResult();
                throw;
            }
        }
    }


    [Fact]
    public async Task MultipleWorkerHosts_CompeteForJobsWithoutDuplicateClaims()
    {
        var state = new MultiHostState(6);
        using var hostA = new HostBuilder()
            .ConfigureServices(services => CopyWorkerServices(services, state, "multi-host-a"))
            .Build();
        using var hostB = new HostBuilder()
            .ConfigureServices(services => CopyWorkerServices(services, state, "multi-host-b"))
            .Build();

        await hostA.StartAsync();
        await hostB.StartAsync();
        try
        {
            await state.AllCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(6, state.CompletedJobIds.Count);
            Assert.Equal(0, state.Failures);
            Assert.InRange(state.MaximumConcurrency, 2, 4);
        }
        finally
        {
            await hostA.StopAsync();
            await hostB.StopAsync();
        }
    }

    private static void CopyWorkerServices(IServiceCollection services, MultiHostState state, string workerId)
    {
        services.AddSingleton(state);
        services.AddSingleton<IIngestionJobQueue, MultiHostQueue>();
        services.AddRagWorker<MultiHostHandler>(options =>
        {
            options.WorkerId = workerId;
            options.MaxConcurrency = 2;
            options.LeaseDuration = TimeSpan.FromSeconds(5);
            options.RenewalInterval = TimeSpan.FromMilliseconds(250);
            options.PollingInterval = TimeSpan.FromMilliseconds(10);
        });
    }

    public sealed class MultiHostState
    {
        private int _active;
        private int _completed;
        public ConcurrentQueue<IngestionJobLease> Jobs { get; } = new();
        public ConcurrentDictionary<Guid, byte> CompletedJobIds { get; } = new();
        public int MaximumConcurrency;
        public int Failures;
        public TaskCompletionSource AllCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public MultiHostState(int jobCount)
        {
            for (var i = 0; i < jobCount; i++)
            {
                Jobs.Enqueue(new IngestionJobLease(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "seed-worker",
                    1,
                    DateTimeOffset.UtcNow.AddMinutes(1)));
            }
        }

        public void HandlerStarted()
        {
            var active = Interlocked.Increment(ref _active);
            int observed;
            do
            {
                observed = Volatile.Read(ref MaximumConcurrency);
                if (active <= observed) break;
            } while (Interlocked.CompareExchange(ref MaximumConcurrency, active, observed) != observed);
        }

        public void HandlerStopped() => Interlocked.Decrement(ref _active);

        public void Completed(Guid jobId)
        {
            if (!CompletedJobIds.TryAdd(jobId, 0))
            {
                Interlocked.Increment(ref Failures);
            }

            if (Interlocked.Increment(ref _completed) == 6)
            {
                AllCompleted.TrySetResult();
            }
        }
    }

    public sealed class MultiHostQueue(MultiHostState state) : IIngestionJobQueue
    {
        public Task<IngestionJobLease?> TryAcquireNextAsync(
            string workerId,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!state.Jobs.TryDequeue(out var candidate))
            {
                return Task.FromResult<IngestionJobLease?>(null);
            }

            return Task.FromResult<IngestionJobLease?>(candidate with
            {
                WorkerId = workerId,
                LeaseExpiresAtUtc = DateTimeOffset.UtcNow.Add(leaseDuration)
            });
        }

        public Task<bool> RenewAsync(
            IngestionJobLease lease,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> CompleteAsync(
            IngestionJobLease lease,
            CancellationToken cancellationToken = default)
        {
            state.Completed(lease.JobId);
            return Task.FromResult(true);
        }

        public Task<bool> FailAsync(
            IngestionJobLease lease,
            string error,
            DateTimeOffset retryAtUtc,
            bool terminal = false,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref state.Failures);
            return Task.FromResult(true);
        }
    }

    public sealed class MultiHostHandler(MultiHostState state) : IIngestionJobHandler
    {
        public async Task HandleAsync(
            IngestionJobLease lease,
            CancellationToken cancellationToken = default)
        {
            state.HandlerStarted();
            try
            {
                await Task.Delay(100, cancellationToken);
            }
            finally
            {
                state.HandlerStopped();
            }
        }
    }


    public sealed class ScopedMarker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    public sealed class WorkerState
    {
        private int _claimCount;

        public Guid JobId { get; } = Guid.NewGuid();
        public Guid? AcquireScopeId { get; set; }
        public Guid? HandlerScopeId { get; set; }
        public Guid? RenewalScopeId { get; set; }
        public Guid? CompleteScopeId { get; set; }
        public Guid? HandledJobId { get; set; }

        public TaskCompletionSource Renewed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Completed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool TryClaim() => Interlocked.Increment(ref _claimCount) == 1;
    }

    public sealed class RecordingHandler(
        ScopedMarker marker,
        WorkerState state) : IIngestionJobHandler
    {
        public async Task HandleAsync(
            IngestionJobLease lease,
            CancellationToken cancellationToken = default)
        {
            state.HandlerScopeId = marker.Id;
            state.HandledJobId = lease.JobId;
            await state.Renewed.Task.WaitAsync(cancellationToken);
        }
    }

    public sealed class RecordingQueue(
        ScopedMarker marker,
        WorkerState state) : IIngestionJobQueue
    {
        public Task<IngestionJobLease?> TryAcquireNextAsync(
            string workerId,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!state.TryClaim())
            {
                return Task.FromResult<IngestionJobLease?>(null);
            }

            state.AcquireScopeId = marker.Id;
            return Task.FromResult<IngestionJobLease?>(new IngestionJobLease(
                state.JobId,
                Guid.NewGuid(),
                workerId,
                1,
                DateTimeOffset.UtcNow.Add(leaseDuration)));
        }

        public Task<bool> RenewAsync(
            IngestionJobLease lease,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            state.RenewalScopeId = marker.Id;
            state.Renewed.TrySetResult();
            return Task.FromResult(true);
        }

        public Task<bool> CompleteAsync(
            IngestionJobLease lease,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            state.CompleteScopeId = marker.Id;
            state.Completed.TrySetResult();
            return Task.FromResult(true);
        }

        public Task<bool> FailAsync(
            IngestionJobLease lease,
            string error,
            DateTimeOffset retryAtUtc,
            bool terminal = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
