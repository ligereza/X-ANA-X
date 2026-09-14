using System.Globalization;

namespace Xanax.Core;

public sealed record CanonicalSessionState(
    IReadOnlyList<string> SelectionOrder,
    IReadOnlyDictionary<string, double> AttributeValues,
    IReadOnlyDictionary<string, double> PlaybackLevels,
    string Environment = "live")
{
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> FixtureAttributes { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, string> ReusableReferences { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, string> TimeOrderedLooks { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record CanonicalExecutionResult(
    bool Applied,
    CanonicalSessionState State,
    IReadOnlyList<string> Events,
    IReadOnlyList<string> Rejections);

public static class CanonicalOperationEngine
{
    public static CanonicalExecutionResult Apply(
        CanonicalSessionState state,
        CanonicalMissionOperation operation) => Apply(state, operation, null);

    public static CanonicalExecutionResult Apply(
        CanonicalSessionState state,
        CanonicalMissionOperation operation,
        CanonicalOperationRegistry? registry)
    {
        var selection = state.SelectionOrder.ToList();
        var attributes = state.AttributeValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var playbacks = state.PlaybackLevels.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var reusableReferences = state.ReusableReferences.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var timeOrderedLooks = state.TimeOrderedLooks.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var nextEnvironment = state.Environment;
        var fixtureAttributes = state.FixtureAttributes.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToDictionary(value => value.Key, value => value.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        var events = new List<string>();
        var rejections = new List<string>();

        switch (operation.CanonicalIntent)
        {
            case "select_fixture_set":
                ApplySelection(operation.Arguments, selection, events, rejections);
                break;
            case "edit_fixture_attribute":
                ApplyAttribute(operation.Arguments, selection, attributes, fixtureAttributes, events, rejections);
                break;
            case "recall_reusable_attribute_values":
                ApplyReusableReference(operation.Arguments, reusableReferences, events, rejections);
                break;
            case "store_or_edit_time_ordered_look":
                ApplyTimeOrderedLook(operation.Arguments, timeOrderedLooks, events, rejections);
                break;
            case "edit_without_or_with_live_output":
                ApplyEnvironment(operation.Arguments, state.Environment, events, rejections, out nextEnvironment);
                break;
            case "start_attribute_modulation":
                if (CanonicalModulationParser.TryParse(operation.Arguments, selection, out var modulation, out var modulationError))
                    events.Add($"modulation_started:{modulation.Key}");
                else
                    rejections.Add(modulationError);
                break;
            case "stop_attribute_modulation":
                if (!operation.Arguments.TryGetValue("key", out var modulationKey) || string.IsNullOrWhiteSpace(modulationKey))
                    rejections.Add("modulation_stop_requires_key");
                else
                    events.Add($"modulation_stopped:{modulationKey}");
                break;
            case "transform_ordered_fixture_selection":
                ApplySelectionTransform(operation.Arguments, selection, events, rejections);
                break;
            case "trigger_or_adjust_live_playback":
                ApplyPlayback(operation.Arguments, playbacks, events, rejections);
                break;
            case "stop_or_release_active_output":
                if (operation.Arguments.TryGetValue("playback", out var playback) && !string.IsNullOrWhiteSpace(playback))
                {
                    playbacks.Remove(playback);
                    events.Add($"playback_released:{playback}");
                }
                else
                {
                    playbacks.Clear();
                    events.Add("all_active_playbacks_released");
                }
                break;
            default:
                if (registry is not null && registry.TryApply(state, operation, out var extensionResult))
                    return extensionResult;
                rejections.Add($"unsupported_canonical_intent:{operation.CanonicalIntent}");
                break;
        }

        if (rejections.Count > 0)
            return new(false, state, events, rejections);
        var next = new CanonicalSessionState(selection, attributes, playbacks,
            operation.CanonicalIntent == "edit_without_or_with_live_output" ? nextEnvironment : state.Environment)
        {
            FixtureAttributes = fixtureAttributes.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyDictionary<string, double>)pair.Value,
                StringComparer.OrdinalIgnoreCase),
            ReusableReferences = reusableReferences,
            TimeOrderedLooks = timeOrderedLooks
        };
        return new(true, next, events, rejections);
    }

    private static void ApplySelection(
        IReadOnlyDictionary<string, string> arguments,
        List<string> selection,
        List<string> events,
        List<string> rejections)
    {
        if (!arguments.TryGetValue("fixtures", out var rawFixtures))
        {
            rejections.Add("selection_requires_fixtures");
            return;
        }
        var fixtures = rawFixtures
            .Split(new[] { ',', ';', '|', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (fixtures.Length == 0)
        {
            rejections.Add("selection_cannot_be_empty");
            return;
        }

        var mode = arguments.TryGetValue("mode", out var requestedMode) ? requestedMode.ToLowerInvariant() : "replace";
        if (mode == "add")
            selection.AddRange(fixtures.Where(fixture => !selection.Contains(fixture, StringComparer.OrdinalIgnoreCase)));
        else if (mode == "toggle")
        {
            foreach (var fixture in fixtures)
            {
                var index = selection.FindIndex(existing => string.Equals(existing, fixture, StringComparison.OrdinalIgnoreCase));
                if (index >= 0) selection.RemoveAt(index);
                else selection.Add(fixture);
            }
        }
        else if (mode == "replace")
        {
            selection.Clear();
            selection.AddRange(fixtures);
        }
        else
        {
            rejections.Add($"unknown_selection_mode:{mode}");
            return;
        }
        events.Add($"selection_{mode}:{string.Join(",", selection)}");
    }

    private static void ApplyAttribute(
        IReadOnlyDictionary<string, string> arguments,
        IReadOnlyList<string> selection,
        IDictionary<string, double> attributes,
        IDictionary<string, Dictionary<string, double>> fixtureAttributes,
        List<string> events,
        List<string> rejections)
    {
        if (selection.Count == 0)
        {
            rejections.Add("attribute_edit_requires_selection");
            return;
        }
        if (!arguments.TryGetValue("attribute", out var attribute) || string.IsNullOrWhiteSpace(attribute))
        {
            rejections.Add("attribute_edit_requires_attribute");
            return;
        }
        if (!arguments.TryGetValue("value", out var rawValue) ||
            !double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            rejections.Add("attribute_edit_requires_numeric_value");
            return;
        }

        var relative = arguments.TryGetValue("mode", out var mode) && string.Equals(mode, "relative", StringComparison.OrdinalIgnoreCase);
        var delta = value;
        var spread = 0d;
        var hasSpread = arguments.TryGetValue("spread", out var rawSpread) &&
            double.TryParse(rawSpread, NumberStyles.Float, CultureInfo.InvariantCulture, out spread);
        if (!DistributionMath.TryCreate(arguments, out var distribution, out var distributionError))
        {
            rejections.Add(distributionError);
            return;
        }
        if (relative && attributes.TryGetValue(attribute, out var current))
            value += current;

        attributes[attribute] = value;
        for (var index = 0; index < selection.Count; index++)
        {
            var fixture = selection[index];
            if (!fixtureAttributes.TryGetValue(fixture, out var fixtureValues))
            {
                fixtureValues = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                fixtureAttributes[fixture] = fixtureValues;
            }
            var fixtureValue = value;
            if (hasSpread)
                fixtureValue += DistributionMath.Offset(distribution, index, selection.Count, spread);
            if (relative && fixtureValues.TryGetValue(attribute, out var fixtureCurrent))
                fixtureValue = fixtureCurrent + delta + (hasSpread && selection.Count > 1
                    ? DistributionMath.Offset(distribution, index, selection.Count, spread)
                    : 0d);
            fixtureValues[attribute] = fixtureValue;
        }
        var spreadLabel = hasSpread ? $",spread={spread.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
        var distributionLabel = hasSpread ? $",distribution={distribution.Shape},frequency={distribution.Frequency.ToString(CultureInfo.InvariantCulture)},phase={distribution.Phase.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
        events.Add($"attribute_{(relative ? "relative" : "absolute")}:{attribute}={value.ToString(CultureInfo.InvariantCulture)}{spreadLabel}{distributionLabel}");
    }

    private static void ApplySelectionTransform(
        IReadOnlyDictionary<string, string> arguments,
        List<string> selection,
        List<string> events,
        List<string> rejections)
    {
        if (selection.Count == 0)
        {
            rejections.Add("selection_transform_requires_selection");
            return;
        }
        var transform = arguments.TryGetValue("transform", out var requestedTransform)
            ? requestedTransform.ToLowerInvariant()
            : "reverse";
        try
        {
            var transformed = SelectionTransformMath.Apply(selection, arguments);
            selection.Clear();
            selection.AddRange(transformed);
        }
        catch (ArgumentException)
        {
            rejections.Add($"unknown_selection_transform:{transform}");
            return;
        }
        events.Add($"selection_transform_{transform}:{string.Join(",", selection)}");
    }

    private static void ApplyPlayback(
        IReadOnlyDictionary<string, string> arguments,
        IDictionary<string, double> playbacks,
        List<string> events,
        List<string> rejections)
    {
        if (!arguments.TryGetValue("playback", out var playback) || string.IsNullOrWhiteSpace(playback))
        {
            rejections.Add("playback_requires_id");
            return;
        }
        var action = arguments.TryGetValue("action", out var requestedAction) ? requestedAction.ToLowerInvariant() : "go";
        if (action is "kill" or "off" or "release")
        {
            playbacks.Remove(playback);
            events.Add($"playback_{action}:{playback}");
            return;
        }
        if (action is "go" or "flash")
        {
            var level = arguments.TryGetValue("level", out var rawLevel) && double.TryParse(rawLevel, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLevel)
                ? Math.Clamp(parsedLevel, 0, 1)
                : 1d;
            playbacks[playback] = level;
            events.Add($"playback_{action}:{playback}={level.ToString(CultureInfo.InvariantCulture)}");
            return;
        }
        rejections.Add($"unknown_playback_action:{action}");
    }

    private static void ApplyReusableReference(
        IReadOnlyDictionary<string, string> arguments,
        IDictionary<string, string> references,
        ICollection<string> events,
        ICollection<string> rejections)
    {
        var reference = arguments.TryGetValue("preset", out var preset) && !string.IsNullOrWhiteSpace(preset)
            ? preset
            : arguments.TryGetValue("palette", out var palette) && !string.IsNullOrWhiteSpace(palette)
                ? palette
                : arguments.TryGetValue("paletteHandle", out var handle) && !string.IsNullOrWhiteSpace(handle)
                    ? handle
                    : string.Empty;
        if (string.IsNullOrWhiteSpace(reference))
        {
            rejections.Add("reusable_reference_requires_preset_or_palette");
            return;
        }
        var key = arguments.TryGetValue("featureGroup", out var featureGroup) && !string.IsNullOrWhiteSpace(featureGroup)
            ? $"{featureGroup}:{reference}"
            : reference;
        references[key] = string.Join(";", arguments.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => $"{pair.Key}={pair.Value}"));
        events.Add($"reusable_reference_recalled:{key}");
    }

    private static void ApplyTimeOrderedLook(
        IReadOnlyDictionary<string, string> arguments,
        IDictionary<string, string> looks,
        ICollection<string> events,
        ICollection<string> rejections)
    {
        var cue = arguments.TryGetValue("cue", out var rawCue) ? rawCue :
            arguments.TryGetValue("cueNumber", out var cueNumber) ? cueNumber : string.Empty;
        if (string.IsNullOrWhiteSpace(cue))
        {
            rejections.Add("time_ordered_look_requires_cue");
            return;
        }
        var sequence = arguments.TryGetValue("sequence", out var rawSequence) ? rawSequence :
            arguments.TryGetValue("sequenceNumber", out var sequenceNumber) ? sequenceNumber : "default";
        var key = $"{sequence}:{cue}";
        looks[key] = string.Join(";", arguments.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => $"{pair.Key}={pair.Value}"));
        events.Add($"time_ordered_look_stored:{key}");
    }

    private static void ApplyEnvironment(
        IReadOnlyDictionary<string, string> arguments,
        string current,
        ICollection<string> events,
        ICollection<string> rejections,
        out string next)
    {
        next = current;
        if (!arguments.TryGetValue("environment", out var environment) ||
            environment is not ("live" or "preview" or "blind"))
        {
            rejections.Add("environment_requires_live_preview_or_blind");
            return;
        }
        next = environment;
        events.Add($"environment:{next}");
    }

}
