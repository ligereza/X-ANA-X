namespace Xanax.Core;

public sealed record CanonicalLightingState(IReadOnlyDictionary<string, double> Values)
{
    public double this[string attribute] => Values.TryGetValue(attribute, out var value) ? value : 0d;
}

public sealed record DerivedCapabilityRule(
    string OutputAttribute,
    IReadOnlyDictionary<string, double> Coefficients,
    double Constant = 0)
{
    public bool CanEvaluate(IReadOnlyDictionary<string, double> values) =>
        Coefficients.Keys.All(values.ContainsKey);

    public double Evaluate(IReadOnlyDictionary<string, double> values) =>
        Constant + Coefficients.Sum(pair => pair.Value * values[pair.Key]);
}

public sealed record CapabilityResolution(
    string Attribute,
    string Kind,
    string? Rule,
    double? Value);

public sealed record DmxFrame(IReadOnlyDictionary<int, byte> Slots)
{
    public byte this[int address] => Slots.TryGetValue(address, out var value) ? value : (byte)0;
}

public sealed record PersonalityBridgeResult(
    CanonicalLightingState State,
    IReadOnlyList<DmxValue> EncodedValues,
    DmxFrame Frame,
    IReadOnlyList<string> UnsupportedAttributes)
{
    public IReadOnlyList<string> DerivedAttributes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CapabilityResolution> Resolutions { get; init; } = Array.Empty<CapabilityResolution>();
}

public static class PersonalityBridge
{
    public static PersonalityBridgeResult TranslateAuto(
        FixturePersonality target,
        CanonicalLightingState state) =>
        Translate(target, state, CapabilityRuleCatalog.Standard(state, target));

    public static CanonicalLightingState Decode(
        FixturePersonality source,
        IEnumerable<DmxValue> values)
    {
        return new CanonicalLightingState(source.DecodeAttributes(values));
    }

    public static PersonalityBridgeResult Translate(
        FixturePersonality target,
        CanonicalLightingState state,
        IEnumerable<DerivedCapabilityRule>? derivedRules = null)
        =>
        CapabilityProjectionEngine.Translate(target, state, derivedRules);
}

public static class DmxFrameEncoder
{
    public static DmxFrame Encode(IEnumerable<DmxValue> values)
    {
        var slots = new Dictionary<int, byte>();
        foreach (var value in values)
        {
            var byteCount = (value.ResolutionBits + 7) / 8;
            for (var index = 0; index < byteCount; index++)
            {
                var shift = (byteCount - index - 1) * 8;
                slots[value.Address + index] = (byte)((value.RawValue >> shift) & 0xff);
            }
        }
        return new DmxFrame(slots);
    }
}

public static class PersonalityModelFactory
{
    public static BehaviorModel Create(FixturePersonality personality, string? name = null)
    {
        var channels = personality.Channels
            .Where(c => !c.Virtual)
            .GroupBy(c => c.Attribute, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        if (channels.Length == 0)
            throw new ArgumentException("The personality has no physical control channels.", nameof(personality));

        return new BehaviorModel(
            name ?? $"{personality.Name}:{personality.Mode}",
            channels.Select(c => new ControlParameter(c.Attribute, c.PhysicalFrom, c.PhysicalTo)),
            channels.Select(c => c.Attribute),
            parameters => parameters.ToArray());
    }
}
