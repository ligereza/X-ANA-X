namespace Xanax.Core;

public sealed record TrajectorySample(
    double TimeSeconds,
    IReadOnlyList<double> Values);

public sealed record TrajectoryAlignment(
    int SourceIndex,
    int TargetIndex,
    double Cost);

public sealed record TrajectoryComparisonResult(
    ConformanceRelation Relation,
    double Distance,
    double Tolerance,
    IReadOnlyList<TrajectoryAlignment> Alignment,
    string Reason);

/// <summary>
/// Compares live behavior over time. Dynamic-time alignment handles different
/// execution rates while weighted value distance preserves physical importance.
/// </summary>
public static class TrajectoryComparator
{
    public static TrajectoryComparisonResult Compare(
        IReadOnlyList<TrajectorySample> source,
        IReadOnlyList<TrajectorySample> target,
        double tolerance,
        IReadOnlyList<double>? axisWeights = null,
        bool allowTimeWarp = true,
        double timeWeight = 0,
        double gapPenalty = 1)
    {
        if (source.Count == 0 || target.Count == 0)
            return new(ConformanceRelation.Unknown, double.PositiveInfinity, tolerance, Array.Empty<TrajectoryAlignment>(), "empty_trajectory");
        if (tolerance < 0 || double.IsNaN(tolerance) || double.IsInfinity(tolerance))
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        var dimensions = source[0].Values.Count;
        if (dimensions == 0 || target.Any(sample => sample.Values.Count != dimensions) || source.Any(sample => sample.Values.Count != dimensions))
            return new(ConformanceRelation.Unknown, double.PositiveInfinity, tolerance, Array.Empty<TrajectoryAlignment>(), "trajectory_dimension_mismatch");

        var weights = axisWeights?.ToArray() ?? Enumerable.Repeat(1d, dimensions).ToArray();
        if (weights.Length != dimensions || weights.Any(weight => weight < 0 || double.IsNaN(weight) || double.IsInfinity(weight)))
            throw new ArgumentException("Axis weights must match trajectory dimensions and be finite non-negative values.", nameof(axisWeights));
        if (weights.All(weight => weight == 0))
            throw new ArgumentException("At least one axis weight must be non-zero.", nameof(axisWeights));

        var n = source.Count;
        var m = target.Count;
        var costs = new double[n + 1, m + 1];
        var moves = new byte[n + 1, m + 1];
        for (var i = 1; i <= n; i++)
        {
            costs[i, 0] = costs[i - 1, 0] + gapPenalty;
            moves[i, 0] = 1;
        }
        for (var j = 1; j <= m; j++)
        {
            costs[0, j] = costs[0, j - 1] + gapPenalty;
            moves[0, j] = 2;
        }

        for (var i = 1; i <= n; i++)
        for (var j = 1; j <= m; j++)
        {
            var match = costs[i - 1, j - 1] + SampleCost(source[i - 1], target[j - 1], weights, allowTimeWarp, timeWeight);
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

        var alignment = new List<TrajectoryAlignment>();
        var sourceIndex = n;
        var targetIndex = m;
        while (sourceIndex > 0 || targetIndex > 0)
        {
            var move = moves[sourceIndex, targetIndex];
            if (sourceIndex > 0 && targetIndex > 0 && move == 0)
            {
                alignment.Add(new(
                    sourceIndex - 1,
                    targetIndex - 1,
                    SampleCost(source[sourceIndex - 1], target[targetIndex - 1], weights, allowTimeWarp, timeWeight)));
                sourceIndex--;
                targetIndex--;
            }
            else if (sourceIndex > 0 && (targetIndex == 0 || move == 1))
            {
                sourceIndex--;
            }
            else
            {
                targetIndex--;
            }
        }
        alignment.Reverse();

        var distance = costs[n, m] / Math.Max(n, m);
        var relation = distance <= tolerance
            ? ConformanceRelation.Equal
            : distance <= Math.Max(tolerance * 4, tolerance + 1e-9)
                ? ConformanceRelation.Approximate
                : ConformanceRelation.Different;
        return new(relation, distance, tolerance, alignment, relation switch
        {
            ConformanceRelation.Equal => "trajectory_within_tolerance",
            ConformanceRelation.Approximate => "trajectory_close_but_not_equal",
            _ => "trajectory_distance_exceeds_tolerance"
        });
    }

    private static double SampleCost(
        TrajectorySample source,
        TrajectorySample target,
        IReadOnlyList<double> weights,
        bool allowTimeWarp,
        double timeWeight)
    {
        var weighted = 0d;
        var weightTotal = 0d;
        for (var index = 0; index < weights.Count; index++)
        {
            var difference = source.Values[index] - target.Values[index];
            weighted += weights[index] * difference * difference;
            weightTotal += weights[index];
        }
        var valueDistance = Math.Sqrt(weighted / Math.Max(weightTotal, double.Epsilon));
        var temporalDistance = allowTimeWarp ? 0d : Math.Abs(source.TimeSeconds - target.TimeSeconds) * timeWeight;
        return valueDistance + temporalDistance;
    }
}

public static class PatchedDmxTrajectory
{
    public static IReadOnlyList<TrajectorySample> FromFrames(
        IEnumerable<(double TimeSeconds, PatchedDmxFrame Frame)> frames,
        int universe)
    {
        if (universe < 1)
            throw new ArgumentOutOfRangeException(nameof(universe));
        return frames.Select(frame => new TrajectorySample(
            frame.TimeSeconds,
            Enumerable.Range(1, 512).Select(channel => (double)frame.Frame[universe, channel]).ToArray())).ToArray();
    }
}
