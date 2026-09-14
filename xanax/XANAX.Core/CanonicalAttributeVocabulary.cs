namespace Xanax.Core;

/// <summary>
/// Host-independent attribute vocabulary. Exact names still win; aliases are
/// only used when a personality exposes a different label for the same intent.
/// </summary>
public static class CanonicalAttributeVocabulary
{
    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["intensity"] = "intensity",
            ["dimmer"] = "intensity",
            ["brightness"] = "intensity",
            ["level"] = "intensity",
            ["pan"] = "pan",
            ["panfine"] = "pan_fine",
            ["tilt"] = "tilt",
            ["tiltfine"] = "tilt_fine",
            ["red"] = "red",
            ["green"] = "green",
            ["blue"] = "blue",
            ["cyan"] = "cyan",
            ["magenta"] = "magenta",
            ["yellow"] = "yellow",
            ["colorwheel"] = "color_wheel",
            ["colourwheel"] = "color_wheel",
            ["gobo"] = "gobo",
            ["gobo1"] = "gobo_1",
            ["gobo2"] = "gobo_2",
            ["zoom"] = "zoom",
            ["focus"] = "focus",
            ["shutter"] = "shutter"
        };

    public static string Canonicalize(string attribute)
    {
        if (string.IsNullOrWhiteSpace(attribute))
            return string.Empty;
        var compact = new string(attribute
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToLowerInvariant();
        return Aliases.TryGetValue(compact, out var canonical) ? canonical : compact;
    }

    public static string? Resolve(
        string canonicalAttribute,
        IEnumerable<string> targetAttributes,
        out bool renamed)
    {
        renamed = false;
        var exact = targetAttributes.FirstOrDefault(attribute =>
            string.Equals(attribute, canonicalAttribute, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        var canonical = Canonicalize(canonicalAttribute);
        var resolved = targetAttributes.FirstOrDefault(attribute => Canonicalize(attribute) == canonical);
        renamed = resolved is not null;
        return resolved;
    }
}
