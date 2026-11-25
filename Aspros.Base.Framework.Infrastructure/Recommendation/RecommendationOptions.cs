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

        if (SimilarityWeight + QualityWeight + PopularityWeight + FreshnessWeight <= 0)
        {
            throw new InvalidOperationException(
                "At least one recommendation scoring weight must be greater than zero.");
        }

        if (QualityWeight + PopularityWeight + FreshnessWeight <= 0)
        {
            throw new InvalidOperationException(
                "At least one non-personalized scoring weight must be greater than zero for cold-start users.");
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
