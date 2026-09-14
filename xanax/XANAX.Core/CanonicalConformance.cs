namespace Xanax.Core;

public enum ConformanceRelation
{
    Equal,
    Approximate,
    Different,
    Unknown
}

public sealed record ConformanceAssessment(
    ConformanceRelation Relation,
    double MaximumNumericError,
    int ComparedSteps,
    int? FirstDifferenceStep,
    string Reason);

public static class CanonicalConformance
{
    public static ConformanceAssessment Compare(
        IReadOnlyList<CanonicalExecutionResult> first,
        IReadOnlyList<CanonicalExecutionResult> second,
        double approximationTolerance = 1e-3,
        double exactTolerance = 1e-9)
    {
        if (first.Count != second.Count)
            return new(ConformanceRelation.Different, double.PositiveInfinity, Math.Min(first.Count, second.Count), Math.Min(first.Count, second.Count), "trace_length_differs");

        var maximumError = 0d;
        int? firstDifference = null;
        var structuralDifference = false;
        for (var step = 0; step < first.Count; step++)
        {
            var left = first[step];
            var right = second[step];
            if (left.Applied != right.Applied)
            {
                structuralDifference = true;
                firstDifference ??= step;
                continue;
            }

            if (!left.State.SelectionOrder.SequenceEqual(right.State.SelectionOrder, StringComparer.OrdinalIgnoreCase) ||
                !string.Equals(left.State.Environment, right.State.Environment, StringComparison.OrdinalIgnoreCase) ||
                !left.Events.SequenceEqual(right.Events, StringComparer.OrdinalIgnoreCase) ||
                !left.Rejections.SequenceEqual(right.Rejections, StringComparer.OrdinalIgnoreCase))
            {
                structuralDifference = true;
                firstDifference ??= step;
            }
            var attributes = CompareValues(left.State.AttributeValues, right.State.AttributeValues, ref firstDifference, step);
            var playbacks = CompareValues(left.State.PlaybackLevels, right.State.PlaybackLevels, ref firstDifference, step);
            var fixtures = CompareFixtureValues(left.State.FixtureAttributes, right.State.FixtureAttributes, ref firstDifference, step);
            structuralDifference |= attributes.StructuralDifference || playbacks.StructuralDifference || fixtures.StructuralDifference;
            maximumError = Math.Max(maximumError, Math.Max(attributes.MaximumError, playbacks.MaximumError));
            maximumError = Math.Max(maximumError, fixtures.MaximumError);
        }

        if (structuralDifference)
            return new(ConformanceRelation.Different, maximumError, first.Count, firstDifference, "canonical_structure_differs");
        if (maximumError <= exactTolerance)
            return new(ConformanceRelation.Equal, maximumError, first.Count, null, "canonical_traces_equal");
        if (maximumError <= approximationTolerance)
            return new(ConformanceRelation.Approximate, maximumError, first.Count, null, "canonical_traces_within_tolerance");
        return new(ConformanceRelation.Different, maximumError, first.Count, firstDifference, "canonical_numeric_error_exceeds_tolerance");
    }

    private static (double MaximumError, bool StructuralDifference) CompareValues(
        IReadOnlyDictionary<string, double> first,
        IReadOnlyDictionary<string, double> second,
        ref int? firstDifference,
        int step)
    {
        var maximum = 0d;
        var structuralDifference = false;
        var keys = first.Keys.Union(second.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            var firstExists = first.TryGetValue(key, out var firstValue);
            var secondExists = second.TryGetValue(key, out var secondValue);
            if (firstExists != secondExists)
            {
                structuralDifference = true;
                firstDifference ??= step;
                continue;
            }
            var error = Math.Abs(firstValue - secondValue);
            maximum = Math.Max(maximum, error);
        }
        return (maximum, structuralDifference);
    }

    private static (double MaximumError, bool StructuralDifference) CompareFixtureValues(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> first,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> second,
        ref int? firstDifference,
        int step)
    {
        var maximum = 0d;
        var structuralDifference = false;
        var fixtures = first.Keys.Union(second.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var fixture in fixtures)
        {
            var firstExists = first.TryGetValue(fixture, out var firstValues);
            var secondExists = second.TryGetValue(fixture, out var secondValues);
            if (firstExists != secondExists)
            {
                structuralDifference = true;
                firstDifference ??= step;
                continue;
            }
            var values = CompareValues(firstValues!, secondValues!, ref firstDifference, step);
            maximum = Math.Max(maximum, values.MaximumError);
            structuralDifference |= values.StructuralDifference;
        }
        return (maximum, structuralDifference);
    }
}
