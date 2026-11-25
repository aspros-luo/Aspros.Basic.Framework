using Microsoft.Extensions.Options;

namespace Aspros.Base.Framework.Infrastructure.Recommendation;

/// <summary>
/// Provider-agnostic ranking orchestration. Storage and candidate recall stay outside
/// the Framework; this service only validates, scores, orders and pages the candidate set.
/// </summary>
public sealed class RecommendationService : IRecommendationService
{
    private const int MaximumPageSize = 100;
    private const int MaximumCandidateLimit = 10_000;

    private readonly IReadOnlyDictionary<string, IRecommendationProvider> _providers;
    private readonly RecommendationOptions _options;

    public RecommendationService(
        IEnumerable<IRecommendationProvider> providers,
        IOptions<RecommendationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(options);

        var providerMap = new Dictionary<string, IRecommendationProvider>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var provider in providers)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentException.ThrowIfNullOrWhiteSpace(provider.ObjectType);

            var objectType = provider.ObjectType.Trim();
            if (!providerMap.TryAdd(objectType, provider))
            {
                throw new InvalidOperationException(
                    $"More than one recommendation provider is registered for object type '{objectType}'.");
            }
        }

        _providers = providerMap;
        _options = options.Value;
        _options.Validate();
    }

    public async Task<RecommendationResult> RecommendAsync(
        RecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ObjectType);

        if (request.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Page));
        }

        if (request.PageSize is < 1 or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.PageSize),
                $"Page size must be between 1 and {MaximumPageSize}.");
        }

        if (request.CandidateLimit is < 1 or > MaximumCandidateLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.CandidateLimit),
                $"Candidate limit must be between 1 and {MaximumCandidateLimit}.");
        }

        var offset = checked((request.Page - 1) * request.PageSize);
        if ((long)offset + request.PageSize > request.CandidateLimit)
        {
            throw new ArgumentException(
                "CandidateLimit must cover the requested page.",
                nameof(request));
        }

        ArgumentNullException.ThrowIfNull(request.Context);
        ArgumentNullException.ThrowIfNull(request.ExcludedObjectIds);

        var objectType = request.ObjectType.Trim();
        if (!_providers.TryGetValue(objectType, out var provider))
        {
            throw new InvalidOperationException(
                $"No recommendation provider is registered for object type '{objectType}'.");
        }

        var profile = await provider.GetUserProfileAsync(
            request.UserId,
            request.TenantId,
            request.Scene,
            request.Context,
            cancellationToken);

        if (profile is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(profile.FeatureSchemaVersion);
            ValidateFeatures(profile.Features, "user profile");
        }

        var query = new RecommendationCandidateQuery(
            request.UserId,
            objectType,
            request.TenantId,
            request.Scene,
            request.CandidateLimit,
            request.ExcludedObjectIds,
            request.Context);

        var candidates = await provider.GetCandidatesAsync(query, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Recommendation provider '{objectType}' returned a null candidate collection.");

        if (candidates.Count > request.CandidateLimit)
        {
            throw new InvalidOperationException(
                $"Recommendation provider '{objectType}' returned {candidates.Count} candidates, exceeding the requested limit of {request.CandidateLimit}.");
        }

        var hasProfile = profile is { Features.Count: > 0 }
            && profile.Features.Values.Any(value => value > 0);
        var excludedIds = new HashSet<string>(
            request.ExcludedObjectIds,
            StringComparer.Ordinal);
        var ranked = new List<RecommendationItemScore>(candidates.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        string? candidateSchemaVersion = null;

        foreach (var candidate in candidates)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            ArgumentException.ThrowIfNullOrWhiteSpace(candidate.ObjectId);
            ArgumentException.ThrowIfNullOrWhiteSpace(candidate.FeatureSchemaVersion);

            candidateSchemaVersion ??= candidate.FeatureSchemaVersion;
            if (!StringComparer.Ordinal.Equals(
                candidateSchemaVersion,
                candidate.FeatureSchemaVersion))
            {
                throw new InvalidOperationException(
                    $"Recommendation provider '{objectType}' returned candidates with different feature schema versions.");
            }

            if (profile is not null && !StringComparer.Ordinal.Equals(
                profile.FeatureSchemaVersion,
                candidate.FeatureSchemaVersion))
            {
                throw new InvalidOperationException(
                    $"User profile schema '{profile.FeatureSchemaVersion}' does not match candidate schema '{candidate.FeatureSchemaVersion}' for object type '{objectType}'.");
            }

            if (!seenIds.Add(candidate.ObjectId))
            {
                throw new InvalidOperationException(
                    $"Recommendation provider '{objectType}' returned duplicate object ID '{candidate.ObjectId}'.");
            }

            ValidateFeatures(candidate.Features, $"candidate '{candidate.ObjectId}'");
            ValidateNormalizedScore(candidate.QualityScore, nameof(candidate.QualityScore));
            ValidateNormalizedScore(candidate.PopularityScore, nameof(candidate.PopularityScore));
            ValidateNormalizedScore(candidate.FreshnessScore, nameof(candidate.FreshnessScore));

            if (excludedIds.Contains(candidate.ObjectId))
            {
                continue;
            }

            var similarity = hasProfile
                ? CalculateCosineSimilarity(profile!.Features, candidate.Features)
                : 0d;

            var score = CalculateScore(candidate, similarity, hasProfile);

            ranked.Add(new RecommendationItemScore(
                candidate.ObjectId,
                0,
                score,
                similarity,
                candidate.QualityScore,
                candidate.PopularityScore,
                candidate.FreshnessScore));
        }

        var ordered = ranked
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.SimilarityScore)
            .ThenByDescending(item => item.QualityScore)
            .ThenByDescending(item => item.PopularityScore)
            .ThenByDescending(item => item.FreshnessScore)
            .ThenBy(item => item.ObjectId, StringComparer.Ordinal)
            .Select((item, index) => item with { Rank = index + 1 })
            .ToArray();

        var pageItems = ordered
            .Skip(offset)
            .Take(request.PageSize)
            .ToArray();

        return new RecommendationResult(
            objectType,
            request.Page,
            request.PageSize,
            ordered.Length,
            pageItems);
    }

    private double CalculateScore(
        RecommendationCandidate candidate,
        double similarity,
        bool hasProfile)
    {
        var activeWeight = _options.QualityWeight
            + _options.PopularityWeight
            + _options.FreshnessWeight;

        var weightedScore = candidate.QualityScore * _options.QualityWeight
            + candidate.PopularityScore * _options.PopularityWeight
            + candidate.FreshnessScore * _options.FreshnessWeight;

        if (hasProfile)
        {
            activeWeight += _options.SimilarityWeight;
            weightedScore += similarity * _options.SimilarityWeight;
        }

        return activeWeight == 0 ? 0 : weightedScore / activeWeight;
    }

    private static double CalculateCosineSimilarity(
        IReadOnlyDictionary<string, double> left,
        IReadOnlyDictionary<string, double> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        // Scale each vector before squaring so very large but finite feature values
        // cannot overflow while calculating norms or the dot product.
        var leftScale = left.Values.Max();
        var rightScale = right.Values.Max();

        if (leftScale == 0 || rightScale == 0)
        {
            return 0;
        }

        var leftNormSquared = left.Values.Sum(value =>
        {
            var normalized = value / leftScale;
            return normalized * normalized;
        });

        var rightNormSquared = right.Values.Sum(value =>
        {
            var normalized = value / rightScale;
            return normalized * normalized;
        });

        var smaller = left.Count <= right.Count ? left : right;
        var larger = ReferenceEquals(smaller, left) ? right : left;
        var dotProduct = 0d;

        foreach (var (feature, value) in smaller)
        {
            if (larger.TryGetValue(feature, out var otherValue))
            {
                dotProduct += (value / leftScale) * (otherValue / rightScale);
            }
        }

        return Math.Clamp(
            dotProduct / (Math.Sqrt(leftNormSquared) * Math.Sqrt(rightNormSquared)),
            0d,
            1d);
    }

    private static void ValidateFeatures(
        IReadOnlyDictionary<string, double> features,
        string source)
    {
        ArgumentNullException.ThrowIfNull(features);

        foreach (var (name, value) in features)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            if (!double.IsFinite(value) || value < 0)
            {
                throw new ArgumentException(
                    $"Feature '{name}' in {source} must be finite and non-negative.",
                    nameof(features));
            }
        }
    }

    private static void ValidateNormalizedScore(double value, string name)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                name,
                value,
                "Recommendation candidate scores must be normalized to [0, 1].");
        }
    }
}
