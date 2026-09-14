namespace Xanax.Core;

public sealed record MissionTraceAlignment(
    int? FirstIndex,
    int? SecondIndex,
    double Cost);

public sealed record MissionTraceComparison(
    ConformanceRelation Relation,
    double Distance,
    double Tolerance,
    IReadOnlyList<MissionTraceAlignment> Alignment,
    string Reason);

/// <summary>
/// Compares canonical mission behavior rather than console step count or
/// event labels. Dynamic alignment permits one source operation to become
/// several destination operations while preserving the canonical state.
/// </summary>
public static class MissionTraceComparator
{
    public static MissionTraceComparison Compare(
        IReadOnlyList<CanonicalExecutionResult> first,
        IReadOnlyList<CanonicalExecutionResult> second,
        double tolerance = 1e-3,
        double gapPenalty = 1d,
        bool allowInternalDecomposition = true)
    {
        if (first.Count == 0 || second.Count == 0)
            return new(ConformanceRelation.Unknown, double.PositiveInfinity, tolerance, Array.Empty<MissionTraceAlignment>(), "empty_mission_trace");
        if (tolerance < 0 || double.IsNaN(tolerance) || double.IsInfinity(tolerance))
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        if (gapPenalty <= 0 || double.IsNaN(gapPenalty) || double.IsInfinity(gapPenalty))
            throw new ArgumentOutOfRangeException(nameof(gapPenalty));

        var costs = new double[first.Count + 1, second.Count + 1];
        var moves = new byte[first.Count + 1, second.Count + 1];
        for (var i = 1; i <= first.Count; i++)
        {
            costs[i, 0] = costs[i - 1, 0] + gapPenalty;
            moves[i, 0] = 1;
        }
        for (var j = 1; j <= second.Count; j++)
        {
            costs[0, j] = costs[0, j - 1] + gapPenalty;
            moves[0, j] = 2;
        }

        for (var i = 1; i <= first.Count; i++)
        for (var j = 1; j <= second.Count; j++)
        {
            var match = costs[i - 1, j - 1] + StateCost(first[i - 1], second[j - 1]);
            var delete = costs[i - 1, j] + gapPenalty;
            var insert = costs[i, j - 1] + gapPenalty;
            if (match <= delete && match <= insert)
            {
                costs[i, j] = match;
                moves[i, j] = 0;
            }
            else if (delete <= insert)
            {
                costs[i, j] = delete;
                moves[i, j] = 1;
            }
            else
            {
                costs[i, j] = insert;
                moves[i, j] = 2;
            }
        }

        var alignment = new List<MissionTraceAlignment>();
        var left = first.Count;
        var right = second.Count;
        while (left > 0 || right > 0)
        {
            var move = moves[left, right];
            if (left > 0 && right > 0 && move == 0)
            {
                alignment.Add(new(left - 1, right - 1, StateCost(first[left - 1], second[right - 1])));
                left--;
                right--;
            }
            else if (left > 0 && (right == 0 || move == 1))
            {
                alignment.Add(new(left - 1, null, gapPenalty));
                left--;
            }
            else
            {
                alignment.Add(new(null, right - 1, gapPenalty));
                right--;
            }
        }
        alignment.Reverse();

        var distance = costs[first.Count, second.Count] / Math.Max(first.Count, second.Count);
        var matchedCosts = alignment
            .Where(step => step.FirstIndex.HasValue && step.SecondIndex.HasValue)
            .Select(step => step.Cost)
            .ToArray();
        var decompositionEquivalent = allowInternalDecomposition &&
            matchedCosts.Length > 0 &&
            matchedCosts.Max() <= tolerance &&
            StateCost(first[^1], second[^1]) <= tolerance &&
            first.All(step => step.Rejections.Count == 0) &&
            second.All(step => step.Rejections.Count == 0);
        if (decompositionEquivalent)
            return new(ConformanceRelation.Equal, 0d, tolerance, alignment, "mission_equivalent_after_internal_decomposition");

        var relation = distance <= tolerance
            ? ConformanceRelation.Equal
            : distance <= Math.Max(tolerance * 4, tolerance + 1e-9)
                ? ConformanceRelation.Approximate
                : ConformanceRelation.Different;
        return new(relation, distance, tolerance, alignment, relation switch
        {
            ConformanceRelation.Equal => "mission_states_equivalent_after_alignment",
            ConformanceRelation.Approximate => "mission_states_approximately_equivalent_after_alignment",
            _ => "mission_state_distance_exceeds_tolerance"
        });
    }

    private static double StateCost(CanonicalExecutionResult first, CanonicalExecutionResult second)
    {
        var cost = first.Applied == second.Applied ? 0d : 1d;
        if (!string.Equals(first.State.Environment, second.State.Environment, StringComparison.OrdinalIgnoreCase))
            cost += 1d;
        cost += SequenceCost(first.State.SelectionOrder, second.State.SelectionOrder);
        cost += MapCost(first.State.AttributeValues, second.State.AttributeValues);
        cost += MapCost(first.State.PlaybackLevels, second.State.PlaybackLevels);
        cost += FixtureCost(first.State.FixtureAttributes, second.State.FixtureAttributes);
        if (first.Rejections.Count != second.Rejections.Count)
            cost += 1d;
        return cost;
    }

    private static double SequenceCost(IReadOnlyList<string> first, IReadOnlyList<string> second)
    {
        if (first.SequenceEqual(second, StringComparer.OrdinalIgnoreCase))
            return 0d;
        var common = first.Intersect(second, StringComparer.OrdinalIgnoreCase).Count();
        return 1d - common / (double)Math.Max(Math.Max(first.Count, second.Count), 1);
    }

    private static double MapCost(IReadOnlyDictionary<string, double> first, IReadOnlyDictionary<string, double> second)
    {
        var cost = 0d;
        foreach (var key in first.Keys.Union(second.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var firstExists = first.TryGetValue(key, out var firstValue);
            var secondExists = second.TryGetValue(key, out var secondValue);
            cost += !firstExists || !secondExists ? 1d : Math.Min(1d, Math.Abs(firstValue - secondValue));
        }
        return cost;
    }

    private static double FixtureCost(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> first,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> second)
    {
        var cost = 0d;
        foreach (var fixture in first.Keys.Union(second.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var firstExists = first.TryGetValue(fixture, out var firstValues);
            var secondExists = second.TryGetValue(fixture, out var secondValues);
            cost += !firstExists || !secondExists
                ? 1d
                : MapCost(firstValues!, secondValues!);
        }
        return cost;
    }
}
