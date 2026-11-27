using Aspros.Base.Framework.RAG.Abstractions;
using Aspros.Base.Framework.RAG.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Aspros.Basic.Framework.IntegrationTests;

public sealed class RagWorkerHostingTests
{
    [Fact]
    public async Task AddRagWorker_ExecutesHandlerAndCompletesLeasedJob()
    {
        var queue = new SingleJobQueue();
        var signal = new HandlerSignal();
        var builder = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IIngestionJobQueue>(queue);
                services.AddSingleton(signal);
                services.AddRagWorker<RecordingHandler>(options =>
                {
                    options.WorkerId = "hosting-test-worker";
                    options.MaxConcurrency = 1;
                    options.LeaseDuration = TimeSpan.FromSeconds(2);
                    options.RenewalInterval = TimeSpan.FromMilliseconds(250);
                    options.PollingInterval = TimeSpan.FromMilliseconds(10);
                });
            });

        await using var host = builder.Build();
        await host.StartAsync();

        try
        {
            await queue.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(queue.JobId, signal.HandledJobId);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private sealed class HandlerSignal
    {
        public Guid? HandledJobId { get; set; }
    }

    private sealed class RecordingHandler(HandlerSignal signal) : IIngestionJobHandler
    {
        public Task HandleAsync(
            IngestionJobLease lease,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            signal.HandledJobId = lease.JobId;
            return Task.CompletedTask;
        }
    }

    private sealed class SingleJobQueue : IIngestionJobQueue
    {
        private int _claimCount;

        public Guid JobId { get; } = Guid.NewGuid();

        public TaskCompletionSource Completed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IngestionJobLease?> TryAcquireNextAsync(
            string workerId,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Interlocked.Increment(ref _claimCount) != 1)
            {
                return Task.FromResult<IngestionJobLease?>(null);
            }

            return Task.FromResult<IngestionJobLease?>(new IngestionJobLease(
                JobId,
                Guid.NewGuid(),
                workerId,
                1,
                DateTimeOffset.UtcNow.Add(leaseDuration)));
        }

        public Task<bool> RenewAsync(
            IngestionJobLease lease,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> CompleteAsync(
            IngestionJobLease lease,
            CancellationToken cancellationToken = default)
        {
            Completed.TrySetResult();
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
