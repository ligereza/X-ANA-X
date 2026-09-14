namespace Xanax.Core;

public sealed record PersonalityPatchMapping(
    string FixtureId,
    FixturePersonality SourcePersonality,
    int SourceUniverse,
    int SourceStartAddress,
    FixturePersonality TargetPersonality,
    int TargetUniverse,
    int TargetStartAddress);

public sealed record PersonalityPatchBridgeResult(
    PatchedDmxFrame Frame,
    IReadOnlyDictionary<string, CanonicalLightingState> CanonicalStates,
    IReadOnlyDictionary<string, PersonalityBridgeResult> FixtureResults,
    IReadOnlyList<string> Errors,
    IReadOnlyList<DmxAddress> Collisions)
{
    public bool Succeeded => Errors.Count == 0;
}

/// <summary>
/// Translates an entire fixture patch through canonical physical values. The
/// source and target may use different personalities, addresses and resolutions.
/// </summary>
public static class PersonalityPatchBridge
{
    public static PersonalityPatchBridgeResult Translate(
        IEnumerable<PersonalityPatchMapping> mappings,
        CanonicalSessionState state)
    {
        var slots = new Dictionary<DmxAddress, byte>();
        var canonicalStates = new Dictionary<string, CanonicalLightingState>(StringComparer.OrdinalIgnoreCase);
        var fixtureResults = new Dictionary<string, PersonalityBridgeResult>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var collisions = new List<DmxAddress>();

        foreach (var mapping in mappings)
        {
            if (!state.FixtureAttributes.TryGetValue(mapping.FixtureId, out var sourceValues))
            {
                errors.Add($"missing_fixture_state:{mapping.FixtureId}");
                continue;
            }
            if (!ValidAddress(mapping.TargetUniverse, mapping.TargetStartAddress))
            {
                errors.Add($"invalid_target_patch_address:{mapping.FixtureId}");
                continue;
            }

            var sourceEncoded = mapping.SourcePersonality.Encode(sourceValues);
            var canonical = PersonalityBridge.Decode(mapping.SourcePersonality, sourceEncoded);
            var targetResult = PersonalityBridge.TranslateAuto(mapping.TargetPersonality, canonical);
            canonicalStates[mapping.FixtureId] = canonical;
            fixtureResults[mapping.FixtureId] = targetResult;
            foreach (var unsupported in targetResult.UnsupportedAttributes)
                errors.Add($"unsupported_attribute:{mapping.FixtureId}:{unsupported}");

            foreach (var pair in targetResult.Frame.Slots)
            {
                var absoluteChannel = mapping.TargetStartAddress + pair.Key - 1;
                if (absoluteChannel is < 1 or > 512)
                {
                    errors.Add($"target_fixture_exceeds_universe:{mapping.FixtureId}:{absoluteChannel}");
                    continue;
                }
                var address = new DmxAddress(mapping.TargetUniverse, absoluteChannel);
                if (slots.ContainsKey(address))
                {
                    collisions.Add(address);
                    errors.Add($"target_dmx_collision:{address.Universe}:{address.Channel}");
                    continue;
                }
                slots[address] = pair.Value;
            }
        }

        return new(new PatchedDmxFrame(slots), canonicalStates, fixtureResults, errors, collisions);
    }

    private static bool ValidAddress(int universe, int startAddress) =>
        universe >= 1 && startAddress is >= 1 and <= 512;
}
