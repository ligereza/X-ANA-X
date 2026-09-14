using System.Globalization;

namespace Xanax.Core;

public sealed record CanonicalModulation(
    string Key,
    string Attribute,
    IReadOnlyList<string> Fixtures,
    double BaseValue,
    double Amplitude,
    double FrequencyHz,
    double PhaseCycles,
    string Waveform)
{
    public double ValueAt(double timeSeconds)
    {
        var phase = FrequencyHz * timeSeconds + PhaseCycles;
        var normalized = phase - Math.Floor(phase);
        var waveformValue = Waveform.ToLowerInvariant() switch
        {
            "sine" => Math.Sin(2d * Math.PI * phase),
            "triangle" => 1d - 4d * Math.Abs(Math.Round(normalized) - normalized),
            "square" => Math.Sin(2d * Math.PI * phase) >= 0d ? 1d : -1d,
            "saw" => 2d * normalized - 1d,
            _ => throw new InvalidOperationException($"Unknown canonical waveform '{Waveform}'.")
        };
        return BaseValue + Amplitude * waveformValue;
    }
}

public static class CanonicalModulationParser
{
    public static bool TryParse(
        IReadOnlyDictionary<string, string> arguments,
        IReadOnlyList<string> selection,
        out CanonicalModulation modulation,
        out string error)
    {
        modulation = new("", "", Array.Empty<string>(), 0, 0, 0, 0, "sine");
        if (selection.Count == 0)
        {
            error = "modulation_requires_selection";
            return false;
        }
        if (!arguments.TryGetValue("attribute", out var attribute) || string.IsNullOrWhiteSpace(attribute))
        {
            error = "modulation_requires_attribute";
            return false;
        }
        if (!TryRead(arguments, "base", 0d, out var baseValue) ||
            !TryRead(arguments, "amplitude", 1d, out var amplitude) ||
            !TryRead(arguments, "frequency", 1d, out var frequency) ||
            !TryRead(arguments, "phase", 0d, out var phase) ||
            frequency <= 0d)
        {
            error = "modulation_requires_finite_positive_frequency";
            return false;
        }
        var waveform = arguments.TryGetValue("waveform", out var rawWaveform)
            ? rawWaveform.Trim().ToLowerInvariant()
            : "sine";
        if (waveform is not ("sine" or "triangle" or "square" or "saw"))
        {
            error = $"unknown_waveform:{waveform}";
            return false;
        }
        var key = arguments.TryGetValue("key", out var rawKey) && !string.IsNullOrWhiteSpace(rawKey)
            ? rawKey
            : $"{attribute}:{string.Join(",", selection)}";
        modulation = new(key, attribute, selection.ToArray(), baseValue, amplitude, frequency, phase, waveform);
        error = string.Empty;
        return true;
    }

    private static bool TryRead(
        IReadOnlyDictionary<string, string> arguments,
        string key,
        double fallback,
        out double value)
    {
        value = arguments.TryGetValue(key, out var raw) &&
                double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
