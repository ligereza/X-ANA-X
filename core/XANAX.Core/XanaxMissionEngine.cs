namespace Xanax.Core;

public sealed record XanaxMissionRequest(
    string MissionId,
    string SourceHost,
    string TargetHost,
    CanonicalSessionState State,
    IReadOnlyDictionary<string, string> Arguments,
    CapabilitySnapshot Capability,
    bool PermitPhysicalOutput = false,
    LearningDecision? LearningDecision = null,
    bool RequireLearningConsensus = false,
    IReadOnlyList<double>? TrajectoryTimes = null,
    bool AllowApproximateRealization = false);

public sealed record XanaxMissionResult(
    MissionPlan Plan,
    CanonicalExecutionResult Canonical,
    AdapterCommandPlan Commands,
    ExecutionGateResult Gate,
    CapabilitySnapshot Capability,
    bool PermitPhysicalOutput,
    MissionRealization Realization,
    LearningDecision? LearningDecision,
    AdapterTrajectoryPlan? Trajectory);

/// <summary>
/// Single orchestration boundary for XANAX. Building a result is pure; sending
/// it requires an explicit later call and still passes through ExecutionGate.
/// </summary>
public sealed class XanaxMissionEngine
{
    private readonly MissionCatalog catalog;
    private readonly CanonicalOperationRegistry? registry;
    private readonly NativeAdapterTransport transport;

    public XanaxMissionEngine(
        MissionCatalog catalog,
        CanonicalOperationRegistry? registry = null,
        NativeAdapterTransport? transport = null)
    {
        this.catalog = catalog;
        this.registry = registry;
        this.transport = transport ?? new NativeAdapterTransport();
    }

    public XanaxMissionResult Build(XanaxMissionRequest request)
    {
        var plan = catalog.Plan(request.MissionId, request.SourceHost, request.TargetHost, request.Arguments);
        var canonical = CanonicalOperationEngine.Apply(request.State, plan.Operation, registry);
        AdapterTrajectoryPlan? trajectory = null;
        AdapterCommandPlan commands;
        if (!canonical.Applied)
        {
            commands = new AdapterCommandPlan(
                plan,
                Array.Empty<AdapterCommand>(),
                canonical.Rejections.Select(rejection => $"canonical_rejected:{rejection}").ToArray());
        }
        else if (request.TrajectoryTimes is not null &&
                 string.Equals(plan.Operation.CanonicalIntent, "start_attribute_modulation", StringComparison.OrdinalIgnoreCase))
        {
            var hybrid = new CanonicalHybridState(
                request.State,
                new Dictionary<string, CanonicalFade>(StringComparer.OrdinalIgnoreCase));
            trajectory = CanonicalMissionComposer.CompileModulation(plan, hybrid, request.TrajectoryTimes);
            commands = new AdapterCommandPlan(
                plan,
                trajectory.Succeeded
                    ? trajectory.Commands.Select(timed => timed.Command).ToArray()
                    : Array.Empty<AdapterCommand>(),
                trajectory.Errors);
        }
        else
        {
            var commandPlan = plan;
            if (!plan.Definition.NativeRoutes.ContainsKey(plan.Target.Host) &&
                CanonicalMissionComposer.TryComposeSelectionTransform(plan, canonical.State, out var composedMission))
                commandPlan = composedMission;
            commands = AdapterCommandCompiler.Compile(commandPlan, canonical.State) with { Mission = plan };
        }
        commands = commands with
        {
            LearningDecision = request.LearningDecision,
            RequireLearningConsensus = request.RequireLearningConsensus,
            AllowApproximateRealization = request.AllowApproximateRealization
        };
        var gate = ExecutionGate.Evaluate(
            commands,
            request.Capability,
            request.PermitPhysicalOutput,
            request.LearningDecision,
            request.RequireLearningConsensus);
        var realization = MissionRealization.Evaluate(plan, canonical, commands.Commands);
        return new(plan, canonical, commands, gate, request.Capability, request.PermitPhysicalOutput, realization, request.LearningDecision, trajectory);
    }

    public XanaxMissionResult BuildWithMatrix(
        XanaxMissionRequest request,
        LearningSession matrix,
        MissionCase query,
        int neighborhood = 5,
        double minimumConfidence = 0.75,
        int minimumEvidenceCases = 2)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        var decision = matrix.Decide(query, neighborhood, minimumConfidence, minimumEvidenceCases);
        return Build(request with
        {
            LearningDecision = decision,
            RequireLearningConsensus = true
        });
    }

    public Task<IReadOnlyList<NativeTransportResult>> ExecuteAsync(
        XanaxMissionResult result,
        NativeTransportTarget target,
        CancellationToken cancellationToken = default)
    {
        if (!result.Gate.Allowed)
        {
            var reason = string.Join(";", result.Gate.Reasons);
            IReadOnlyList<NativeTransportResult> rejected = result.Commands.Commands.Count == 0
                ? new[] { NativeTransportResult.Rejected(result.Plan.Target.Host, "plan", reason) }
                : result.Commands.Commands.Select(command => NativeTransportResult.Rejected(command, reason)).ToArray();
            return Task.FromResult<IReadOnlyList<NativeTransportResult>>(rejected);
        }
        if (result.Trajectory is not null)
        {
            return transport.SendTrajectoryAsync(
                result.Commands,
                result.Trajectory,
                target,
                result.Capability,
                result.PermitPhysicalOutput,
                cancellationToken);
        }
        return transport.SendPlanAsync(result.Commands, target, result.Capability,
            permitPhysicalOutput: result.PermitPhysicalOutput,
            cancellationToken);
    }
}
