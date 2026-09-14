namespace Xanax.Core;

public enum MissionRealizationKind
{
    Direct,
    Composed,
    Approximate,
    Unsupported
}

public sealed record MissionRealization(
    MissionRealizationKind Kind,
    bool Exact,
    bool Executable,
    IReadOnlyList<string> Reasons)
{
    public static MissionRealization Evaluate(
        MissionPlan mission,
        CanonicalExecutionResult canonical,
        IReadOnlyList<AdapterCommand> commands)
    {
        if (!canonical.Applied)
            return new(MissionRealizationKind.Unsupported, false, false,
                canonical.Rejections.Select(rejection => $"canonical:{rejection}").ToArray());
        if (commands.Count == 0 || commands.Any(command => !command.Concrete || command.RouteKind == AdapterRouteKind.Unsupported))
            return new(MissionRealizationKind.Unsupported, false, false, new[] { "no_concrete_target_realization" });

        var kind = mission.Relation switch
        {
            MissionRelation.Composed or MissionRelation.Merged or MissionRelation.Split => MissionRealizationKind.Composed,
            MissionRelation.Lossy or MissionRelation.ContextShift => MissionRealizationKind.Approximate,
            MissionRelation.Unknown or MissionRelation.AOnly or MissionRelation.BOnly => MissionRealizationKind.Unsupported,
            _ => MissionRealizationKind.Direct
        };
        var reasons = kind switch
        {
            MissionRealizationKind.Direct => new[] { "canonical_state_has_direct_target_route" },
            MissionRealizationKind.Composed => new[] { "target_realized_by_canonical_composition" },
            MissionRealizationKind.Approximate => new[] { "target_route_has_declared_loss_or_context_shift" },
            _ => new[] { "mission_relation_is_not_realizable" }
        };
        return new(kind, kind == MissionRealizationKind.Direct, true, reasons);
    }
}
