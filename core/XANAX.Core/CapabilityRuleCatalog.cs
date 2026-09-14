namespace Xanax.Core;

public static class CapabilityRuleCatalog
{
    public static IReadOnlyList<DerivedCapabilityRule> Standard(
        CanonicalLightingState source,
        FixturePersonality target)
    {
        var targetAttributes = target.Channels
            .Where(channel => !channel.Virtual)
            .Select(channel => channel.Attribute)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rules = new List<DerivedCapabilityRule>();

        AddComplementRule(rules, targetAttributes, source, "Cyan", "Red");
        AddComplementRule(rules, targetAttributes, source, "Magenta", "Green");
        AddComplementRule(rules, targetAttributes, source, "Yellow", "Blue");
        AddComplementRule(rules, targetAttributes, source, "Red", "Cyan");
        AddComplementRule(rules, targetAttributes, source, "Green", "Magenta");
        AddComplementRule(rules, targetAttributes, source, "Blue", "Yellow");
        return rules;
    }

    private static void AddComplementRule(
        ICollection<DerivedCapabilityRule> rules,
        IReadOnlySet<string> targetAttributes,
        CanonicalLightingState source,
        string target,
        string sourceAttribute)
    {
        if (targetAttributes.Contains(target) && source.Values.ContainsKey(sourceAttribute))
            rules.Add(new DerivedCapabilityRule(target, new Dictionary<string, double> { [sourceAttribute] = -1d }, 1d));
    }
}
