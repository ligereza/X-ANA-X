namespace Xanax.Core;

public sealed record MatrixLearningResult(
    bool Learned,
    MissionCase Case,
    string Reason);

/// <summary>
/// Persistent evidence memory for MATRIX. It stores mission cases, not host
/// manuals, and never promotes an unverified prediction into executable truth.
/// </summary>
public sealed class MatrixLearningSession
{
    private readonly string? storePath;
    private readonly HashSet<string> persistedIds = new(StringComparer.OrdinalIgnoreCase);

    public MatrixLearningSession(string? storePath = null)
    {
        this.storePath = storePath;
        Learner = new MissionRelationLearner();
        NeuralModel = new MatrixRelationNeuralModel();
        if (!string.IsNullOrWhiteSpace(storePath))
        {
            var existing = MissionCaseStore.Read(storePath);
            Learner.Learn(existing);
            foreach (var missionCase in existing)
            {
                persistedIds.Add(missionCase.Id);
                NeuralModel.Train(missionCase);
            }
        }
    }

    public MissionRelationLearner Learner { get; }
    public MatrixRelationNeuralModel NeuralModel { get; }
    public IReadOnlyList<MissionCase> Cases => Learner.Cases;

    public MatrixLearningResult LearnObservation(
        MissionObservation observation,
        IReadOnlyList<TrajectorySample>? targetTrajectory = null)
    {
        var missionCase = targetTrajectory is null
            ? MissionCaseEvidence.Promote(observation)
            : MissionCaseEvidence.Promote(observation, targetTrajectory);
        if (!IsPromotable(missionCase))
            return new(false, missionCase, "observation_not_verified_for_learning");
        if (IsKnownCase(missionCase))
            return new(false, missionCase, "mission_case_already_known");

        Learner.Learn(new[] { missionCase });
        NeuralModel.Train(missionCase);
        PersistIfNew(missionCase);
        return new(true, missionCase, "verified_mission_case_learned");
    }

    public MatrixLearningResult LearnVerifiedCase(MissionCase missionCase)
    {
        if (!IsPromotable(missionCase))
            return new(false, missionCase, "case_requires_replayed_or_human_confirmed_evidence");
        if (IsKnownCase(missionCase))
            return new(false, missionCase, "mission_case_already_known");
        Learner.Learn(new[] { missionCase });
        NeuralModel.Train(missionCase);
        PersistIfNew(missionCase);
        return new(true, missionCase, "verified_mission_case_learned");
    }

    public MissionRelationPrediction Predict(
        MissionCase query,
        int neighborhood = 5,
        double minimumConfidence = 0.75,
        int minimumEvidenceCases = 2) =>
        Learner.Predict(query, neighborhood, minimumConfidence, minimumEvidenceCases);

    public MatrixNeuralPrediction PredictNeural(MissionCase query) => NeuralModel.Predict(query);

    public MatrixDecision Decide(
        MissionCase query,
        int neighborhood = 5,
        double minimumConfidence = 0.75,
        int minimumEvidenceCases = 2)
    {
        var symbolic = Predict(query, neighborhood, minimumConfidence, minimumEvidenceCases);
        var neural = PredictNeural(query);
        var consensus = symbolic.Relation.Equals(neural.Relation, StringComparison.OrdinalIgnoreCase) &&
            !symbolic.Relation.Equals("unknown", StringComparison.OrdinalIgnoreCase) &&
            !neural.Relation.Equals("unknown", StringComparison.OrdinalIgnoreCase);
        var eligible = consensus && symbolic.ExecutionEligible && neural.GeneralizationEligible && neural.WithinLearnedDomain;
        var reasons = new List<string>();
        if (!consensus) reasons.Add("symbolic_neural_disagreement");
        if (!symbolic.ExecutionEligible) reasons.Add("symbolic_evidence_insufficient_or_unsafe");
        if (!neural.GeneralizationEligible) reasons.Add("neural_evidence_insufficient_or_novel");
        if (eligible) reasons.Add("symbolic_neural_consensus_within_verified_domain");
        return new(consensus ? symbolic.Relation : "unknown", consensus, eligible, symbolic, neural, reasons);
    }

    private void PersistIfNew(MissionCase missionCase)
    {
        if (string.IsNullOrWhiteSpace(storePath) || !persistedIds.Add(missionCase.Id))
            return;
        MissionCaseStore.Append(storePath, missionCase);
    }

    private static bool IsPromotable(MissionCase missionCase) =>
        missionCase.Evidence.Count > 0 &&
        (missionCase.EvidenceLevel.Equals("replayed", StringComparison.OrdinalIgnoreCase) ||
         missionCase.EvidenceLevel.Equals("human_confirmed", StringComparison.OrdinalIgnoreCase));

    private bool IsKnownCase(MissionCase missionCase) =>
        Cases.Any(existing => string.Equals(existing.Id, missionCase.Id, StringComparison.OrdinalIgnoreCase));
}
