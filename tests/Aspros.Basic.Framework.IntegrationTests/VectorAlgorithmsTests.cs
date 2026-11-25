using Aspros.Base.Framework.Algorithms;
using Xunit;

namespace Aspros.Basic.Framework.IntegrationTests;

public sealed class VectorAlgorithmsTests
{
    [Fact]
    public void DotProduct_ReturnsExpectedValue()
    {
        Assert.Equal(32d, VectorAlgorithms.DotProduct([1d, 2d, 3d], [4d, 5d, 6d]));
    }

    [Fact]
    public void CosineSimilarity_PreservesNegativeValues()
    {
        Assert.Equal(-1d, VectorAlgorithms.CosineSimilarity([1d, 0d], [-1d, 0d]), 12);
        Assert.Equal(1d, VectorAlgorithms.CosineSimilarity([1d, 2d], [2d, 4d]), 12);
    }

    [Fact]
    public void CosineSimilarity_IsStableForLargeFiniteValues()
    {
        Assert.Equal(1d, VectorAlgorithms.CosineSimilarity(
            [double.MaxValue, double.MaxValue],
            [double.MaxValue, double.MaxValue]), 12);
    }

    [Fact]
    public void CosineSimilarity_ReturnsZeroWhenEitherVectorIsZero()
    {
        Assert.Equal(0d, VectorAlgorithms.CosineSimilarity([0d, 0d], [1d, 2d]));
        Assert.Equal(0d, VectorAlgorithms.CosineSimilarity([0d, 0d], [0d, 0d]));
    }

    [Fact]
    public void EuclideanDistance_ReturnsExpectedValue()
    {
        Assert.Equal(5d, VectorAlgorithms.EuclideanDistance([1d, 2d], [4d, 6d]));
    }

    [Fact]
    public void VectorOperations_RejectMismatchedDimensions()
    {
        Assert.Throws<ArgumentException>(() =>
            VectorAlgorithms.DotProduct([1d], [1d, 2d]));
        Assert.Throws<ArgumentException>(() =>
            VectorAlgorithms.CosineSimilarity([1d], [1d, 2d]));
        Assert.Throws<ArgumentException>(() =>
            VectorAlgorithms.EuclideanDistance([1d], [1d, 2d]));
    }

    [Fact]
    public void VectorOperations_RejectNonFiniteValues()
    {
        Assert.Throws<ArgumentException>(() =>
            VectorAlgorithms.CosineSimilarity([double.NaN], [1d]));
        Assert.Throws<ArgumentException>(() =>
            VectorAlgorithms.DotProduct([double.PositiveInfinity], [1d]));
    }

    [Fact]
    public void JaccardSimilarity_IgnoresDuplicates()
    {
        Assert.Equal(1d / 3d, SetSimilarityAlgorithms.JaccardSimilarity(
            new[] { "a", "a", "b" },
            new[] { "b", "c" }));
    }

    [Fact]
    public void JaccardSimilarity_TreatsTwoEmptySetsAsIdentical()
    {
        Assert.Equal(1d, SetSimilarityAlgorithms.JaccardSimilarity(
            Array.Empty<int>(),
            Array.Empty<int>()));
    }
}
