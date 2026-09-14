namespace Xanax.Core;

public sealed record LiveShowTrajectoryAnalysis(
    LiveShowSignature Signature,
    double DurationSeconds,
    int PeakCount,
    IReadOnlyList<double> Energy);

/// <summary>
/// Converts a sampled lighting trajectory into observable show-language axes.
/// The result is deliberately independent of console names, fixture addresses,
/// and UI layouts.
/// </summary>
public static class LiveShowTrajectoryAnalyzer
{
    public static LiveShowTrajectoryAnalysis Analyze(
        IReadOnlyList<TrajectorySample> trajectory,
        double activityThreshold = 0.08)
    {
        if (trajectory.Count < 2)
            throw new ArgumentException("At least two trajectory samples are required.", nameof(trajectory));
        if (activityThreshold <= 0 || activityThreshold >= 1 || double.IsNaN(activityThreshold) || double.IsInfinity(activityThreshold))
            throw new ArgumentOutOfRangeException(nameof(activityThreshold));

        var dimensions = trajectory[0].Values.Count;
        if (dimensions == 0 || trajectory.Any(sample => sample.Values.Count != dimensions))
            throw new ArgumentException("Trajectory samples must have equal non-zero dimensions.", nameof(trajectory));
        if (trajectory.Any(sample => sample.Values.Any(value => double.IsNaN(value) || double.IsInfinity(value))))
            throw new ArgumentException("Trajectory values must be finite.", nameof(trajectory));
        for (var index = 1; index < trajectory.Count; index++)
            if (trajectory[index].TimeSeconds <= trajectory[index - 1].TimeSeconds)
                throw new ArgumentException("Trajectory time must be strictly increasing.", nameof(trajectory));

        var normalized = NormalizeChannels(trajectory);
        var energy = normalized
            .Select(sample => sample.Average(Math.Abs))
            .ToArray();
        var duration = trajectory[^1].TimeSeconds - trajectory[0].TimeSeconds;
        var peakThreshold = Math.Max(activityThreshold, energy.Max() * activityThreshold);
        var peaks = FindPeaks(trajectory, energy, peakThreshold);
        var active = energy.Count(value => value >= activityThreshold) / (double)energy.Length;
        var onsetIndex = Array.FindIndex(energy, value => value >= peakThreshold);
        var onset = onsetIndex < 0 ? 1d : onsetIndex / (double)(energy.Length - 1);
        var meanEnergy = energy.Average();
        var attack = 0d;
        var decay = 0d;
        for (var index = 1; index < energy.Length; index++)
        {
            var delta = energy[index] - energy[index - 1];
            if (delta >= 0) attack += delta;
            else decay -= delta;
        }

        var signature = new LiveShowSignature(
            new[]
            {
                "onset", "rhythm", "rate_frequency", "envelope",
                "amplitude_energy", "density", "persistence"
            },
            new[]
            {
                onset,
                duration <= 0 ? 0d : peaks.Count / duration,
                duration <= 0 ? 0d : ZeroCrossingFrequency(energy, duration),
                (attack + decay) <= double.Epsilon ? 0d : attack / (attack + decay),
                meanEnergy,
                normalized.SelectMany(sample => sample).Count(value => value >= activityThreshold) /
                    (double)(normalized.Count * dimensions),
                active
            });
        signature.Validate();
        return new(signature, duration, peaks.Count, energy);
    }

    private static IReadOnlyList<double[]> NormalizeChannels(IReadOnlyList<TrajectorySample> trajectory)
    {
        var dimensions = trajectory[0].Values.Count;
        var minima = Enumerable.Range(0, dimensions).Select(index => trajectory.Min(sample => sample.Values[index])).ToArray();
        var maxima = Enumerable.Range(0, dimensions).Select(index => trajectory.Max(sample => sample.Values[index])).ToArray();
        return trajectory.Select(sample => Enumerable.Range(0, dimensions)
            .Select(index => maxima[index] - minima[index] <= double.Epsilon
                ? 0d
                : Math.Clamp((sample.Values[index] - minima[index]) / (maxima[index] - minima[index]), 0d, 1d))
            .ToArray()).ToArray();
    }

    private static IReadOnlyList<int> FindPeaks(
        IReadOnlyList<TrajectorySample> trajectory,
        IReadOnlyList<double> energy,
        double threshold)
    {
        var peaks = new List<int>();
        for (var index = 1; index < energy.Count - 1; index++)
        {
            if (energy[index] >= threshold && energy[index] >= energy[index - 1] && energy[index] > energy[index + 1])
                peaks.Add(index);
        }
        if (energy[^1] >= threshold && energy[^1] > energy[^2])
            peaks.Add(energy.Count - 1);
        return peaks;
    }

    private static double ZeroCrossingFrequency(IReadOnlyList<double> energy, double duration)
    {
        var center = energy.Average();
        var crossings = 0;
        var previous = energy[0] - center;
        for (var index = 1; index < energy.Count; index++)
        {
            var current = energy[index] - center;
            if ((previous < 0 && current >= 0) || (previous > 0 && current <= 0))
                crossings++;
            previous = current;
        }
        return crossings / Math.Max(2d * duration, double.Epsilon);
    }
}
