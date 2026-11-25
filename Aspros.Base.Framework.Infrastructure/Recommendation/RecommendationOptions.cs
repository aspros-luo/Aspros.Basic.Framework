namespace Aspros.Base.Framework.Infrastructure.Recommendation;

/// <summary>
/// Relative weights for normalized ranking signals. These are transparent defaults,
/// not universal business truths; tune them against offline and online evaluation data.
/// </summary>
public sealed class RecommendationOptions
{
    public double SimilarityWeight { get; set; } = 0.65;

    public double QualityWeight { get; set; } = 0.20;

    public double PopularityWeight { get; set; } = 0.10;

    public double FreshnessWeight { get; set; } = 0.05;

    internal void Validate()
    {
        ValidateWeight(SimilarityWeight, nameof(SimilarityWeight));
        ValidateWeight(QualityWeight, nameof(QualityWeight));
        ValidateWeight(PopularityWeight, nameof(PopularityWeight));
        ValidateWeight(FreshnessWeight, nameof(FreshnessWeight));

        var totalWeight = SimilarityWeight + QualityWeight + PopularityWeight + FreshnessWeight;
        var coldStartWeight = QualityWeight + PopularityWeight + FreshnessWeight;

        if (!double.IsFinite(totalWeight) || totalWeight <= 0)
        {
            throw new InvalidOperationException(
                "The total recommendation scoring weight must be finite and greater than zero.");
        }

        if (!double.IsFinite(coldStartWeight) || coldStartWeight <= 0)
        {
            throw new InvalidOperationException(
                "The total non-personalized scoring weight must be finite and greater than zero for cold-start users.");
        }
    }

    private static void ValidateWeight(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(
                name,
                value,
                "Recommendation scoring weights must be finite and non-negative.");
        }
    }
}
