namespace Aspros.Base.Framework.Algorithms;

/// <summary>
/// General-purpose similarity operations for discrete sets.
/// </summary>
public static class SetSimilarityAlgorithms
{
    /// <summary>
    /// Calculates Jaccard similarity: |A ∩ B| / |A ∪ B|.
    /// Two empty sets have a similarity of 1.
    /// Duplicate input values are ignored.
    /// </summary>
    public static double JaccardSimilarity<T>(
        IEnumerable<T> left,
        IEnumerable<T> right,
        IEqualityComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var leftSet = new HashSet<T>(left, comparer);
        var rightSet = new HashSet<T>(right, comparer);

        if (leftSet.Count == 0 && rightSet.Count == 0)
        {
            return 1d;
        }

        var intersectionCount = 0;
        foreach (var value in leftSet)
        {
            if (rightSet.Contains(value))
            {
                intersectionCount++;
            }
        }

        var unionCount = leftSet.Count + rightSet.Count - intersectionCount;
        return (double)intersectionCount / unionCount;
    }
}
