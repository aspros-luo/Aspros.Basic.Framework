namespace Aspros.Base.Framework.Algorithms;

/// <summary>
/// General-purpose operations over dense numeric vectors.
/// Inputs must have equal dimensions and contain only finite values.
/// </summary>
public static class VectorAlgorithms
{
    /// <summary>
    /// Calculates the dot product of two vectors.
    /// </summary>
    public static double DotProduct(
        ReadOnlySpan<double> left,
        ReadOnlySpan<double> right)
    {
        ValidateDimensions(left, right);
        ValidateFinite(left, nameof(left));
        ValidateFinite(right, nameof(right));

        var sum = 0d;
        for (var i = 0; i < left.Length; i++)
        {
            sum = Math.FusedMultiplyAdd(left[i], right[i], sum);
        }

        return sum;
    }

    /// <summary>
    /// Calculates cosine similarity in the mathematical range [-1, 1].
    /// If either vector has zero magnitude (including two zero vectors), returns 0.
    /// </summary>
    public static double CosineSimilarity(
        ReadOnlySpan<double> left,
        ReadOnlySpan<double> right)
    {
        ValidateDimensions(left, right);
        ValidateFinite(left, nameof(left));
        ValidateFinite(right, nameof(right));

        var leftScale = 0d;
        var rightScale = 0d;
        for (var i = 0; i < left.Length; i++)
        {
            leftScale = Math.Max(leftScale, Math.Abs(left[i]));
            rightScale = Math.Max(rightScale, Math.Abs(right[i]));
        }

        if (leftScale == 0d || rightScale == 0d)
        {
            return 0d;
        }

        // Scale each vector before multiplying so finite values near double.MaxValue
        // do not overflow while computing the magnitudes.
        var dot = 0d;
        var leftSquaredMagnitude = 0d;
        var rightSquaredMagnitude = 0d;

        for (var i = 0; i < left.Length; i++)
        {
            var normalizedLeft = left[i] / leftScale;
            var normalizedRight = right[i] / rightScale;

            dot = Math.FusedMultiplyAdd(normalizedLeft, normalizedRight, dot);
            leftSquaredMagnitude = Math.FusedMultiplyAdd(
                normalizedLeft, normalizedLeft, leftSquaredMagnitude);
            rightSquaredMagnitude = Math.FusedMultiplyAdd(
                normalizedRight, normalizedRight, rightSquaredMagnitude);
        }

        // Floating-point rounding can move a mathematically bounded result
        // infinitesimally outside [-1, 1].
        return Math.Clamp(
            dot / (Math.Sqrt(leftSquaredMagnitude) * Math.Sqrt(rightSquaredMagnitude)),
            -1d,
            1d);
    }

    /// <summary>
    /// Calculates Euclidean distance between two vectors.
    /// </summary>
    public static double EuclideanDistance(
        ReadOnlySpan<double> left,
        ReadOnlySpan<double> right)
    {
        ValidateDimensions(left, right);
        ValidateFinite(left, nameof(left));
        ValidateFinite(right, nameof(right));

        var sumOfSquares = 0d;
        for (var i = 0; i < left.Length; i++)
        {
            var difference = left[i] - right[i];
            sumOfSquares = Math.FusedMultiplyAdd(difference, difference, sumOfSquares);
        }

        return Math.Sqrt(sumOfSquares);
    }

    private static void ValidateDimensions(
        ReadOnlySpan<double> left,
        ReadOnlySpan<double> right)
    {
        if (left.Length != right.Length)
        {
            throw new ArgumentException(
                $"Vector dimensions must match. Left: {left.Length}; right: {right.Length}.");
        }
    }

    private static void ValidateFinite(ReadOnlySpan<double> values, string parameterName)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (!double.IsFinite(values[i]))
            {
                throw new ArgumentException(
                    $"Vector values must be finite. Invalid value at index {i}.",
                    parameterName);
            }
        }
    }
}
