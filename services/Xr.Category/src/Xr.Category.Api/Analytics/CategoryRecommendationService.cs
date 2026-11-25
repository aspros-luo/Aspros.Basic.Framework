using ClickHouse.Driver;
using ClickHouse.Driver.Utility;
using Aspros.Base.Framework.Algorithms;
using Aspros.Base.Framework.ClickHouse;
using ClickHouse.Driver.ADO.Parameters;
using Microsoft.EntityFrameworkCore;
using Xr.Category.Application;
using Xr.Category.Infrastructure;

namespace Xr.Category.Api.Analytics;

/// <summary>
/// Builds Category recommendations from real, deduplicated behavior events in ClickHouse.
/// Candidate eligibility and Category metadata remain owned by the MySQL-backed service.
/// </summary>
public sealed class CategoryRecommendationService(
    CategoryDbContext dbContext,
    IClickHouseClient clickHouse) : ICategoryRecommendationService
{
    private const string EventsTable =
        "aspros_analytics.category_behavior_events";

    private const int DecayHalfLifeDays = 7;

    private readonly CategoryDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private readonly IClickHouseClient _clickHouse =
        clickHouse ?? throw new ArgumentNullException(nameof(clickHouse));

    public async Task<CategoryRecommendationResponse> RecommendAsync(
        CategoryRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserId);

        if (request.UserId.Length > 128)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "UserId cannot exceed 128 characters.");
        }

        if (request.WindowDays is < 1 or > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "WindowDays must be between 1 and 90.");
        }

        if (request.Take is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Take must be between 1 and 100.");
        }

        var categories = await _dbContext.Categories
            .AsNoTracking()
            .Where(category => category.IsEnabled && !category.Deleted)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Id)
            .Select(category => new CategoryCandidate(
                category.Id,
                category.Name,
                category.SortOrder))
            .ToArrayAsync(cancellationToken);

        var asOfUtc = DateTime.UtcNow;
        var windowStartUtc = asOfUtc.AddDays(-request.WindowDays);

        if (categories.Length == 0)
        {
            return CreateResponse(
                request,
                asOfUtc,
                windowStartUtc,
                "catalog-empty",
                []);
        }

        var behaviorScores = await ReadBehaviorScoresAsync(
            request.UserId,
            windowStartUtc,
            asOfUtc,
            cancellationToken);

        // No analytical history is a normal cold-start case, not a server error.
        // Fall back to the stable business catalog order until behavior arrives.
        if (behaviorScores.Count == 0)
        {
            return CreateResponse(
                request,
                asOfUtc,
                windowStartUtc,
                "catalog-fallback",
                CreateCatalogFallback(categories, request.Take));
        }

        var candidates = categories
            .Select(category =>
            {
                behaviorScores.TryGetValue(category.Id, out var score);
                return new CategoryRecommendationCandidate(
                    category,
                    score?.PersonalizedScore ?? 0d,
                    score?.EventCount ?? 0,
                    score?.LastEventTimeUtc,
                    score?.PopularityScore ?? 0d);
            })
            .ToArray();

        if (!candidates.Any(candidate => candidate.PopularityScore > 0d))
        {
            return CreateResponse(
                request,
                asOfUtc,
                windowStartUtc,
                "catalog-fallback",
                CreateCatalogFallback(categories, request.Take));
        }

        var popularityRanking = candidates
            .OrderByDescending(candidate => candidate.PopularityScore)
            .ThenBy(candidate => candidate.Category.SortOrder)
            .ThenBy(candidate => candidate.Category.Id)
            .ToArray();

        var sources = new List<RankingSource<CategoryRecommendationCandidate>>(2);

        var personalizedRanking = candidates
            .Where(candidate => candidate.PersonalizedScore > 0d)
            .OrderByDescending(candidate => candidate.PersonalizedScore)
            .ThenByDescending(candidate => candidate.LastEventTimeUtc)
            .ThenBy(candidate => candidate.Category.SortOrder)
            .ThenBy(candidate => candidate.Category.Id)
            .ToArray();

        if (personalizedRanking.Length > 0)
        {
            sources.Add(new RankingSource<CategoryRecommendationCandidate>(
                "personalized",
                personalizedRanking));
        }

        sources.Add(new RankingSource<CategoryRecommendationCandidate>(
            "popularity",
            popularityRanking));

        var fused = RankingAlgorithms.ReciprocalRankFusion(
            sources,
            candidate => candidate.Category.Id);

        var items = fused
            .Take(request.Take)
            .Select(result => new CategoryRecommendationItem(
                result.Item.Category.Id,
                result.Item.Category.Name,
                result.Item.PersonalizedScore,
                result.Item.EventCount,
                result.Item.LastEventTimeUtc,
                result.Item.PopularityScore,
                result.Score,
                result.SourceCount,
                result.Rank))
            .ToArray();

        var mode = personalizedRanking.Length > 0
            ? "personalized-and-popular"
            : "popular-only";

        return CreateResponse(
            request,
            asOfUtc,
            windowStartUtc,
            mode,
            items);
    }

    private async Task<Dictionary<long, CategoryBehaviorScore>> ReadBehaviorScoresAsync(
        string userId,
        DateTime windowStartUtc,
        DateTime asOfUtc,
        CancellationToken cancellationToken)
    {
        var sql = $$"""
            WITH deduplicated_events AS
            (
                SELECT
                    event_id,
                    argMin(user_id, event_time) AS user_id,
                    argMin(category_id, event_time) AS category_id,
                    argMin(event_type, event_time) AS event_type,
                    min(event_time) AS event_time
                FROM {{EventsTable}}
                WHERE event_time >= {window_start:DateTime64(3, 'UTC')}
                  AND event_time <= {as_of:DateTime64(3, 'UTC')}
                GROUP BY event_id
            ),
            weighted_events AS
            (
                SELECT
                    user_id,
                    category_id,
                    event_time,
                    toFloat64(multiIf(
                        event_type = 'purchase', 8,
                        event_type = 'click', 3,
                        event_type = 'view', 1,
                        0
                    )) * exp(
                        -0.6931471805599453 * greatest(
                            dateDiff('second', event_time, {as_of:DateTime64(3, 'UTC')}),
                            0
                        ) / {{DecayHalfLifeDays * 86400}}.0
                    ) AS weighted_score
                FROM deduplicated_events
            ),
            user_category_scores AS
            (
                SELECT
                    user_id,
                    category_id,
                    sum(weighted_score) AS personalized_score,
                    toUInt64(count()) AS event_count,
                    max(event_time) AS last_event_time
                FROM weighted_events
                GROUP BY user_id, category_id
            ),
            global_popularity AS
            (
                SELECT
                    category_id,
                    sum(personalized_score) AS popularity_score
                FROM user_category_scores
                GROUP BY category_id
            )
            SELECT
                popularity.category_id,
                coalesce(user_score.personalized_score, 0.0) AS personalized_score,
                coalesce(user_score.event_count, toUInt64(0)) AS event_count,
                coalesce(
                    user_score.last_event_time,
                    toDateTime64('1970-01-01 00:00:00', 3, 'UTC')
                ) AS last_event_time,
                popularity.popularity_score
            FROM global_popularity AS popularity
            LEFT JOIN user_category_scores AS user_score
                ON user_score.category_id = popularity.category_id
               AND user_score.user_id = {user_id:String}
            ORDER BY popularity.category_id
            """;

        var parameters = new ClickHouseParameterCollection();
        parameters.AddParameter("window_start", windowStartUtc);
        parameters.AddParameter("as_of", asOfUtc);
        parameters.AddParameter("user_id", userId);

        var scores = new Dictionary<long, CategoryBehaviorScore>();
        await using var reader = await _clickHouse.ExecuteReaderAsync(
            sql,
            parameters,
            cancellationToken: cancellationToken);

        while (reader.Read())
        {
            var categoryId = checked((long)reader.GetUInt64(0));
            var personalizedScore = reader.GetDouble(1);
            var eventCount = checked((long)reader.GetUInt64(2));
            var lastEventTime = reader.GetDateTime(3);
            var popularityScore = reader.GetDouble(4);

            scores[categoryId] = new CategoryBehaviorScore(
                personalizedScore,
                eventCount,
                eventCount == 0 ? null : DateTime.SpecifyKind(lastEventTime, DateTimeKind.Utc),
                popularityScore);
        }

        return scores;
    }

    private static IReadOnlyList<CategoryRecommendationItem> CreateCatalogFallback(
        IReadOnlyList<CategoryCandidate> categories,
        int take)
        => categories
            .Take(take)
            .Select((category, index) => new CategoryRecommendationItem(
                category.Id,
                category.Name,
                PersonalizedScore: 0d,
                BehaviorEventCount: 0,
                LastBehaviorAtUtc: null,
                PopularityScore: 0d,
                FusionScore: 0d,
                SourceCount: 0,
                Rank: index + 1))
            .ToArray();

    private static CategoryRecommendationResponse CreateResponse(
        CategoryRecommendationRequest request,
        DateTime asOfUtc,
        DateTime windowStartUtc,
        string mode,
        IReadOnlyList<CategoryRecommendationItem> items)
        => new(
            request.UserId,
            request.WindowDays,
            windowStartUtc,
            asOfUtc,
            $"view=1, click=3, purchase=8; exponential time decay with a {DecayHalfLifeDays}-day half-life",
            "Deduplicate by event_id before scoring",
            mode,
            items);

    private sealed record CategoryCandidate(
        long Id,
        string Name,
        int SortOrder);

    private sealed record CategoryRecommendationCandidate(
        CategoryCandidate Category,
        double PersonalizedScore,
        long EventCount,
        DateTime? LastEventTimeUtc,
        double PopularityScore);

    private sealed record CategoryBehaviorScore(
        double PersonalizedScore,
        long EventCount,
        DateTime? LastEventTimeUtc,
        double PopularityScore);
}
