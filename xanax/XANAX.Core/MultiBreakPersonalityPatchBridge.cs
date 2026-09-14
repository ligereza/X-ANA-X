namespace Xanax.Core;

public sealed record MultiBreakPersonalityPatchMapping(
    string FixtureId,
    FixturePersonality SourcePersonality,
    int SourceUniverse,
    int SourceStartAddress,
    FixturePersonality TargetPersonality,
    int TargetUniverse,
    int TargetStartAddress,
    IReadOnlyDictionary<int, int>? TargetBreakUniverses = null);

public sealed record MultiBreakPersonalityPatchBridgeResult(
    MultiBreakPatchedDmxFrame Frame,
    IReadOnlyDictionary<string, CanonicalLightingState> CanonicalStates,
    IReadOnlyDictionary<string, PersonalityBridgeResult> FixtureResults,
    IReadOnlyList<string> Errors,
    IReadOnlyList<DmxAddress> Collisions)
{
    public bool Succeeded => Errors.Count == 0;
}

/// <summary>
/// The multi-break form of the personality bridge. Break identity is kept
/// until absolute patching, so an identical local address on two DMX breaks
/// cannot overwrite the other break.
/// </summary>
public static class MultiBreakPersonalityPatchBridge
{
    public static MultiBreakPersonalityPatchBridgeResult Translate(
        IEnumerable<MultiBreakPersonalityPatchMapping> mappings,
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
            if (mapping.TargetUniverse < 1 || mapping.TargetStartAddress is < 1 or > 512)
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

            foreach (var value in targetResult.EncodedValues)
            {
                var universe = mapping.TargetBreakUniverses is not null && mapping.TargetBreakUniverses.TryGetValue(value.DmxBreak, out var mappedUniverse)
                    ? mappedUniverse
                    : value.DmxBreak == 1 ? mapping.TargetUniverse : 0;
                if (universe < 1)
                {
                    errors.Add($"unmapped_target_dmx_break:{mapping.FixtureId}:{value.DmxBreak}");
                    continue;
                }

                var byteCount = (value.ResolutionBits + 7) / 8;
                for (var index = 0; index < byteCount; index++)
                {
                    var channel = mapping.TargetStartAddress + value.Address + index - 1;
                    if (channel is < 1 or > 512)
                    {
                        errors.Add($"target_fixture_exceeds_universe:{mapping.FixtureId}:{universe}:{channel}");
                        continue;
                    }
                    var address = new DmxAddress(universe, channel);
                    if (slots.ContainsKey(address))
                    {
                        collisions.Add(address);
                        errors.Add($"target_dmx_collision:{address.Universe}:{address.Channel}");
                        continue;
                    }
                    var shift = (byteCount - index - 1) * 8;
                    slots[address] = (byte)((value.RawValue >> shift) & 0xff);
                }
            }
        }

        return new(new MultiBreakPatchedDmxFrame(slots), canonicalStates, fixtureResults, errors, collisions);
    }
}
