namespace Xanax.Core;

public sealed record MissionObservation(
    MissionCase SourceCase,
    TrajectoryComparisonResult OutputComparison,
    bool FeedbackMatched,
    IReadOnlyList<string> ObservedFeedback,
    DateTimeOffset ObservedAt,
    string ObservationId)
{
    public bool Reusable =>
        FeedbackMatched && OutputComparison.Relation == ConformanceRelation.Equal;
}

public static class MissionCaseEvidence
{
    public static MissionCase Promote(
        MissionObservation observation)
    {
        var source = observation.SourceCase;
        var relation = observation.OutputComparison.Relation switch
        {
            ConformanceRelation.Equal => source.Relation,
            ConformanceRelation.Approximate => "lossy_observed",
            ConformanceRelation.Different => "different_observed",
            _ => "unknown_observed"
        };
        var evidence = source.Evidence
            .Append($"observation:{observation.ObservationId}")
            .Append($"trajectory:{observation.OutputComparison.Distance:R}")
            .Concat(observation.ObservedFeedback.Select(feedback => $"feedback:{feedback}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var level = observation.Reusable
            ? "replayed"
            : observation.OutputComparison.Relation == ConformanceRelation.Approximate
                ? "replayed_approximate"
                : "replayed_failed";
        return source with
        {
            Relation = relation,
            EvidenceLevel = level,
            Evidence = evidence,
            CapturedAt = observation.ObservedAt,
            Loss = source.Loss
                .Concat(observation.OutputComparison.Relation == ConformanceRelation.Equal
                    ? Array.Empty<string>()
                    : new[] { $"trajectory_relation:{observation.OutputComparison.Relation}" })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    public static MissionCase Promote(
        MissionObservation observation,
        IReadOnlyList<TrajectorySample> targetTrajectory)
    {
        var promoted = Promote(observation);
        var signature = LiveShowTrajectoryAnalyzer.Analyze(targetTrajectory).Signature;
        return MissionCaseSignature.AttachTrajectory(promoted, signature);
    }
}
