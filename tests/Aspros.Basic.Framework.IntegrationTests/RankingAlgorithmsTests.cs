using Aspros.Base.Framework.Algorithms;
using Xunit;

namespace Aspros.Basic.Framework.IntegrationTests;

public sealed class RankingAlgorithmsTests
{
    [Fact]
    public void ReciprocalRankFusion_CombinesIndependentRankings()
    {
        var first = new[]
        {
            new Candidate("a", "from-personalized"),
            new Candidate("b", "from-personalized")
        };
        var second = new[]
        {
            new Candidate("b", "from-popular"),
            new Candidate("a", "from-popular")
        };

        var result = RankingAlgorithms.ReciprocalRankFusion(
            [
                new RankingSource<Candidate>("personalized", first),
                new RankingSource<Candidate>("popular", second)
            ],
            candidate => candidate.Id);

        Assert.Equal(new[] { "a", "b" }, result.Select(item => item.Key));
        Assert.Equal(2, result[0].SourceCount);
        Assert.Equal(1, result[0].Rank);
        Assert.Equal(1d / 61d + 1d / 62d, result[0].Score, 12);
    }

    [Fact]
    public void ReciprocalRankFusion_AppliesSourceWeights()
    {
        var result = RankingAlgorithms.ReciprocalRankFusion(
            [
                new RankingSource<Candidate>(
                    "personalized",
                    [new Candidate("a", "personalized"), new Candidate("b", "personalized")],
                    Weight: 1d),
                new RankingSource<Candidate>(
                    "business-priority",
                    [new Candidate("b", "priority"), new Candidate("a", "priority")],
                    Weight: 10d)
            ],
            candidate => candidate.Id);

        Assert.Equal("b", result[0].Key);
    }

    [Fact]
    public void ReciprocalRankFusion_DeduplicatesWithinSource()
    {
        var result = RankingAlgorithms.ReciprocalRankFusion(
            [
                new RankingSource<Candidate>(
                    "personalized",
                    [
                        new Candidate("a", "first"),
                        new Candidate("a", "duplicate"),
                        new Candidate("b", "second")
                    ])
            ],
            candidate => candidate.Id);

        Assert.Equal(new[] { "a", "b" }, result.Select(item => item.Key));
        Assert.Equal(1, result[0].SourceCount);
        Assert.Equal("first", result[0].Item.Origin);
        Assert.Equal(1d / 61d, result[0].Score, 12);
        Assert.Equal(1d / 62d, result[1].Score, 12);
    }

    [Fact]
    public void ReciprocalRankFusion_UsesStableOrderForEqualScores()
    {
        var result = RankingAlgorithms.ReciprocalRankFusion(
            [
                new RankingSource<Candidate>(
                    "source",
                    [new Candidate("first", "source"), new Candidate("second", "source")])
            ],
            candidate => candidate.Id);

        Assert.Equal(new[] { "first", "second" }, result.Select(item => item.Key));
    }

    [Fact]
    public void ReciprocalRankFusion_RejectsInvalidConfiguration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RankingAlgorithms.ReciprocalRankFusion(
                [new RankingSource<Candidate>("source", [new Candidate("a", "source")])],
                candidate => candidate.Id,
                rankConstant: 0));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RankingAlgorithms.ReciprocalRankFusion(
                [new RankingSource<Candidate>(
                    "source",
                    [new Candidate("a", "source")],
                    Weight: double.NaN)],
                candidate => candidate.Id));
    }

    private sealed record Candidate(string Id, string Origin);
}
