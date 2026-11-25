using Aspros.Base.Framework.Infrastructure.Recommendation;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aspros.Basic.Framework.IntegrationTests;

public sealed class RecommendationServiceTests
{
    [Fact]
    public async Task RecommendAsync_ProducesDifferentRankingsForDifferentUsers()
    {
        var provider = new FakeRecommendationProvider(
            "product",
            new Dictionary<string, RecommendationUserProfile>(StringComparer.Ordinal)
            {
                ["user-a"] = new("v1", new Dictionary<string, double> { ["coffee"] = 1 }),
                ["user-b"] = new("v1", new Dictionary<string, double> { ["running"] = 1 })
            },
            [
                Candidate("coffee-machine", "coffee"),
                Candidate("running-shoes", "running")
            ]);

        var service = CreateService(provider);

        var userA = await service.RecommendAsync(Request("user-a"));
        var userB = await service.RecommendAsync(Request("user-b"));

        Assert.Equal("coffee-machine", userA.Items[0].ObjectId);
        Assert.Equal("running-shoes", userB.Items[0].ObjectId);
        Assert.Equal(1d, userA.Items[0].SimilarityScore);
        Assert.Equal(1d, userB.Items[0].SimilarityScore);
    }

    [Fact]
    public async Task RecommendAsync_CosineSimilarityDoesNotOverflowForLargeFiniteFeatures()
    {
        var provider = new FakeRecommendationProvider(
            "product",
            new Dictionary<string, RecommendationUserProfile>
            {
                ["user"] = new("v1", new Dictionary<string, double> { ["feature"] = 1e308 })
            },
            [
                new RecommendationCandidate(
                    "matching",
                    "v1",
                    new Dictionary<string, double> { ["feature"] = 1e308 },
                    QualityScore: 0.5,
                    PopularityScore: 0.5,
                    FreshnessScore: 0.5)
            ]);

        var result = await CreateService(provider).RecommendAsync(Request("user"));

        Assert.Equal(1d, result.Items[0].SimilarityScore);
    }

    [Fact]
    public async Task RecommendAsync_UsesNonPersonalizedSignalsWhenProfileIsMissing()
    {
        var provider = new FakeRecommendationProvider(
            "merchant",
            new Dictionary<string, RecommendationUserProfile>(),
            [
                new RecommendationCandidate(
                    "high-quality",
                    "v1",
                    new Dictionary<string, double>(),
                    QualityScore: 1,
                    PopularityScore: 0,
                    FreshnessScore: 0),
                new RecommendationCandidate(
                    "popular",
                    "v1",
                    new Dictionary<string, double>(),
                    QualityScore: 0,
                    PopularityScore: 1,
                    FreshnessScore: 1)
            ]);

        var result = await CreateService(provider).RecommendAsync(
            Request("anonymous", "merchant"));

        Assert.Equal("high-quality", result.Items[0].ObjectId);
        Assert.Equal(0d, result.Items[0].SimilarityScore);
    }

    [Fact]
    public async Task RecommendAsync_TreatsZeroVectorAsColdStart()
    {
        var provider = new FakeRecommendationProvider(
            "product",
            new Dictionary<string, RecommendationUserProfile>
            {
                ["user"] = new("v1", new Dictionary<string, double> { ["coffee"] = 0 })
            },
            [
                new RecommendationCandidate(
                    "quality-first",
                    "v1",
                    new Dictionary<string, double> { ["coffee"] = 1 },
                    QualityScore: 1,
                    PopularityScore: 0,
                    FreshnessScore: 0),
                new RecommendationCandidate(
                    "popular",
                    "v1",
                    new Dictionary<string, double> { ["coffee"] = 1 },
                    QualityScore: 0,
                    PopularityScore: 1,
                    FreshnessScore: 1)
            ]);

        var result = await CreateService(provider).RecommendAsync(Request("user"));

        Assert.Equal("quality-first", result.Items[0].ObjectId);
        Assert.Equal(0d, result.Items[0].SimilarityScore);
    }

    [Fact]
    public async Task RecommendAsync_AppliesExclusionsBeforeStablePagination()
    {
        var provider = new FakeRecommendationProvider(
            "article",
            new Dictionary<string, RecommendationUserProfile>
            {
                ["reader"] = new("v1", new Dictionary<string, double> { ["topic"] = 1 })
            },
            [
                Candidate("article-a", "topic"),
                Candidate("article-b", "topic"),
                Candidate("article-c", "topic")
            ]);

        var request = Request("reader", "article") with
        {
            Page = 2,
            PageSize = 1,
            CandidateLimit = 3,
            ExcludedObjectIds = ["article-a"]
        };

        var result = await CreateService(provider).RecommendAsync(request);

        Assert.Equal(2, result.TotalCandidateCount);
        Assert.Single(result.Items);
        Assert.Equal("article-c", result.Items[0].ObjectId);
        Assert.Equal(2, result.Items[0].Rank);
    }

    [Fact]
    public async Task RecommendAsync_RejectsMismatchedFeatureSchemaVersions()
    {
        var provider = new FakeRecommendationProvider(
            "product",
            new Dictionary<string, RecommendationUserProfile>
            {
                ["user"] = new("v1", new Dictionary<string, double> { ["feature"] = 1 })
            },
            [
                new RecommendationCandidate(
                    "item",
                    "v2",
                    new Dictionary<string, double> { ["feature"] = 1 },
                    QualityScore: 0.5,
                    PopularityScore: 0.5,
                    FreshnessScore: 0.5)
            ]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(provider).RecommendAsync(Request("user")));
    }

    [Fact]
    public async Task RecommendAsync_RejectsUnknownObjectTypes()
    {
        var service = CreateService(new FakeRecommendationProvider(
            "product",
            new Dictionary<string, RecommendationUserProfile>(),
            []));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecommendAsync(Request("user", "unknown")));
    }

    private static RecommendationService CreateService(
        params IRecommendationProvider[] providers)
        => new(providers, Options.Create(new RecommendationOptions()));

    private static RecommendationRequest Request(
        string userId,
        string objectType = "product")
        => new()
        {
            UserId = userId,
            ObjectType = objectType,
            CandidateLimit = 10
        };

    private static RecommendationCandidate Candidate(
        string objectId,
        string feature)
        => new(
            objectId,
            "v1",
            new Dictionary<string, double> { [feature] = 1 },
            QualityScore: 0.5,
            PopularityScore: 0.5,
            FreshnessScore: 0.5);

    private sealed class FakeRecommendationProvider(
        string objectType,
        IReadOnlyDictionary<string, RecommendationUserProfile> profiles,
        IReadOnlyList<RecommendationCandidate> candidates)
        : IRecommendationProvider
    {
        public string ObjectType { get; } = objectType;

        public Task<RecommendationUserProfile?> GetUserProfileAsync(
            string userId,
            string? tenantId,
            string? scene,
            IReadOnlyDictionary<string, string> context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                profiles.TryGetValue(userId, out var profile)
                    ? profile
                    : null);

        public Task<IReadOnlyList<RecommendationCandidate>> GetCandidatesAsync(
            RecommendationCandidateQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(candidates);
    }
}
