using System.Globalization;

namespace Xanax.Core;

public sealed record DistributionProfile(
    string Shape,
    double Frequency = 1d,
    double Phase = 0d);

/// <summary>
/// Common distribution algebra for fixture groups. A console adapter can map
/// these profiles to Fan, MAtricks, a phaser, or a composed set of values.
/// </summary>
public static class DistributionMath
{
    public static bool TryCreate(
        IReadOnlyDictionary<string, string> arguments,
        out DistributionProfile profile,
        out string error)
    {
        var shape = arguments.TryGetValue("distribution", out var rawShape)
            ? rawShape.Trim().ToLowerInvariant()
            : "linear";
        if (shape is not ("linear" or "center" or "wings" or "sine" or "triangle"))
        {
            profile = new("linear");
            error = $"unknown_distribution:{shape}";
            return false;
        }

        var frequency = Parse(arguments, "frequency", 1d);
        var phase = Parse(arguments, "phase", 0d);
        if (frequency <= 0 || double.IsNaN(frequency) || double.IsInfinity(frequency) ||
            double.IsNaN(phase) || double.IsInfinity(phase))
        {
            profile = new("linear");
            error = "distribution_requires_finite_positive_frequency";
            return false;
        }

        profile = new(shape, frequency, phase);
        error = string.Empty;
        return true;
    }

    public static double Offset(
        DistributionProfile profile,
        int index,
        int count,
        double spread)
    {
        if (count < 1 || index < 0 || index >= count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (double.IsNaN(spread) || double.IsInfinity(spread))
            throw new ArgumentOutOfRangeException(nameof(spread));

        var position = count == 1 ? 0.5d : index / (double)(count - 1);
        var normalized = profile.Shape switch
        {
            "linear" => position,
            "center" => 1d - Math.Abs(2d * position - 1d),
            "wings" => Math.Abs(2d * position - 1d),
            "sine" => 0.5d + 0.5d * Math.Sin(2d * Math.PI * (profile.Frequency * position + profile.Phase)),
            "triangle" => Triangle(profile.Frequency * position + profile.Phase),
            _ => throw new ArgumentException($"Unknown distribution '{profile.Shape}'.", nameof(profile))
        };
        return (Math.Clamp(normalized, 0d, 1d) - 0.5d) * spread;
    }

    private static double Parse(IReadOnlyDictionary<string, string> arguments, string key, double fallback)
    {
        return arguments.TryGetValue(key, out var raw) &&
               double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static double Triangle(double position)
    {
        var fraction = position - Math.Floor(position);
        return 1d - Math.Abs(2d * fraction - 1d);
    }
}
