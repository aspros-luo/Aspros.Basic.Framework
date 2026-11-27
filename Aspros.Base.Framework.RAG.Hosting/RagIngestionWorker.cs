using System.Diagnostics;
using Aspros.Base.Framework.RAG.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspros.Base.Framework.RAG.Hosting;

/// <summary>
/// Optional queue consumer. Every queue operation and every handler execution gets its own
/// DI scope, so a scoped DbContext is never used concurrently by the handler and lease heartbeat.
/// </summary>
internal sealed class RagIngestionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RagWorkerOptions _options;
    private readonly ILogger<RagIngestionWorker> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly string _workerId;

    public RagIngestionWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<RagWorkerOptions> options,
        ILogger<RagIngestionWorker> logger,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;

        _options.Validate();
        _workerId = string.IsNullOrWhiteSpace(_options.WorkerId)
            ? $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}"
            : _options.WorkerId.Trim();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workers = Enumerable.Range(0, _options.MaxConcurrency)
            .Select(index => RunWorkerLoopAsync(index, stoppingToken));

        await Task.WhenAll(workers);
    }

    private async Task RunWorkerLoopAsync(int workerIndex, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var lease = await InQueueScopeAsync(
                    (queue, token) => queue.TryAcquireNextAsync(
                        _workerId,
                        _options.LeaseDuration,
                        token),
                    stoppingToken);

                if (lease is null)
                {
                    await Task.Delay(_options.PollingInterval, _timeProvider, stoppingToken);
                    continue;
                }

                _logger.LogDebug(
                    "Worker {WorkerId} acquired ingestion job {JobId} (attempt {AttemptCount}).",
                    _workerId,
                    lease.JobId,
                    lease.AttemptCount);

                await ProcessLeaseAsync(lease, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "RAG worker {WorkerId} loop {WorkerIndex} failed. It will retry after the polling interval.",
                    _workerId,
                    workerIndex);

                try
                {
                    await Task.Delay(_options.PollingInterval, _timeProvider, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    private async Task ProcessLeaseAsync(
        IngestionJobLease lease,
        CancellationToken stoppingToken)
    {
        using var jobCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeatTask = RenewLeaseLoopAsync(lease, jobCancellation, heartbeatCancellation.Token);
        Exception? handlerFailure = null;

        try
        {
            await using var handlerScope = _scopeFactory.CreateAsyncScope();
            var handler = handlerScope.ServiceProvider.GetRequiredService<IIngestionJobHandler>();
            await handler.HandleAsync(lease, jobCancellation.Token);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown: leave the lease to expire so another process can recover it.
        }
        catch (OperationCanceledException) when (jobCancellation.IsCancellationRequested)
        {
            // The heartbeat determined that this worker no longer owns the lease.
        }
        catch (Exception exception)
        {
            handlerFailure = exception;
        }
        finally
        {
            heartbeatCancellation.Cancel();
            await IgnoreHeartbeatCancellationAsync(heartbeatTask);
        }

        if (stoppingToken.IsCancellationRequested || jobCancellation.IsCancellationRequested)
        {
            return;
        }

        if (handlerFailure is null)
        {
            var completed = await InQueueScopeAsync(
                (queue, token) => queue.CompleteAsync(lease, token),
                stoppingToken);

            if (!completed)
            {
                _logger.LogWarning(
                    "Worker {WorkerId} could not complete job {JobId}; its lease may have expired or been reclaimed.",
                    _workerId,
                    lease.JobId);
            }

            return;
        }

        var terminal = handlerFailure is IngestionJobPermanentException;
        var retryAtUtc = _timeProvider.GetUtcNow().Add(GetRetryDelay(lease.AttemptCount));
        var failed = await InQueueScopeAsync(
            (queue, token) => queue.FailAsync(
                lease,
                handlerFailure.ToString(),
                retryAtUtc,
                terminal,
                token),
            stoppingToken);

        if (!failed)
        {
            _logger.LogWarning(
                "Worker {WorkerId} could not record failure for job {JobId}; its lease may have expired or been reclaimed.",
                _workerId,
                lease.JobId);
        }

        if (terminal)
        {
            _logger.LogError(
                handlerFailure,
                "Ingestion job {JobId} failed permanently.",
                lease.JobId);
        }
        else
        {
            _logger.LogWarning(
                handlerFailure,
                "Ingestion job {JobId} failed on attempt {AttemptCount}; it will be retried when eligible.",
                lease.JobId,
                lease.AttemptCount);
        }
    }

    private async Task RenewLeaseLoopAsync(
        IngestionJobLease lease,
        CancellationTokenSource jobCancellation,
        CancellationToken heartbeatToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(_options.RenewalInterval, _timeProvider, heartbeatToken);

                var renewed = await InQueueScopeAsync(
                    (queue, token) => queue.RenewAsync(
                        lease,
                        _options.LeaseDuration,
                        token),
                    heartbeatToken);

                if (!renewed)
                {
                    _logger.LogWarning(
                        "Worker {WorkerId} lost the lease for job {JobId}; cancelling its handler.",
                        _workerId,
                        lease.JobId);
                    jobCancellation.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (heartbeatToken.IsCancellationRequested)
        {
            // Normal completion or host shutdown.
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Worker {WorkerId} could not renew the lease for job {JobId}; cancelling its handler to avoid stale work.",
                _workerId,
                lease.JobId);
            jobCancellation.Cancel();
        }
    }

    private async Task<TResult> InQueueScopeAsync<TResult>(
        Func<IIngestionJobQueue, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<IIngestionJobQueue>();
        return await operation(queue, cancellationToken);
    }

    private TimeSpan GetRetryDelay(int attemptCount)
    {
        if (_options.RetryDelay == TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var exponent = Math.Clamp(attemptCount - 1, 0, 30);
        var milliseconds = _options.RetryDelay.TotalMilliseconds * Math.Pow(2, exponent);
        return TimeSpan.FromMilliseconds(
            Math.Min(milliseconds, _options.MaxRetryDelay.TotalMilliseconds));
    }

    private static async Task IgnoreHeartbeatCancellationAsync(Task heartbeatTask)
    {
        try
        {
            await heartbeatTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when the handler finishes before the next heartbeat.
        }
    }
}
