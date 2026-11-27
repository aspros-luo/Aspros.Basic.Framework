using Aspros.Base.Framework.RAG.Models;
using Microsoft.EntityFrameworkCore;

namespace Aspros.Base.Framework.RAG.EntityFrameworkCore;

/// <summary>
/// Atomically switches a document to a fully built index generation.
/// The document's current-version and active-generation columns are concurrency tokens,
/// so only one competing activation can commit against the same observed document state.
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
    /// won the optimistic-concurrency race.
    /// </summary>
    public async Task<bool> ActivateAsync(
        Guid documentId,
        Guid generationId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var document = await _dbContext.Set<Document>()
            .SingleOrDefaultAsync(document => document.Id == documentId, cancellationToken);
        var generation = await _dbContext.Set<IndexGeneration>()
            .SingleOrDefaultAsync(item =>
                item.Id == generationId && item.DocumentId == documentId,
                cancellationToken);

        if (document is null || generation is null)
        {
            return false;
        }

        IndexGeneration? previousGeneration = null;
        if (document.ActiveIndexGenerationId is Guid previousGenerationId)
        {
            previousGeneration = await _dbContext.Set<IndexGeneration>()
                .SingleOrDefaultAsync(item => item.Id == previousGenerationId, cancellationToken);

            if (previousGeneration is null)
            {
                throw new InvalidOperationException(
                    "The document points to an active index generation that does not exist.");
            }
        }

        var now = _timeProvider.GetUtcNow();
        generation.Activate(now);
        document.SetActiveIndexGeneration(generation, now);
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
            return false;
        }
    }
}
