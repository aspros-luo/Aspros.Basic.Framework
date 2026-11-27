using Aspros.Base.Framework.RAG.Abstractions;
using Aspros.Base.Framework.RAG.Hosting;
using Microsoft.Extensions.DependencyInjection;
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

        await using var host = builder.Build();
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
