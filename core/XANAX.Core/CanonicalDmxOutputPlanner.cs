namespace Xanax.Core;

public sealed record CanonicalDmxOutputPlan(
    PersonalityPatchBridgeResult Translation,
    DmxOutputPlan? ArtNet,
    IReadOnlyList<SacnDmxPacket> Sacn,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}

public sealed record MultiBreakCanonicalDmxOutputPlan(
    MultiBreakPersonalityPatchBridgeResult Translation,
    DmxOutputPlan? ArtNet,
    IReadOnlyList<SacnDmxPacket> Sacn,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}

/// <summary>
/// End-to-end pure planner for canonical lighting output. It translates a
/// source personality into a target personality and then produces protocol
/// packets without opening a socket or authorizing physical output.
/// </summary>
public static class CanonicalDmxOutputPlanner
{
    public static MultiBreakCanonicalDmxOutputPlan CompileMultiBreakArtNet(
        IEnumerable<MultiBreakPersonalityPatchMapping> mappings,
        CanonicalSessionState state,
        IEnumerable<int> universes,
        byte sequence = 0,
        byte physical = 0)
    {
        var translation = MultiBreakPersonalityPatchBridge.Translate(mappings, state);
        var errors = translation.Errors.ToList();
        DmxOutputPlan? output = null;
        if (errors.Count == 0)
        {
            try
            {
                output = ArtNetEncoder.Compile(translation.Frame, universes, sequence, physical);
                errors.AddRange(output.Errors);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                errors.Add($"invalid_artnet_configuration:{exception.ParamName}");
            }
        }
        return new(translation, output, Array.Empty<SacnDmxPacket>(), errors);
    }

    public static MultiBreakCanonicalDmxOutputPlan CompileMultiBreakSacn(
        IEnumerable<MultiBreakPersonalityPatchMapping> mappings,
        CanonicalSessionState state,
        IEnumerable<int> universes,
        Guid cid,
        string sourceName,
        byte sequence = 0,
        byte priority = 100,
        bool previewData = false)
    {
        var translation = MultiBreakPersonalityPatchBridge.Translate(mappings, state);
        var errors = translation.Errors.ToList();
        var packets = new List<SacnDmxPacket>();
        if (errors.Count == 0)
        {
            foreach (var universe in universes.Distinct().OrderBy(value => value))
            {
                try
                {
                    packets.Add(SacnEncoder.Encode(translation.Frame, universe, cid, sourceName, sequence, priority, previewData));
                }
                catch (ArgumentOutOfRangeException exception)
                {
                    errors.Add($"invalid_sacn_configuration:{exception.ParamName}");
                }
                catch (ArgumentException exception)
                {
                    errors.Add($"invalid_sacn_configuration:{exception.Message}");
                }
            }
        }
        return new(translation, null, packets, errors);
    }

    public static CanonicalDmxOutputPlan CompileArtNet(
        IEnumerable<PersonalityPatchMapping> mappings,
        CanonicalSessionState state,
        IEnumerable<int> universes,
        byte sequence = 0,
        byte physical = 0)
    {
        var translation = PersonalityPatchBridge.Translate(mappings, state);
        var errors = translation.Errors.ToList();
        DmxOutputPlan? output = null;
        if (errors.Count == 0)
        {
            try
            {
                output = ArtNetEncoder.Compile(translation.Frame, universes, sequence, physical);
                errors.AddRange(output.Errors);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                errors.Add($"invalid_artnet_configuration:{exception.ParamName}");
            }
        }
        return new(translation, output, Array.Empty<SacnDmxPacket>(), errors);
    }

    public static CanonicalDmxOutputPlan CompileSacn(
        IEnumerable<PersonalityPatchMapping> mappings,
        CanonicalSessionState state,
        IEnumerable<int> universes,
        Guid cid,
        string sourceName,
        byte sequence = 0,
        byte priority = 100,
        bool previewData = false)
    {
        var translation = PersonalityPatchBridge.Translate(mappings, state);
        var errors = translation.Errors.ToList();
        var packets = new List<SacnDmxPacket>();
        if (errors.Count == 0)
        {
            foreach (var universe in universes.Distinct().OrderBy(value => value))
            {
                try
                {
                    packets.Add(SacnEncoder.Encode(translation.Frame, universe, cid, sourceName, sequence, priority, previewData));
                }
                catch (ArgumentOutOfRangeException exception)
                {
                    errors.Add($"invalid_sacn_configuration:{exception.ParamName}");
                }
                catch (ArgumentException exception)
                {
                    errors.Add($"invalid_sacn_configuration:{exception.Message}");
                }
            }
        }
        return new(translation, null, packets, errors);
    }
}
