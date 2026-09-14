namespace Xanax.Core;

public sealed record MissionRelationPrediction(
    string Relation,
    double Confidence,
    int SupportingCases,
    bool ExecutionEligible,
    IReadOnlyList<string> EvidenceIds);

/// <summary>
/// Evidence-only learner for LEARNING. It has no pretrained knowledge and never
/// promotes an inferred relation to an executable route.
/// </summary>
public sealed class MissionRelationLearner
{
    private readonly List<MissionCase> verifiedCases = new();

    public IReadOnlyList<MissionCase> Cases => verifiedCases;

    public void Learn(IEnumerable<MissionCase> cases)
    {
        foreach (var missionCase in cases)
        {
            if (IsVerified(missionCase) && verifiedCases.All(existing => !string.Equals(existing.Id, missionCase.Id, StringComparison.OrdinalIgnoreCase)))
                verifiedCases.Add(missionCase);
        }
    }

    public MissionRelationPrediction Predict(
        MissionCase query,
        int neighborhood = 5,
        double minimumConfidence = 0.75,
        int minimumEvidenceCases = 2)
    {
        var matches = MissionCaseSimilarity.Rank(query, verifiedCases, Math.Max(1, neighborhood));
        if (matches.Count == 0)
            return new("unknown", 0, 0, false, Array.Empty<string>());

        var votes = matches
            .GroupBy(match => match.Case.Relation, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Relation = group.Key,
                Weight = group.Sum(match => Math.Max(0, match.Score) + 1),
                Cases = group.Select(match => MissionEvidenceIdentity.For(match.Case)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                Evidence = group.Select(match => match.Case.Id).ToArray()
            })
            .OrderByDescending(vote => vote.Weight)
            .ThenByDescending(vote => vote.Cases)
            .First();
        var totalWeight = votes.Weight + matches
            .Where(match => !string.Equals(match.Case.Relation, votes.Relation, StringComparison.OrdinalIgnoreCase))
            .Sum(match => Math.Max(0, match.Score) + 1);
        var confidence = totalWeight <= 0 ? 0 : votes.Weight / totalWeight;
        var eligible = confidence >= minimumConfidence && votes.Cases >= Math.Max(1, minimumEvidenceCases) && IsExecutionSafe(votes.Relation);
        return new(votes.Relation, confidence, votes.Cases, eligible, votes.Evidence);
    }

    private static bool IsExecutionSafe(string relation) =>
        relation is "common" or "reordered" or "renamed" or "split" or "merged" or "composed";

    private static bool IsVerified(MissionCase missionCase) =>
        missionCase.Evidence.Count > 0 &&
        (missionCase.EvidenceLevel.Equals("replayed", StringComparison.OrdinalIgnoreCase) ||
         missionCase.EvidenceLevel.Equals("human_confirmed", StringComparison.OrdinalIgnoreCase));
}
