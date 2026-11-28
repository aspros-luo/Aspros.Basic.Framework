namespace Rag.KnowledgeBase.Api;

/// <summary>
/// Demo-owned durable receipt that a retired generation's external vector points were deleted.
/// The receipt is written only after the idempotent Qdrant delete succeeds.
/// </summary>
public sealed class DemoVectorCleanupRecord
{
    public Guid IndexGenerationId { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
}
