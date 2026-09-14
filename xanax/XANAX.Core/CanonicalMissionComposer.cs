namespace Xanax.Core;

/// <summary>
/// Expands canonical operations that a target may not expose natively into a
/// finite sequence of canonical primitives. The expansion remains inspectable
/// and never claims that the target has the original native function.
/// </summary>
public static class CanonicalMissionComposer
{
    public static bool TryComposeSelectionTransform(
        MissionPlan mission,
        CanonicalSessionState transformedState,
        out MissionPlan composedMission)
    {
        composedMission = mission;
        if (!string.Equals(mission.Operation.CanonicalIntent, "transform_ordered_fixture_selection", StringComparison.OrdinalIgnoreCase) ||
            transformedState.SelectionOrder.Count == 0)
            return false;
        var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fixtures"] = string.Join(",", transformedState.SelectionOrder),
            ["mode"] = "replace"
        };
        composedMission = mission with
        {
            Operation = new CanonicalMissionOperation(
                $"{mission.Operation.MissionId}:select_result",
                "select_fixture_set",
                arguments)
        };
        return true;
    }

    public static AdapterTrajectoryPlan CompileModulation(
        MissionPlan mission,
        CanonicalHybridState initial,
        IReadOnlyList<double> times)
    {
        if (!string.Equals(mission.Operation.CanonicalIntent, "start_attribute_modulation", StringComparison.OrdinalIgnoreCase))
            return new(Array.Empty<TimedAdapterCommand>(), new[] { "composer_requires_start_attribute_modulation" });

        var started = CanonicalHybridRuntime.Apply(initial, mission.Operation);
        if (!started.Applied)
            return new(Array.Empty<TimedAdapterCommand>(), started.Rejections.Select(rejection => $"modulation:{rejection}").ToArray());
        if (!CanonicalModulationParser.TryParse(mission.Operation.Arguments, started.State.Session.SelectionOrder, out var modulation, out var error))
            return new(Array.Empty<TimedAdapterCommand>(), new[] { error });

        var primitiveArguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["attribute"] = modulation.Attribute,
            ["value"] = modulation.BaseValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            ["spread"] = "0",
            ["distribution"] = "linear"
        };
        var primitiveMission = mission with
        {
            Operation = new CanonicalMissionOperation(
                $"{mission.Operation.MissionId}:sampled",
                "edit_fixture_attribute",
                primitiveArguments)
        };
        return CanonicalAdapterTrajectoryPlanner.Compile(primitiveMission, started.State, times);
    }
}
