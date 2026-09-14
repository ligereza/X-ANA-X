namespace Xanax.Core;

public readonly record struct DmxBreakSlot(int DmxBreak, int Address);

public sealed record MultiBreakDmxFrame(IReadOnlyDictionary<DmxBreakSlot, byte> Slots)
{
    public byte this[int dmxBreak, int address] =>
        Slots.TryGetValue(new DmxBreakSlot(dmxBreak, address), out var value) ? value : (byte)0;
}

public static class MultiBreakDmxFrameEncoder
{
    public static MultiBreakDmxFrame Encode(IEnumerable<DmxValue> values)
    {
        var slots = new Dictionary<DmxBreakSlot, byte>();
        foreach (var value in values)
        {
            var byteCount = (value.ResolutionBits + 7) / 8;
            for (var index = 0; index < byteCount; index++)
            {
                var shift = (byteCount - index - 1) * 8;
                var slot = new DmxBreakSlot(value.DmxBreak, value.Address + index);
                slots[slot] = (byte)((value.RawValue >> shift) & 0xff);
            }
        }
        return new(slots);
    }
}

public sealed record MultiBreakPatchedDmxFrame(IReadOnlyDictionary<DmxAddress, byte> Slots) : IDmxUniverseFrame
{
    public byte this[int universe, int channel] =>
        Slots.TryGetValue(new DmxAddress(universe, channel), out var value) ? value : (byte)0;
}

public sealed record MultiBreakPatchCompileResult(
    MultiBreakPatchedDmxFrame Frame,
    IReadOnlyList<string> Errors,
    IReadOnlyList<DmxAddress> Collisions)
{
    public bool Succeeded => Errors.Count == 0;
}

public static class MultiBreakPatchCompiler
{
    public static MultiBreakPatchCompileResult Compile(
        IEnumerable<PatchedFixture> patch,
        CanonicalSessionState state,
        IReadOnlyDictionary<int, int>? breakUniverses = null)
    {
        var slots = new Dictionary<DmxAddress, byte>();
        var errors = new List<string>();
        var collisions = new List<DmxAddress>();
        foreach (var fixture in patch)
        {
            if (!state.FixtureAttributes.TryGetValue(fixture.Id, out var values))
                continue;
            if (fixture.Universe < 1 || fixture.StartAddress is < 1 or > 512)
            {
                errors.Add($"invalid_patch_address:{fixture.Id}");
                continue;
            }

            var frame = fixture.Personality.EncodeMultiBreak(values);
            foreach (var pair in frame.Slots)
            {
                var universe = breakUniverses is not null && breakUniverses.TryGetValue(pair.Key.DmxBreak, out var mappedUniverse)
                    ? mappedUniverse
                    : pair.Key.DmxBreak == 1 ? fixture.Universe : 0;
                if (universe < 1)
                {
                    errors.Add($"unmapped_dmx_break:{fixture.Id}:{pair.Key.DmxBreak}");
                    continue;
                }
                var channel = fixture.StartAddress + pair.Key.Address - 1;
                if (channel is < 1 or > 512)
                {
                    errors.Add($"fixture_exceeds_universe:{fixture.Id}:{channel}");
                    continue;
                }
                var address = new DmxAddress(universe, channel);
                if (slots.ContainsKey(address))
                {
                    collisions.Add(address);
                    errors.Add($"dmx_collision:{address.Universe}:{address.Channel}");
                    continue;
                }
                slots[address] = pair.Value;
            }
        }
        return new(new MultiBreakPatchedDmxFrame(slots), errors, collisions);
    }
}
