using System.Globalization;

namespace Xanax.Core;

public sealed record CanonicalFade(
    string Key,
    double Start,
    double Target,
    double DurationSeconds,
    double ElapsedSeconds = 0)
{
    public double Value => DurationSeconds <= 0
        ? Target
        : Start + (Target - Start) * Math.Clamp(ElapsedSeconds / DurationSeconds, 0d, 1d);

    public bool Complete => DurationSeconds <= 0 || ElapsedSeconds >= DurationSeconds;

    public CanonicalFade Advance(double seconds) =>
        this with { ElapsedSeconds = Math.Max(0d, ElapsedSeconds + seconds) };
}

public sealed record CanonicalHybridState(
    CanonicalSessionState Session,
    IReadOnlyDictionary<string, CanonicalFade> Fades,
    double TimeSeconds = 0,
    IReadOnlyDictionary<string, CanonicalModulation>? Modulations = null)
{
    public IReadOnlyDictionary<string, CanonicalModulation> ActiveModulations =>
        Modulations ?? new Dictionary<string, CanonicalModulation>(StringComparer.OrdinalIgnoreCase);
}

public sealed record CanonicalHybridResult(
    bool Applied,
    CanonicalHybridState State,
    IReadOnlyList<string> Events,
    IReadOnlyList<string> Rejections);

/// <summary>
/// Hybrid canonical runtime: discrete mission operations are applied first;
/// continuous trajectories are then advanced independently of either console.
/// </summary>
public static class CanonicalHybridRuntime
{
    public static CanonicalHybridResult Apply(
        CanonicalHybridState state,
        CanonicalMissionOperation operation,
        CanonicalOperationRegistry? registry = null)
    {
        var result = CanonicalOperationEngine.Apply(state.Session, operation, registry);
        if (!result.Applied)
            return new(false, state, result.Events, result.Rejections);

        var fades = state.Fades.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var modulations = state.ActiveModulations.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var nextSession = result.State;
        var events = result.Events.ToList();
        if (operation.CanonicalIntent == "start_attribute_modulation" &&
            CanonicalModulationParser.TryParse(operation.Arguments, nextSession.SelectionOrder, out var modulation, out _))
        {
            modulations[modulation.Key] = modulation;
            nextSession = ApplyModulations(nextSession, modulations.Values, state.TimeSeconds);
        }
        else if (operation.CanonicalIntent == "stop_attribute_modulation" &&
                 operation.Arguments.TryGetValue("key", out var modulationKey))
        {
            modulations.Remove(modulationKey);
        }
        var duration = ReadDuration(operation.Arguments);
        if (duration > 0 &&
            operation.CanonicalIntent == "trigger_or_adjust_live_playback" &&
            operation.Arguments.TryGetValue("playback", out var playback) &&
            nextSession.PlaybackLevels.TryGetValue(playback, out var target))
        {
            var start = state.Session.PlaybackLevels.TryGetValue(playback, out var current) ? current : 0d;
            var playbackValues = nextSession.PlaybackLevels.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            playbackValues[playback] = start;
            nextSession = nextSession with { PlaybackLevels = playbackValues };
            fades[playback] = new CanonicalFade(playback, start, target, duration);
            events.Add($"fade_scheduled:{playback}:{duration.ToString(CultureInfo.InvariantCulture)}");
        }

        return new(true, new CanonicalHybridState(nextSession, fades, state.TimeSeconds, modulations), events, result.Rejections);
    }

    public static CanonicalHybridResult Advance(
        CanonicalHybridState state,
        double deltaSeconds)
    {
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0)
            return new(false, state, Array.Empty<string>(), new[] { "advance_requires_non_negative_finite_seconds" });
        if (deltaSeconds == 0)
            return new(true, state, Array.Empty<string>(), Array.Empty<string>());

        var playbackValues = state.Session.PlaybackLevels.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var nextFades = new Dictionary<string, CanonicalFade>(StringComparer.OrdinalIgnoreCase);
        var events = new List<string>();
        foreach (var fade in state.Fades.Values)
        {
            var next = fade.Advance(deltaSeconds);
            playbackValues[next.Key] = next.Value;
            if (next.Complete)
            {
                playbackValues[next.Key] = next.Target;
                events.Add($"fade_complete:{next.Key}");
            }
            else
            {
                nextFades[next.Key] = next;
            }
        }

        var nextTime = state.TimeSeconds + deltaSeconds;
        var session = state.Session with { PlaybackLevels = playbackValues };
        session = ApplyModulations(session, state.ActiveModulations.Values, nextTime);
        var nextModulations = state.ActiveModulations.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        return new(true, new CanonicalHybridState(session, nextFades, nextTime, nextModulations), events, Array.Empty<string>());
    }

    private static CanonicalSessionState ApplyModulations(
        CanonicalSessionState session,
        IEnumerable<CanonicalModulation> modulations,
        double timeSeconds)
    {
        var fixtures = session.FixtureAttributes.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToDictionary(value => value.Key, value => value.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        foreach (var modulation in modulations)
        foreach (var fixture in modulation.Fixtures)
        {
            if (!fixtures.TryGetValue(fixture, out var values))
            {
                values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                fixtures[fixture] = values;
            }
            values[modulation.Attribute] = modulation.ValueAt(timeSeconds);
        }
        return session with
        {
            FixtureAttributes = fixtures.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyDictionary<string, double>)pair.Value,
                StringComparer.OrdinalIgnoreCase)
        };
    }

    private static double ReadDuration(IReadOnlyDictionary<string, string> arguments)
    {
        if (!arguments.TryGetValue("fade", out var rawFade) && !arguments.TryGetValue("fadeSeconds", out rawFade))
            return 0d;
        return double.TryParse(rawFade, NumberStyles.Float, CultureInfo.InvariantCulture, out var duration)
            ? Math.Max(0d, duration)
            : 0d;
    }
}
