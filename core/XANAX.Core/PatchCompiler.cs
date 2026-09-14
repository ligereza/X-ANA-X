namespace Xanax.Core;

public readonly record struct DmxAddress(int Universe, int Channel);

public sealed record PatchedFixture(
    string Id,
    FixturePersonality Personality,
    int Universe,
    int StartAddress);

public sealed record PatchedDmxFrame(IReadOnlyDictionary<DmxAddress, byte> Slots) : IDmxUniverseFrame
{
    public byte this[int universe, int channel] =>
        Slots.TryGetValue(new DmxAddress(universe, channel), out var value) ? value : (byte)0;
}

public sealed record PatchCompileResult(
    PatchedDmxFrame Frame,
    IReadOnlyList<string> Errors,
    IReadOnlyList<DmxAddress> Collisions)
{
    public bool Succeeded => Errors.Count == 0;
}

public static class PatchCompiler
{
    public static PatchCompileResult Compile(
        IEnumerable<PatchedFixture> patch,
        CanonicalSessionState state)
    {
        var slots = new Dictionary<DmxAddress, byte>();
        var errors = new List<string>();
        var collisions = new List<DmxAddress>();

        foreach (var fixture in patch)
        {
            if (fixture.Universe < 1 || fixture.StartAddress < 1 || fixture.StartAddress > 512)
            {
                errors.Add($"invalid_patch_address:{fixture.Id}");
                continue;
            }
            if (!state.FixtureAttributes.TryGetValue(fixture.Id, out var values))
                continue;

            var relative = DmxFrameEncoder.Encode(fixture.Personality.Encode(values)).Slots;
            foreach (var pair in relative)
            {
                var absoluteChannel = fixture.StartAddress + pair.Key - 1;
                var address = new DmxAddress(fixture.Universe, absoluteChannel);
                if (absoluteChannel > 512)
                {
                    errors.Add($"fixture_exceeds_universe:{fixture.Id}:{absoluteChannel}");
                    continue;
                }
                if (slots.ContainsKey(address))
                {
                    collisions.Add(address);
                    errors.Add($"dmx_collision:{address.Universe}:{address.Channel}");
                    continue;
                }
                slots[address] = pair.Value;
            }
        }

        return new(new PatchedDmxFrame(slots), errors, collisions);
    }
}
