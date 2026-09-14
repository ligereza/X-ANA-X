namespace Xanax.Core;

public sealed record CanonicalDmxTrajectorySample(
    double TimeSeconds,
    CanonicalHybridState State,
    PatchedDmxFrame Frame,
    IReadOnlyList<string> Errors);

/// <summary>
/// Samples the hybrid canonical runtime into translated DMX frames. It is a
/// finite, deterministic trajectory planner, not a watcher or a live loop.
/// </summary>
public static class CanonicalDmxTrajectoryPlanner
{
    public static IReadOnlyList<CanonicalDmxTrajectorySample> Sample(
        CanonicalHybridState initial,
        IEnumerable<PersonalityPatchMapping> mappings,
        IReadOnlyList<double> times)
    {
        if (times.Count == 0)
            return Array.Empty<CanonicalDmxTrajectorySample>();
        if (times.Any(time => double.IsNaN(time) || double.IsInfinity(time) || time < 0) ||
            times.Zip(times.Skip(1), (first, second) => second <= first).Any(value => value))
            throw new ArgumentException("Trajectory times must be finite, non-negative and strictly increasing.", nameof(times));

        var mappingArray = mappings.ToArray();
        var current = initial;
        var previousTime = 0d;
        var samples = new List<CanonicalDmxTrajectorySample>(times.Count);
        foreach (var time in times)
        {
            var advanced = CanonicalHybridRuntime.Advance(current, time - previousTime);
            if (!advanced.Applied)
            {
                samples.Add(new(time, current, new PatchedDmxFrame(new Dictionary<DmxAddress, byte>()), advanced.Rejections));
                continue;
            }
            current = advanced.State;
            var translation = PersonalityPatchBridge.Translate(mappingArray, current.Session);
            samples.Add(new(time, current, translation.Frame, translation.Errors));
            previousTime = time;
        }
        return samples;
    }

    public static IReadOnlyList<TrajectorySample> ToUniverseTrajectory(
        IEnumerable<CanonicalDmxTrajectorySample> samples,
        int universe)
    {
        var frames = samples.Select(sample => (sample.TimeSeconds, sample.Frame));
        return PatchedDmxTrajectory.FromFrames(frames, universe);
    }
}
