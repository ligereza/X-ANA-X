namespace Xanax.Core;

public sealed record CapabilitySnapshot(
    string Host,
    string SoftwareVersion,
    bool SoftwareInstalled,
    bool SemanticEditing,
    bool ParameterAccess,
    bool DmxOutput,
    bool RemoteInput,
    bool TargetIdentityVerified,
    bool ForegroundVerified,
    bool InteractiveDesktop = true);

public sealed record ExecutionGateResult(
    bool Allowed,
    IReadOnlyList<string> Reasons)
{
    public static ExecutionGateResult Denied(params string[] reasons) => new(false, reasons);
    public static ExecutionGateResult Allow() => new(true, Array.Empty<string>());
}

/// <summary>
/// Deterministic safety boundary between a compiled mission and native transport.
/// It cannot be learned away by LEARNING.
/// </summary>
public static class ExecutionGate
{
    public static ExecutionGateResult Evaluate(
        AdapterCommandPlan plan,
        CapabilitySnapshot capability,
        bool permitPhysicalOutput = false,
        LearningDecision? matrixDecision = null,
        bool requireMatrixConsensus = false)
    {
        var reasons = new List<string>();
        if (!plan.Executable)
            reasons.Add("plan_is_not_executable");
        if (plan.RealizationKind == MissionRealizationKind.Unsupported)
            reasons.Add("mission_has_no_realization");
        if (plan.RealizationKind == MissionRealizationKind.Approximate && !plan.AllowApproximateRealization)
            reasons.Add("approximate_realization_requires_explicit_permission");
        if (requireMatrixConsensus && (matrixDecision is null || !matrixDecision.ExecutionEligible))
            reasons.Add("matrix_consensus_required");
        if (!capability.SoftwareInstalled)
            reasons.Add("software_not_verified");
        if (!capability.SemanticEditing)
            reasons.Add("semantic_editing_not_verified");
        if (!capability.ParameterAccess)
            reasons.Add("parameter_access_not_verified");
        if (!capability.RemoteInput)
            reasons.Add("remote_input_not_verified");
        if (!capability.TargetIdentityVerified)
            reasons.Add("target_identity_not_verified");
        if (!capability.InteractiveDesktop)
            reasons.Add("interactive_desktop_not_verified");
        if (!string.Equals(capability.Host, plan.Mission.Target.Host, StringComparison.OrdinalIgnoreCase))
            reasons.Add("capability_host_does_not_match_plan_target");

        var outputOperation = plan.Commands.Any(command =>
            command.Operation is "trigger_or_adjust_live_playback" or "stop_or_release_active_output" or "carry_or_trigger_semantic_operation");
        if (outputOperation && (!capability.DmxOutput || !permitPhysicalOutput))
            reasons.Add("physical_output_gate_closed");
        if (plan.Commands.Any(command => command.RequiresConfirmation))
            reasons.Add("explicit_confirmation_required");
        return reasons.Count == 0 ? ExecutionGateResult.Allow() : new(false, reasons);
    }
}
