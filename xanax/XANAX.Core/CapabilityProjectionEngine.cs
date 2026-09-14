namespace Xanax.Core;

/// <summary>
/// Resolves a canonical state into a target personality by closure over
/// capability rules. A target may therefore realize a function directly,
/// through one rule, or through a deterministic composition of rules.
/// </summary>
public static class CapabilityProjectionEngine
{
    public static PersonalityBridgeResult Translate(
        FixturePersonality target,
        CanonicalLightingState state,
        IEnumerable<DerivedCapabilityRule>? rules = null,
        int maxPasses = 32)
    {
        var supported = target.Channels
            .Where(channel => !channel.Virtual)
            .Select(channel => channel.Attribute)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var targetAttributes = supported.ToArray();
        var effective = state.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var origins = state.Values.Keys.ToDictionary(
            key => key,
            key => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { key },
            StringComparer.OrdinalIgnoreCase);
        var resolutions = new List<CapabilityResolution>();
        var derived = new List<string>();
        var appliedRules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var availableRules = (rules ?? Array.Empty<DerivedCapabilityRule>()).ToArray();

        foreach (var source in state.Values.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var resolved = CanonicalAttributeVocabulary.Resolve(source.Key, targetAttributes, out var renamed);
            if (resolved is null)
                continue;
            if (effective.ContainsKey(resolved))
            {
                if (string.Equals(resolved, source.Key, StringComparison.OrdinalIgnoreCase))
                    resolutions.Add(new(resolved, "direct", null, source.Value));
                continue;
            }
            effective[resolved] = source.Value;
            origins[resolved] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { source.Key };
            resolutions.Add(new(resolved, renamed ? "renamed" : "direct", null, source.Value));
        }

        for (var pass = 0; pass < Math.Max(1, maxPasses); pass++)
        {
            var changed = false;
            foreach (var rule in availableRules)
            {
                var resolvedOutput = CanonicalAttributeVocabulary.Resolve(rule.OutputAttribute, targetAttributes, out _)
                    ?? rule.OutputAttribute;
                var resolvedRule = rule with { OutputAttribute = resolvedOutput };
                if (effective.ContainsKey(resolvedOutput) || !resolvedRule.CanEvaluate(effective))
                    continue;

                var value = resolvedRule.Evaluate(effective);
                if (double.IsNaN(value) || double.IsInfinity(value))
                    continue;

                effective[resolvedOutput] = value;
                origins[resolvedOutput] = resolvedRule.Coefficients
                    .SelectMany(coefficient => origins.TryGetValue(coefficient.Key, out var origin)
                        ? origin
                        : new HashSet<string>(StringComparer.OrdinalIgnoreCase))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                appliedRules.Add(Describe(resolvedRule));
                changed = true;

                if (supported.Contains(resolvedOutput))
                {
                    derived.Add(resolvedOutput);
                    resolutions.Add(new(resolvedOutput, "derived", Describe(resolvedRule), value));
                }
            }

            if (!changed)
                break;
        }

        var resolvedSourceAttributes = origins
            .Where(pair => supported.Contains(pair.Key) || derived.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
            .SelectMany(pair => pair.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unsupported = state.Values.Keys
            .Where(attribute => !resolvedSourceAttributes.Contains(attribute))
            .OrderBy(attribute => attribute, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var encoded = target.Encode(effective);
        return new(
            state,
            encoded,
            DmxFrameEncoder.Encode(encoded),
            unsupported)
        {
            DerivedAttributes = derived.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Resolutions = resolutions
        };
    }

    private static string Describe(DerivedCapabilityRule rule) =>
        $"{rule.OutputAttribute}={rule.Constant.ToString(System.Globalization.CultureInfo.InvariantCulture)}+" +
        string.Join("+", rule.Coefficients.Select(pair => $"({pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}*{pair.Key})"));
}
