using Aspros.Base.Framework.RAG.Models;
using Microsoft.EntityFrameworkCore;

namespace Aspros.Base.Framework.RAG.EntityFrameworkCore;

/// <summary>
/// Atomically switches a document to a fully built index generation.
/// The document pointer is changed with a conditional SQL update before generation statuses
/// are persisted, so competing activations serialize on the document row.
/// </summary>
public sealed class EfCoreIndexGenerationActivator<TContext>
    where TContext : DbContext
{
    private readonly TContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public EfCoreIndexGenerationActivator(TContext dbContext, TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Activates the specified building generation and retires the previous active generation
    /// in one relational transaction. Returns false when another activation or version update
    /// won the compare-and-swap race.
    /// </summary>
    public async Task<bool> ActivateAsync(
        Guid documentId,
        Guid generationId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var document = await _dbContext.Set<Document>()
            .SingleOrDefaultAsync(item => item.Id == documentId, cancellationToken);
        var generation = await _dbContext.Set<IndexGeneration>()
            .SingleOrDefaultAsync(item =>
                item.Id == generationId && item.DocumentId == documentId,
                cancellationToken);

        if (document is null || generation is null)
        {
            return false;
        }

        var expectedVersionId = document.CurrentVersionId;
        var expectedActiveGenerationId = document.ActiveIndexGenerationId;

        IndexGeneration? previousGeneration = null;
        if (expectedActiveGenerationId is Guid previousId)
        {
            previousGeneration = await _dbContext.Set<IndexGeneration>()
                .SingleOrDefaultAsync(item => item.Id == previousId, cancellationToken);

            if (previousGeneration is null)
            {
                throw new InvalidOperationException(
                    "The document points to an active index generation that does not exist.");
            }
        }

        var now = _timeProvider.GetUtcNow();

        // Domain methods validate that the target is Building and matches the current version.
        generation.Activate(now);
        document.SetActiveIndexGeneration(generation, now);

        // This is the compare-and-swap boundary. The WHERE clause includes both observed
        // pointers; a concurrent version update or activation makes the affected row count zero.
        var affected = await _dbContext.Set<Document>()
            .Where(item =>
                item.Id == documentId &&
                item.CurrentVersionId == expectedVersionId &&
                item.ActiveIndexGenerationId == expectedActiveGenerationId &&
                item.Status != DocumentStatus.Deleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.ActiveIndexGenerationId, generationId)
                .SetProperty(item => item.UpdatedAtUtc, now),
                cancellationToken);

        if (affected != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.Entry(document).State = EntityState.Detached;
            _dbContext.Entry(generation).State = EntityState.Detached;
            if (previousGeneration is not null)
            {
                _dbContext.Entry(previousGeneration).State = EntityState.Detached;
            }

            return false;
        }

        // ExecuteUpdate bypasses the change tracker. Detach the stale in-memory document
        // so SaveChanges cannot overwrite the pointer or trigger a second concurrency check.
        _dbContext.Entry(document).State = EntityState.Detached;

        previousGeneration?.Retire(now);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.Entry(generation).State = EntityState.Detached;
            if (previousGeneration is not null)
            {
                _dbContext.Entry(previousGeneration).State = EntityState.Detached;
            }

            return false;
        }
    }
}
