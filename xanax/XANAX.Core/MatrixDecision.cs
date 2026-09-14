namespace Xanax.Core;

public sealed record MatrixDecision(
    string Relation,
    bool Consensus,
    bool ExecutionEligible,
    MissionRelationPrediction Symbolic,
    MatrixNeuralPrediction Neural,
    IReadOnlyList<string> Reasons);
