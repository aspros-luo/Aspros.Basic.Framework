using Aspros.Base.Framework.Algorithms;
using Aspros.Base.Framework.ClickHouse;
using ClickHouse.Driver;
using ClickHouse.Driver.ADO.Parameters;
using ClickHouse.Driver.Utility;
using Microsoft.EntityFrameworkCore;
using Xr.Category.Infrastructure;
using CategoryEntity = Xr.Category.Domain.Category;

namespace Xr.Category.Api.Analytics;

public static class CategoryPersonalizationValidationExtensions
{
    public static IServiceCollection AddCategoryPersonalizationValidation(
        this IServiceCollection services)
    {
        services.AddScoped<CategoryPersonalizationValidationService>();
        return services;
    }

    public static IEndpointRouteBuilder MapCategoryPersonalizationValidation(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/_dev/validation/personalization",
            async (
                CategoryPersonalizationValidationService service,
                CancellationToken cancellationToken) =>
            {
                var result = await service.StartAsync(cancellationToken);
                return Results.Ok(result);
            });

        endpoints.MapGet(
            "/_dev/validation/personalization/{runId}",
            async (
                string runId,
                CategoryPersonalizationValidationService service,
                CancellationToken cancellationToken) =>
            {
                var result = await service.GetResultAsync(runId, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            });

        return endpoints;
    }
}

/// <summary>
/// Development-only validation of per-user category ranking against real ClickHouse data.
/// The sample events are synthetic; the category master data is read from MySQL.
/// </summary>
public sealed class CategoryPersonalizationValidationService(
    CategoryDbContext dbContext,
    IClickHouseClient clickHouse)
{
    private const string EventsTable =
        "aspros_analytics.category_behavior_events";

    private const string UserA = "validation-user-a";
    private const string UserB = "validation-user-b";

    private readonly CategoryDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private readonly IClickHouseClient _clickHouse =
        clickHouse ?? throw new ArgumentNullException(nameof(clickHouse));

    public async Task<CategoryPersonalizationValidationStarted> StartAsync(
        CancellationToken cancellationToken)
    {
        // Use two stable, dedicated fixture rows instead of whichever business categories
        // happen to sort first. Repeated validation runs update these rows instead of polluting
        // the category table with new random test data.
        var fixtureDefinitions = new[]
        {
            (Code: "FRAMEWORK-VALIDATION-PERSONALIZATION-A", Name: "Validation Category A", SortOrder: 900001),
            (Code: "FRAMEWORK-VALIDATION-PERSONALIZATION-B", Name: "Validation Category B", SortOrder: 900002)
        };
        var fixtureCodes = fixtureDefinitions.Select(item => item.Code).ToArray();
        var existingFixtures = await _dbContext.Categories
            .Where(category => fixtureCodes.Contains(category.Code))
            .ToDictionaryAsync(category => category.Code, cancellationToken);

        var fixtureEntities = new List<CategoryEntity>(fixtureDefinitions.Length);
        foreach (var definition in fixtureDefinitions)
        {
            if (!existingFixtures.TryGetValue(definition.Code, out var entity))
            {
                entity = new CategoryEntity(
                    definition.Code,
                    definition.Name,
                    parentId: null,
                    definition.SortOrder,
                    isEnabled: true);
                _dbContext.Categories.Add(entity);
            }
            else
            {
                entity.Deleted = false;
                entity.Update(
                    definition.Code,
                    definition.Name,
                    parentId: null,
                    definition.SortOrder,
                    isEnabled: true);
            }

            fixtureEntities.Add(entity);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        var categories = fixtureEntities
            .Select(entity => new CategoryPersonalizationCategory(entity.Id, entity.Name))
            .ToArray();

        var runId = Guid.NewGuid().ToString("N");
        var eventTime = DateTime.UtcNow;
        var rows = new List<object[]>();
        var logicalEventCount = 0;

        object[] CreateEvent(
            string userId,
            long categoryId,
            string eventType,
            int secondsOffset)
        {
            logicalEventCount++;

            return
            [
                Guid.NewGuid().ToString("N"),
                runId,
                userId,
                checked((ulong)categoryId),
                eventType,
                eventTime.AddSeconds(secondsOffset)
            ];
        }

        // User A prefers category A: view (1) + click (3) + purchase (8) = 12.
        rows.Add(CreateEvent(UserA, categories[0].Id, "view", 1));
        rows.Add(CreateEvent(UserA, categories[0].Id, "click", 2));
        var duplicatedPurchase = CreateEvent(
            UserA,
            categories[0].Id,
            "purchase",
            3);
        rows.Add(duplicatedPurchase);
        rows.Add(duplicatedPurchase.ToArray());

        rows.Add(CreateEvent(UserA, categories[1].Id, "view", 4));
        rows.Add(CreateEvent(UserA, categories[1].Id, "view", 5));

        // User B has the opposite preference distribution.
        rows.Add(CreateEvent(UserB, categories[0].Id, "view", 6));
        rows.Add(CreateEvent(UserB, categories[0].Id, "view", 7));
        rows.Add(CreateEvent(UserB, categories[1].Id, "view", 8));
        rows.Add(CreateEvent(UserB, categories[1].Id, "click", 9));
        rows.Add(CreateEvent(UserB, categories[1].Id, "purchase", 10));

        var insertedRows = await _clickHouse.InsertBinaryAsync(
            EventsTable,
            ["event_id", "run_id", "user_id", "category_id", "event_type", "event_time"],
            rows,
            cancellationToken: cancellationToken);

        if (insertedRows != rows.Count)
        {
            throw new InvalidOperationException(
                $"Expected {rows.Count} physical ClickHouse rows, but inserted {insertedRows}.");
        }

        return new CategoryPersonalizationValidationStarted(
            runId,
            insertedRows,
            logicalEventCount,
            [UserA, UserB],
            categories,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["view"] = 1,
                ["click"] = 3,
                ["purchase"] = 8
            });
    }

    public async Task<CategoryPersonalizationValidationResult?> GetResultAsync(
        string runId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(runId, "N", out var parsedRunId))
        {
            return null;
        }

        var normalizedRunId = parsedRunId.ToString("N");
        const string rankingSql = """
            WITH deduplicated_events AS
            (
                SELECT
                    event_id,
                    any(user_id) AS user_id,
                    any(category_id) AS category_id,
                    any(event_type) AS event_type,
                    min(event_time) AS event_time
                FROM aspros_analytics.category_behavior_events
                WHERE run_id = {run_id:String}
                GROUP BY event_id
            ),
            category_scores AS
            (
                SELECT
                    user_id,
                    category_id,
                    toInt64(sum(multiIf(
                        event_type = 'purchase', 8,
                        event_type = 'click', 3,
                        event_type = 'view', 1,
                        0
                    ))) AS score,
                    toUInt64(count()) AS event_count,
                    max(event_time) AS last_event_time
                FROM deduplicated_events
                GROUP BY user_id, category_id
            )
            SELECT
                user_id,
                category_id,
                score,
                event_count
            FROM category_scores
            ORDER BY
                user_id ASC,
                score DESC,
                last_event_time DESC,
                category_id ASC
            """;

        var parameters = new ClickHouseParameterCollection();
        parameters.AddParameter("run_id", normalizedRunId);

        var rawScores = new List<RawCategoryScore>();
        await using (var reader = await _clickHouse.ExecuteReaderAsync(
            rankingSql,
            parameters,
            cancellationToken: cancellationToken))
        {
            while (reader.Read())
            {
                rawScores.Add(new RawCategoryScore(
                    reader.GetString(0),
                    reader.GetUInt64(1),
                    reader.GetInt64(2),
                    reader.GetUInt64(3)));
            }
        }

        if (rawScores.Count == 0)
        {
            return null;
        }

        var categoryIds = rawScores
            .Select(score => checked((long)score.CategoryId))
            .Distinct()
            .OrderBy(categoryId => categoryId)
            .ToArray();

        var categoryNames = await _dbContext.Categories
            .AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .Select(category => new { category.Id, category.Name })
            .ToDictionaryAsync(
                category => category.Id,
                category => category.Name,
                cancellationToken);

        var scoresByUser = rawScores
            .GroupBy(score => score.UserId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(score => checked((long)score.CategoryId)),
                StringComparer.Ordinal);

        var popularityScores = scoresByUser.Values
            .SelectMany(userScores => userScores.Values)
            .GroupBy(score => checked((long)score.CategoryId))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(score => score.Score));

        var rankings = new List<CategoryPersonalizationScore>(scoresByUser.Count * categoryIds.Length);

        foreach (var (userId, scores) in scoresByUser)
        {
            var userVector = categoryIds
                .Select(categoryId => scores.TryGetValue(categoryId, out var score)
                    ? (double)score.Score
                    : 0d)
                .ToArray();

            var candidates = new List<CategoryPersonalizationScore>(categoryIds.Length);
            for (var index = 0; index < categoryIds.Length; index++)
            {
                var categoryId = categoryIds[index];
                scores.TryGetValue(categoryId, out var score);

                // Each category is a one-hot candidate vector; the user vector
                // contains behavior-weighted category interests aggregated in ClickHouse.
                var candidateVector = new double[categoryIds.Length];
                candidateVector[index] = 1d;
                var similarity = VectorAlgorithms.CosineSimilarity(
                    userVector,
                    candidateVector);

                var categoryName = categoryNames.TryGetValue(
                    categoryId,
                    out var name)
                    ? name
                    : $"Category {categoryId}";

                candidates.Add(new CategoryPersonalizationScore(
                    userId,
                    categoryId,
                    categoryName,
                    score?.Score ?? 0,
                    score?.EventCount ?? 0,
                    similarity,
                    FusionScore: 0d,
                    SourceCount: 0,
                    Rank: 0));
            }

            // Keep personalized relevance and global popularity as independent ranking
            // sources. RRF combines their relative positions without assuming their raw
            // scores share the same scale.
            var personalized = candidates
                .OrderByDescending(item => item.SimilarityScore)
                .ThenByDescending(item => item.Score)
                .ThenBy(item => item.CategoryId)
                .ToArray();

            var popular = candidates
                .OrderByDescending(item => popularityScores.GetValueOrDefault(item.CategoryId))
                .ThenBy(item => item.CategoryId)
                .ToArray();

            var fused = RankingAlgorithms.ReciprocalRankFusion(
                [
                    new RankingSource<CategoryPersonalizationScore>(
                        "personalized",
                        personalized),
                    new RankingSource<CategoryPersonalizationScore>(
                        "popular",
                        popular)
                ],
                item => item.CategoryId);

            rankings.AddRange(fused.Select(item => item.Item with
            {
                FusionScore = item.Score,
                SourceCount = item.SourceCount,
                Rank = item.Rank
            }));
        }

        var physicalRows = await CountRowsAsync(
            "SELECT count() FROM aspros_analytics.category_behavior_events WHERE run_id = {run_id:String}",
            normalizedRunId,
            cancellationToken);

        var logicalEvents = await CountRowsAsync(
            "SELECT uniqExact(event_id) FROM aspros_analytics.category_behavior_events WHERE run_id = {run_id:String}",
            normalizedRunId,
            cancellationToken);

        var userARankings = rankings
            .Where(item => item.UserId == UserA)
            .OrderBy(item => item.Rank)
            .ToArray();

        var userBRankings = rankings
            .Where(item => item.UserId == UserB)
            .OrderBy(item => item.Rank)
            .ToArray();

        var passed =
            physicalRows == 11 &&
            logicalEvents == 10 &&
            userARankings.Length == 2 &&
            userBRankings.Length == 2 &&
            userARankings[0].CategoryId == userBRankings[1].CategoryId &&
            userARankings[1].CategoryId == userBRankings[0].CategoryId &&
            userARankings[0].SimilarityScore > userARankings[1].SimilarityScore &&
            userBRankings[0].SimilarityScore > userBRankings[1].SimilarityScore;

        return new CategoryPersonalizationValidationResult(
            passed,
            normalizedRunId,
            physicalRows,
            logicalEvents,
            rankings,
            "view=1, click=3, purchase=8",
            "Deduplicate by event_id before aggregating");
    }

    private async Task<long> CountRowsAsync(
        string sql,
        string runId,
        CancellationToken cancellationToken)
    {
        var parameters = new ClickHouseParameterCollection();
        parameters.AddParameter("run_id", runId);

        var value = await _clickHouse.ExecuteScalarAsync(
            sql,
            parameters,
            cancellationToken: cancellationToken);
        return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed record RawCategoryScore(
        string UserId,
        ulong CategoryId,
        long Score,
        ulong EventCount);
}

public sealed record CategoryPersonalizationCategory(long Id, string Name);

public sealed record CategoryPersonalizationValidationStarted(
    string RunId,
    long PhysicalRowsInserted,
    int ExpectedLogicalEvents,
    IReadOnlyList<string> UserIds,
    IReadOnlyList<CategoryPersonalizationCategory> Categories,
    IReadOnlyDictionary<string, int> Weights);

public sealed record CategoryPersonalizationScore(
    string UserId,
    long CategoryId,
    string CategoryName,
    long Score,
    ulong EventCount,
    double SimilarityScore,
    double FusionScore,
    int SourceCount,
    int Rank);

public sealed record CategoryPersonalizationValidationResult(
    bool Passed,
    string RunId,
    long PhysicalRows,
    long LogicalEvents,
    IReadOnlyList<CategoryPersonalizationScore> Rankings,
    string ScoringRule,
    string DeduplicationRule);
