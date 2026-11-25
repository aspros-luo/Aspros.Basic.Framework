namespace Aspros.Base.Framework.Infrastructure.Recommendation;

/// <summary>
/// Requests recommendations for one business object type. Object IDs remain opaque;
/// the business service is responsible for loading the corresponding entities.
/// </summary>
public sealed record RecommendationRequest
{
    public string UserId { get; init; } = string.Empty;

    public string ObjectType { get; init; } = string.Empty;

    public string? TenantId { get; init; }

    public string? Scene { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    /// <summary>
    /// Maximum number of candidates loaded before ranking. This is a bounded recall window,
    /// not the total number of objects in the business catalog.
    /// </summary>
    public int CandidateLimit { get; init; } = 500;

    public IReadOnlyCollection<string> ExcludedObjectIds { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Optional, non-sensitive scene parameters understood by the registered provider.
    /// Tenant authorization must use TenantId and the provider's trusted data boundary.
    /// </summary>
    public IReadOnlyDictionary<string, string> Context { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed record RecommendationCandidateQuery(
    string UserId,
    string ObjectType,
    string? TenantId,
    string? Scene,
    int CandidateLimit,
    IReadOnlyCollection<string> ExcludedObjectIds,
    IReadOnlyDictionary<string, string> Context);

/// <summary>
/// A sparse, non-negative feature vector. Feature names must have the same meaning
/// for a user profile and candidates of the same ObjectType.
/// </summary>
public sealed record RecommendationUserProfile(
    IReadOnlyDictionary<string, double> Features);

/// <summary>
/// Scores supplied by the provider must be normalized to the inclusive range [0, 1].
/// Candidates must be unique by ObjectId within one provider response.
/// </summary>
public sealed record RecommendationCandidate(
    string ObjectId,
    IReadOnlyDictionary<string, double> Features,
    double QualityScore,
    double PopularityScore,
    double FreshnessScore);

public sealed record RecommendationItemScore(
    string ObjectId,
    int Rank,
    double Score,
    double SimilarityScore,
    double QualityScore,
    double PopularityScore,
    double FreshnessScore);

/// <summary>
/// TotalCandidateCount describes the bounded candidate set returned by the provider,
/// after exclusions; it is not a count of the entire business catalog.
/// </summary>
public sealed record RecommendationResult(
    string ObjectType,
    int Page,
    int PageSize,
    int TotalCandidateCount,
    IReadOnlyList<RecommendationItemScore> Items);
