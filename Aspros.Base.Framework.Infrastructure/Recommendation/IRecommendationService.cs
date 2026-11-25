namespace Aspros.Base.Framework.Infrastructure.Recommendation;

public interface IRecommendationService
{
    Task<RecommendationResult> RecommendAsync(
        RecommendationRequest request,
        CancellationToken cancellationToken = default);
}
