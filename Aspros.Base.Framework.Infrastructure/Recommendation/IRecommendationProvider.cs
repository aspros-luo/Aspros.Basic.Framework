namespace Aspros.Base.Framework.Infrastructure.Recommendation;

/// <summary>
/// Supplies the user feature vector and a bounded candidate set for one business object type.
/// Implementations own storage, recall, eligibility filters and tenant isolation.
/// </summary>
public interface IRecommendationProvider
{
    string ObjectType { get; }

    Task<RecommendationUserProfile?> GetUserProfileAsync(
        string userId,
        string? tenantId,
        string? scene,
        IReadOnlyDictionary<string, string> context,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecommendationCandidate>> GetCandidatesAsync(
        RecommendationCandidateQuery query,
        CancellationToken cancellationToken = default);
}
