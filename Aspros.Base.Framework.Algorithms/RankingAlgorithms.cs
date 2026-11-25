namespace Aspros.Base.Framework.Algorithms;

/// <summary>
/// An ordered candidate list produced by one independent recommendation signal or recall source.
/// Items earlier in the list have a stronger position within that source.
/// </summary>
public sealed record RankingSource<T>(
    string Name,
    IReadOnlyList<T> Items,
    double Weight = 1d);

/// <summary>
/// A candidate produced by reciprocal-rank fusion across one or more sources.
/// </summary>
public sealed record FusedRankingItem<T, TKey>(
    T Item,
    TKey Key,
    double Score,
    int Rank,
    int SourceCount)
    where TKey : notnull;

/// <summary>
/// General-purpose ranking fusion algorithms. Candidate retrieval, eligibility filtering,
/// feature generation and business-specific scoring remain the responsibility of the caller.
/// </summary>
public static class RankingAlgorithms
{
    /// <summary>
    /// Combines independently ordered candidate lists using weighted reciprocal-rank fusion.
    /// Duplicate keys within one source contribute only their first (best) position.
    /// Across sources, the candidate instance with the strongest individual contribution is
    /// retained. Equal final scores preserve first-seen order.
    /// </summary>
    /// <remarks>
    /// The returned score is a fusion score, not a probability and is not normalized to [0, 1].
    /// A larger rank constant reduces the influence of differences near the top of each source.
    /// </remarks>
    public static IReadOnlyList<FusedRankingItem<T, TKey>> ReciprocalRankFusion<T, TKey>(
        IEnumerable<RankingSource<T>> sources,
        Func<T, TKey> keySelector,
        int rankConstant = 60,
        IEqualityComparer<TKey>? keyComparer = null)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(keySelector);

        if (rankConstant < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rankConstant));
        }

        var aggregates = new Dictionary<TKey, Aggregate<T, TKey>>(keyComparer);
        var nextOrder = 0;

        foreach (var source in sources)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentException.ThrowIfNullOrWhiteSpace(source.Name);
            ArgumentNullException.ThrowIfNull(source.Items);

            if (!double.IsFinite(source.Weight) || source.Weight <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sources),
                    $"Ranking source '{source.Name}' must have a finite weight greater than zero.");
            }

            var seenInSource = new HashSet<TKey>(keyComparer);
            var sourceRank = 0;
            for (var index = 0; index < source.Items.Count; index++)
            {
                var item = source.Items[index];
                var key = keySelector(item);

                if (key is null)
                {
                    throw new InvalidOperationException(
                        $"Ranking source '{source.Name}' produced a null candidate key.");
                }

                if (!seenInSource.Add(key))
                {
                    continue;
                }

                sourceRank++;
                var rank = sourceRank;
                var contribution = source.Weight / (rankConstant + (double)rank);

                if (!aggregates.TryGetValue(key, out var aggregate))
                {
                    aggregate = new Aggregate<T, TKey>(
                        item,
                        key,
                        contribution,
                        nextOrder++);
                    aggregates.Add(key, aggregate);
                }
                else
                {
                    aggregate.Score += contribution;
                    aggregate.SourceCount++;

                    if (contribution > aggregate.BestContribution)
                    {
                        aggregate.Item = item;
                        aggregate.BestContribution = contribution;
                    }
                }

                if (!double.IsFinite(aggregate.Score))
                {
                    throw new ArithmeticException(
                        "The reciprocal-rank fusion score exceeded the supported numeric range.");
                }
            }
        }

        return aggregates.Values
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.FirstSeenOrder)
            .Select((item, index) => new FusedRankingItem<T, TKey>(
                item.Item,
                item.Key,
                item.Score,
                index + 1,
                item.SourceCount))
            .ToArray();
    }

    private sealed class Aggregate<TItem, TKey>(
        TItem item,
        TKey key,
        double initialContribution,
        int firstSeenOrder)
        where TKey : notnull
    {
        public TItem Item { get; set; } = item;
        public TKey Key { get; } = key;
        public double Score { get; set; } = initialContribution;
        public double BestContribution { get; set; } = initialContribution;
        public int SourceCount { get; set; } = 1;
        public int FirstSeenOrder { get; } = firstSeenOrder;
    }
}
