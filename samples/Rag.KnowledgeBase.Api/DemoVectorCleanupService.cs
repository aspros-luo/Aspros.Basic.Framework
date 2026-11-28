using Aspros.Base.Framework.RAG.Models;
using Microsoft.EntityFrameworkCore;

namespace Rag.KnowledgeBase.Api;

/// <summary>
/// Reclaims vectors for retired index generations. SQL remains the source of truth:
/// a generation is eligible only after the activation transaction has retired it.
/// Qdrant deletion is idempotent, so a crash before the SQL receipt is saved is safe to retry.
/// </summary>
public sealed class DemoVectorCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<DemoVectorCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(15);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The retired vector generation cleanup batch failed.");
            }

            try
            {
                await Task.Delay(PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task CleanupBatchAsync(CancellationToken cancellationToken)
    {
        List<Guid> candidates;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DemoRagDbContext>();
            candidates = await db.Set<IndexGeneration>()
                .Where(generation =>
                    generation.Status == IndexGenerationStatus.Retired &&
                    !db.Set<DemoVectorCleanupRecord>()
                        .Any(receipt => receipt.IndexGenerationId == generation.Id))
                .OrderBy(generation => generation.RetiredAtUtc)
                .Take(BatchSize)
                .Select(generation => generation.Id)
                .ToListAsync(cancellationToken);
        }

        foreach (var generationId in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var scope = scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<DemoRagDbContext>();
            var vectorStore = services.GetRequiredService<DemoVectorStore>();

            var generation = await db.Set<IndexGeneration>()
                .AsNoTracking()
                .SingleOrDefaultAsync(item =>
                    item.Id == generationId &&
                    item.Status == IndexGenerationStatus.Retired,
                    cancellationToken);

            if (generation is null)
            {
                continue;
            }

            var alreadyCleaned = await db.Set<DemoVectorCleanupRecord>()
                .AnyAsync(receipt => receipt.IndexGenerationId == generationId, cancellationToken);
            if (alreadyCleaned)
            {
                continue;
            }

            try
            {
                await vectorStore.DeleteGenerationAsync(
                    generation.TenantId,
                    generation.KnowledgeBaseId,
                    generation.EmbeddingModelKey,
                    generation.EmbeddingDimension,
                    generation.Id,
                    cancellationToken);

                db.Add(new DemoVectorCleanupRecord
                {
                    IndexGenerationId = generation.Id,
                    CompletedAtUtc = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Removed vectors for retired index generation {IndexGenerationId}.",
                    generation.Id);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Could not clean vectors for retired index generation {IndexGenerationId}; it will be retried.",
                    generation.Id);
            }
        }
    }
}
