using Aspros.Base.Framework.RAG.Abstractions;
using Aspros.Base.Framework.RAG.Models;
using Microsoft.EntityFrameworkCore;

namespace Aspros.Base.Framework.RAG.EntityFrameworkCore;

/// <summary>
/// A provider-neutral, optimistic lease queue backed by the host application's relational DbContext.
/// Claims are atomic conditional updates: concurrent workers can read the same candidate, but only
/// the worker that changes the observed lease token can own it.
/// </summary>
public sealed class EfCoreIngestionJobQueue<TContext> : IIngestionJobQueue
    where TContext : DbContext
{
    private readonly TContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public EfCoreIngestionJobQueue(TContext dbContext, TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IngestionJobLease?> TryAcquireNextAsync(
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        ValidateLeaseDuration(leaseDuration);

        var now = _timeProvider.GetUtcNow();

        // Reconcile both newly exhausted leases and terminal jobs left behind by an
        // older process version. The latter makes already-persisted Building generations
        // self-heal after deployment instead of requiring manual database repair.
        var hasUnfinishedTerminalGenerations = await _dbContext.Set<IndexGeneration>()
            .AnyAsync(generation =>
                generation.Status == IndexGenerationStatus.Building &&
                _dbContext.Set<IngestionJob>().Any(job =>
                    job.Id == generation.IngestionJobId &&
                    (job.Status == IngestionJobStatus.Failed ||
                     (job.Status == IngestionJobStatus.Processing &&
                      job.LeaseExpiresAtUtc != null &&
                      job.LeaseExpiresAtUtc <= now &&
                      job.AttemptCount >= job.MaxAttempts))),
                cancellationToken);

        // Keep the polling hot path read-only when no terminal job has unfinished index work.
        if (hasUnfinishedTerminalGenerations)
        {
            await using var recoveryTransaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // A worker that crashed on its final allowed attempt must not leave a job
            // in Processing forever.
            var expiredJobIds = await _dbContext.Set<IngestionJob>()
                .Where(job =>
                    job.Status == IngestionJobStatus.Processing &&
                    job.LeaseExpiresAtUtc != null &&
                    job.LeaseExpiresAtUtc <= now &&
                    job.AttemptCount >= job.MaxAttempts)
                .Select(job => job.Id)
                .ToArrayAsync(cancellationToken);

            if (expiredJobIds.Length > 0)
            {
                await _dbContext.Set<IngestionJob>()
                    .Where(job =>
                        expiredJobIds.Contains(job.Id) &&
                        job.Status == IngestionJobStatus.Processing &&
                        job.LeaseExpiresAtUtc != null &&
                        job.LeaseExpiresAtUtc <= now &&
                        job.AttemptCount >= job.MaxAttempts)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(job => job.Status, IngestionJobStatus.Failed)
                        .SetProperty(job => job.LeaseOwner, (string?)null)
                        .SetProperty(job => job.LeaseToken, Guid.Empty)
                        .SetProperty(job => job.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                        .SetProperty(job => job.LastError, job => job.LastError ?? "Worker lease expired after the maximum number of attempts.")
                        .SetProperty(job => job.CompletedAtUtc, now)
                        .SetProperty(job => job.UpdatedAtUtc, now),
                        cancellationToken);
            }

            // Also repair terminal failures written before this lifecycle invariant existed.
            // The status predicate limits this transition to Building generations only.
            var terminalJobIds = await _dbContext.Set<IndexGeneration>()
                .Where(generation => generation.Status == IndexGenerationStatus.Building)
                .Join(
                    _dbContext.Set<IngestionJob>().Where(job => job.Status == IngestionJobStatus.Failed),
                    generation => generation.IngestionJobId,
                    job => job.Id,
                    (_, job) => job.Id)
                .Distinct()
                .ToArrayAsync(cancellationToken);

            if (terminalJobIds.Length > 0)
            {
                await _dbContext.Set<IndexGeneration>()
                    .Where(generation =>
                        terminalJobIds.Contains(generation.IngestionJobId) &&
                        generation.Status == IndexGenerationStatus.Building)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(generation => generation.Status, IndexGenerationStatus.Failed),
                        cancellationToken);
            }

            await recoveryTransaction.CommitAsync(cancellationToken);
        }

        var candidate = await _dbContext.Set<IngestionJob>()
            .AsNoTracking()
            .Where(job =>
                job.AttemptCount < job.MaxAttempts &&
                job.NextAttemptAtUtc <= now &&
                (job.Status == IngestionJobStatus.Pending ||
                 (job.Status == IngestionJobStatus.Processing &&
                  job.LeaseExpiresAtUtc != null &&
                  job.LeaseExpiresAtUtc <= now)))
            .OrderBy(job => job.CreatedAtUtc)
            .Select(job => new Candidate(
                job.Id,
                job.LeaseToken,
                job.AttemptCount))
            .FirstOrDefaultAsync(cancellationToken);

        if (candidate is null)
        {
            return null;
        }

        var leaseToken = Guid.NewGuid();
        var leaseExpiresAt = now.Add(leaseDuration);

        var affected = await _dbContext.Set<IngestionJob>()
            .Where(job =>
                job.Id == candidate.Id &&
                job.LeaseToken == candidate.LeaseToken &&
                job.AttemptCount < job.MaxAttempts &&
                job.NextAttemptAtUtc <= now &&
                (job.Status == IngestionJobStatus.Pending ||
                 (job.Status == IngestionJobStatus.Processing &&
                  job.LeaseExpiresAtUtc != null &&
                  job.LeaseExpiresAtUtc <= now)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(job => job.Status, IngestionJobStatus.Processing)
                .SetProperty(job => job.LeaseOwner, workerId.Trim())
                .SetProperty(job => job.LeaseToken, leaseToken)
                .SetProperty(job => job.LeaseExpiresAtUtc, leaseExpiresAt)
                .SetProperty(job => job.AttemptCount, job => job.AttemptCount + 1)
                .SetProperty(job => job.StartedAtUtc, job => job.StartedAtUtc ?? now)
                .SetProperty(job => job.UpdatedAtUtc, now),
                cancellationToken);

        return affected == 1
            ? new IngestionJobLease(
                candidate.Id,
                leaseToken,
                workerId.Trim(),
                candidate.AttemptCount + 1,
                leaseExpiresAt)
            : null;
    }

    public async Task<bool> RenewAsync(
        IngestionJobLease lease,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ValidateLeaseDuration(leaseDuration);

        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.Add(leaseDuration);

        var affected = await _dbContext.Set<IngestionJob>()
            .Where(job =>
                job.Id == lease.JobId &&
                job.Status == IngestionJobStatus.Processing &&
                job.LeaseToken == lease.LeaseToken &&
                job.LeaseOwner == lease.WorkerId &&
                job.LeaseExpiresAtUtc > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(job => job.LeaseExpiresAtUtc, expiresAt)
                .SetProperty(job => job.UpdatedAtUtc, now),
                cancellationToken);

        return affected == 1;
    }

    public async Task<bool> CompleteAsync(
        IngestionJobLease lease,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);

        var now = _timeProvider.GetUtcNow();
        var affected = await _dbContext.Set<IngestionJob>()
            .Where(job =>
                job.Id == lease.JobId &&
                job.Status == IngestionJobStatus.Processing &&
                job.LeaseToken == lease.LeaseToken &&
                job.LeaseOwner == lease.WorkerId &&
                job.LeaseExpiresAtUtc > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(job => job.Status, IngestionJobStatus.Succeeded)
                .SetProperty(job => job.LeaseOwner, (string?)null)
                .SetProperty(job => job.LeaseToken, Guid.Empty)
                .SetProperty(job => job.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(job => job.LastError, (string?)null)
                .SetProperty(job => job.CompletedAtUtc, now)
                .SetProperty(job => job.UpdatedAtUtc, now),
                cancellationToken);

        return affected == 1;
    }

    public async Task<bool> FailAsync(
        IngestionJobLease lease,
        string error,
        DateTimeOffset retryAtUtc,
        bool terminal = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        var now = _timeProvider.GetUtcNow();
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Read the attempt limit under the same transaction so the generation transition
        // follows the exact terminal decision made for this owned lease.
        var job = await _dbContext.Set<IngestionJob>()
            .AsNoTracking()
            .Where(job =>
                job.Id == lease.JobId &&
                job.Status == IngestionJobStatus.Processing &&
                job.LeaseToken == lease.LeaseToken &&
                job.LeaseOwner == lease.WorkerId &&
                job.LeaseExpiresAtUtc > now)
            .Select(job => new { job.AttemptCount, job.MaxAttempts })
            .SingleOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            return false;
        }

        var isTerminal = terminal || job.AttemptCount >= job.MaxAttempts;
        var affected = await _dbContext.Set<IngestionJob>()
            .Where(item =>
                item.Id == lease.JobId &&
                item.Status == IngestionJobStatus.Processing &&
                item.LeaseToken == lease.LeaseToken &&
                item.LeaseOwner == lease.WorkerId &&
                item.LeaseExpiresAtUtc > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status,
                    isTerminal ? IngestionJobStatus.Failed : IngestionJobStatus.Pending)
                .SetProperty(item => item.LeaseOwner, (string?)null)
                .SetProperty(item => item.LeaseToken, Guid.Empty)
                .SetProperty(item => item.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.NextAttemptAtUtc, retryAtUtc)
                .SetProperty(item => item.LastError, error.Length <= 4000 ? error : error[..4000])
                .SetProperty(item => item.CompletedAtUtc, isTerminal ? now : null)
                .SetProperty(item => item.UpdatedAtUtc, now),
                cancellationToken);

        if (affected != 1)
        {
            return false;
        }

        if (isTerminal)
        {
            // Keep job and generation lifecycle consistent even if the process crashes
            // immediately after recording the terminal job failure.
            await _dbContext.Set<IndexGeneration>()
                .Where(generation =>
                    generation.IngestionJobId == lease.JobId &&
                    generation.Status == IndexGenerationStatus.Building)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(generation => generation.Status, IndexGenerationStatus.Failed),
                    cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static void ValidateLeaseDuration(TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leaseDuration),
                "Lease duration must be greater than zero.");
        }
    }

    private sealed record Candidate(Guid Id, Guid LeaseToken, int AttemptCount);
}
