namespace Xanax.Core;

public sealed record TimedAdapterCommand(
    double TimeSeconds,
    AdapterCommand Command);

public sealed record AdapterTrajectoryPlan(
    IReadOnlyList<TimedAdapterCommand> Commands,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => Errors.Count == 0 && Commands.Count > 0;
}

/// <summary>
/// Converts a finite canonical trajectory into timestamped native commands.
/// Scheduling/transport remains outside this pure planner.
/// </summary>
public static class CanonicalAdapterTrajectoryPlanner
{
    public static AdapterTrajectoryPlan Compile(
        MissionPlan mission,
        CanonicalHybridState initial,
        IReadOnlyList<double> times)
    {
        if (!string.Equals(mission.Operation.CanonicalIntent, "edit_fixture_attribute", StringComparison.OrdinalIgnoreCase))
            return new(Array.Empty<TimedAdapterCommand>(), new[] { "trajectory_requires_attribute_edit_mission" });
        if (times.Count == 0 || times.Any(time => double.IsNaN(time) || double.IsInfinity(time) || time < 0) ||
            times.Zip(times.Skip(1), (first, second) => second <= first).Any(value => value))
            return new(Array.Empty<TimedAdapterCommand>(), new[] { "trajectory_times_must_be_strictly_increasing" });

        var errors = new List<string>();
        var commands = new List<TimedAdapterCommand>();
        var current = initial;
        var previousTime = 0d;
        foreach (var time in times)
        {
            var advanced = CanonicalHybridRuntime.Advance(current, time - previousTime);
            if (!advanced.Applied)
            {
                errors.AddRange(advanced.Rejections.Select(rejection => $"advance:{rejection}"));
                break;
            }
            current = advanced.State;
            var arguments = mission.Operation.Arguments.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            arguments["spread"] = "0";
            arguments["distribution"] = "linear";
            var sampleMission = mission with
            {
                Operation = mission.Operation with { Arguments = arguments }
            };
            var samplePlan = AdapterCommandCompiler.Compile(sampleMission, current.Session);
            if (!samplePlan.Executable)
            {
                errors.AddRange(samplePlan.Warnings.Select(warning => $"sample:{warning}"));
                if (samplePlan.Warnings.Count == 0)
                    errors.Add($"sample_not_executable:{time:R}");
                break;
            }
            commands.AddRange(samplePlan.Commands.Select(command => new TimedAdapterCommand(time, command)));
            previousTime = time;
        }
        return new(commands, errors);
    }
}
