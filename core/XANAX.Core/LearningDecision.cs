namespace Xanax.Core;

public sealed record LearningDecision(
    string Relation,
    bool Consensus,
    bool ExecutionEligible,
    MissionRelationPrediction Symbolic,
    NeuralPrediction Neural,
    IReadOnlyList<string> Reasons);
