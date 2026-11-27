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

        // A worker that crashed on its final allowed attempt must not leave a job
        // in Processing forever. Expired, exhausted leases are terminally closed first.
        await _dbContext.Set<IngestionJob>()
            .Where(job =>
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
        var affected = await _dbContext.Set<IngestionJob>()
            .Where(job =>
                job.Id == lease.JobId &&
                job.Status == IngestionJobStatus.Processing &&
                job.LeaseToken == lease.LeaseToken &&
                job.LeaseOwner == lease.WorkerId &&
                job.LeaseExpiresAtUtc > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    job => job.Status,
                    job => terminal || job.AttemptCount >= job.MaxAttempts
                        ? IngestionJobStatus.Failed
                        : IngestionJobStatus.Pending)
                .SetProperty(job => job.LeaseOwner, (string?)null)
                .SetProperty(job => job.LeaseToken, Guid.Empty)
                .SetProperty(job => job.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(job => job.NextAttemptAtUtc, retryAtUtc)
                .SetProperty(job => job.LastError, error.Length <= 4000 ? error : error[..4000])
                .SetProperty(job => job.CompletedAtUtc, job => terminal || job.AttemptCount >= job.MaxAttempts ? now : null)
                .SetProperty(job => job.UpdatedAtUtc, now),
                cancellationToken);

        return affected == 1;
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
