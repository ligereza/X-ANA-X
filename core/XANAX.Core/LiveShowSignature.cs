namespace Xanax.Core;

public sealed record LiveShowSignature(
    IReadOnlyList<string> Axes,
    IReadOnlyList<double> Values)
{
    public static IReadOnlyList<string> DefaultAxes { get; } = new[]
    {
        "onset", "rhythm", "rate_frequency", "envelope", "amplitude_energy",
        "phase", "density", "spatial_topology", "persistence", "causality",
        "memory_behavior", "feedback"
    };

    public void Validate()
    {
        if (Axes.Count == 0 || Axes.Count != Values.Count ||
            Axes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != Axes.Count ||
            Values.Any(value => double.IsNaN(value) || double.IsInfinity(value)))
            throw new ArgumentException("A live-show signature must have unique finite axes with matching values.");
    }
}

public sealed record SignatureComparison(
    ConformanceRelation Relation,
    double Distance,
    double Tolerance,
    IReadOnlyList<string> CriticalConflicts,
    string Reason);

public static class LiveShowSignatureMath
{
    public static SignatureComparison Compare(
        LiveShowSignature source,
        LiveShowSignature target,
        double tolerance,
        IReadOnlyDictionary<string, double>? weights = null,
        IReadOnlySet<string>? criticalAxes = null)
    {
        source.Validate();
        target.Validate();
        if (tolerance < 0 || double.IsNaN(tolerance) || double.IsInfinity(tolerance))
            throw new ArgumentOutOfRangeException(nameof(tolerance));

        var sourceValues = source.Axes
            .Select((axis, index) => (axis, value: source.Values[index]))
            .ToDictionary(item => item.axis, item => item.value, StringComparer.OrdinalIgnoreCase);
        var targetValues = target.Axes
            .Select((axis, index) => (axis, value: target.Values[index]))
            .ToDictionary(item => item.axis, item => item.value, StringComparer.OrdinalIgnoreCase);
        var weightedError = 0d;
        var weightTotal = 0d;
        var conflicts = new List<string>();
        foreach (var axis in sourceValues.Keys.Intersect(targetValues.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var weight = weights is not null && weights.TryGetValue(axis, out var configured) ? configured : 1d;
            if (weight < 0 || double.IsNaN(weight) || double.IsInfinity(weight))
                throw new ArgumentException("Signature weights must be finite and non-negative.", nameof(weights));
            var difference = sourceValues[axis] - targetValues[axis];
            weightedError += weight * difference * difference;
            weightTotal += weight;
            if (criticalAxes is not null && criticalAxes.Contains(axis) && Math.Abs(difference) > tolerance)
                conflicts.Add(axis);
        }
        var distance = Math.Sqrt(weightedError / Math.Max(weightTotal, double.Epsilon));
        var relation = conflicts.Count > 0 || distance > Math.Max(tolerance * 4, tolerance + 1e-9)
            ? ConformanceRelation.Different
            : distance <= tolerance
                ? ConformanceRelation.Equal
                : ConformanceRelation.Approximate;
        return new(relation, distance, tolerance, conflicts, relation switch
        {
            ConformanceRelation.Equal => "live_show_signature_equal",
            ConformanceRelation.Approximate => "live_show_signature_approximate",
            _ => "critical_signature_conflict_or_distance"
        });
    }
}
