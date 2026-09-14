using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Xanax.Core;

public sealed record DmxFunction(
    string Name,
    string Attribute,
    double DmxFrom,
    double DmxTo,
    double PhysicalFrom,
    double PhysicalTo,
    string? ModeMaster = null);

public sealed record GdtfLoadResult(
    FixturePersonality Personality,
    IReadOnlyList<string> Warnings);

public static class GdtfProfileLoader
{
    public static GdtfLoadResult Load(string path, string? modeName = null)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("GDTF file was not found.", path);

        using var archive = ZipFile.OpenRead(path);
        var entry = archive.GetEntry("description.xml") ?? archive.GetEntry("Description.xml");
        if (entry is null)
            throw new InvalidDataException("GDTF archive does not contain description.xml.");

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return LoadDescriptionXml(reader, modeName);
    }

    public static GdtfLoadResult LoadDescriptionXml(TextReader reader, string? modeName = null)
    {
        var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidDataException("GDTF description has no root element.");
        var warnings = new List<string>();
        var fixture = Is(root, "FixtureType")
            ? root
            : root.Descendants().FirstOrDefault(e => Is(e, "FixtureType"));
        if (fixture is null)
            throw new InvalidDataException("GDTF description has no FixtureType.");
        var fixtureName = fixture.Attribute("Name")?.Value ?? "GDTF_FIXTURE";

        var modes = fixture.Descendants().Where(e => Is(e, "DMXMode")).ToArray();
        if (modes.Length == 0)
            throw new InvalidDataException("GDTF description has no DMXMode.");

        var mode = modeName is null
            ? modes[0]
            : modes.FirstOrDefault(e => string.Equals(e.Attribute("Name")?.Value, modeName, StringComparison.OrdinalIgnoreCase));
        if (mode is null)
            throw new InvalidDataException($"GDTF mode '{modeName}' was not found.");

        var resolvedModeName = mode.Attribute("Name")?.Value ?? "DMX_MODE";
        var channels = new List<DmxChannel>();
        foreach (var element in mode.Descendants().Where(e => Is(e, "DMXChannel")))
        {
            var offsets = ParseOffsets(element.Attribute("Offset")?.Value);
            if (offsets.Count == 0)
            {
                warnings.Add("A DMXChannel without Offset was ignored.");
                continue;
            }

            var parsedFunctions = element
                .Descendants()
                .Where(e => Is(e, "ChannelFunction"))
                .Select(e => ParseFunction(e, element))
                .ToArray();
            if (parsedFunctions.Length == 0)
            {
                warnings.Add($"DMXChannel at offset {offsets[0]} has no ChannelFunction and was ignored.");
                continue;
            }
            var maximumRaw = MaximumRaw(offsets.Count * 8);
            var functions = parsedFunctions
                .Select((function, index) => function with
                {
                    DmxTo = ResolveDmxTo(function, index, parsedFunctions, maximumRaw)
                })
                .ToArray();
            if (functions.Select(f => f.Attribute).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                warnings.Add($"DMXChannel at offset {offsets[0]} contains multiple attributes; the primary mapping is retained and alternate functions remain available.");

            var primary = functions[0];
            var channel = new DmxChannel(
                primary.Attribute,
                offsets[0],
                offsets.Count * 8,
                primary.PhysicalFrom,
                primary.PhysicalTo,
                Virtual: false,
                ModeMaster: primary.ModeMaster)
            {
                Functions = functions,
                DmxBreak = ParseInt(element.Attribute("DMXBreak")?.Value, 1)
            };
            channels.Add(channel);
        }

        if (channels.Count == 0)
            throw new InvalidDataException($"GDTF mode '{resolvedModeName}' has no usable DMX channels.");

        var personality = new FixturePersonality(fixtureName, resolvedModeName, channels);
        return new GdtfLoadResult(personality, warnings);
    }

    private static DmxFunction ParseFunction(XElement element, XElement dmxChannel)
    {
        var logical = element.Parent;
        var attribute = element.Attribute("Attribute")?.Value
            ?? logical?.Attribute("Attribute")?.Value
            ?? dmxChannel.Attribute("Attribute")?.Value
            ?? "Unknown";
        return new DmxFunction(
            element.Attribute("Name")?.Value ?? attribute,
            attribute,
            ParseDmxRaw(element.Attribute("DMXFrom")?.Value, 0),
            ParseDmxRaw(element.Attribute("DMXTo")?.Value, double.NaN),
            ParseDouble(element.Attribute("PhysicalFrom")?.Value, 0),
            ParseDouble(element.Attribute("PhysicalTo")?.Value, 1),
            element.Attribute("ModeMaster")?.Value);
    }

    private static IReadOnlyList<int> ParseOffsets(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<int>();
        return value
            .Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(v => ParseInt(v, 0))
            .Where(v => v > 0)
            .ToArray();
    }

    private static double ParseRational(string? value, double fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        var parts = value.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator) &&
            Math.Abs(denominator) > double.Epsilon)
            return numerator / denominator;
        return ParseDouble(value, fallback);
    }

    private static double ParseDmxRaw(string? value, double fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        var numerator = value.Split('/', StringSplitOptions.TrimEntries)[0];
        return double.TryParse(numerator, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static double ResolveDmxTo(DmxFunction function, int index, IReadOnlyList<DmxFunction> functions, double maximumRaw)
    {
        if (!double.IsNaN(function.DmxTo) && function.DmxTo >= function.DmxFrom)
            return function.DmxTo;
        var next = index + 1 < functions.Count ? functions[index + 1].DmxFrom - 1 : maximumRaw;
        return Math.Max(function.DmxFrom, next);
    }

    private static double MaximumRaw(int resolutionBits)
    {
        if (resolutionBits is < 1 or > 31)
            throw new InvalidDataException("GDTF channel resolution is outside the supported 1..31 bit range.");
        return (1L << resolutionBits) - 1L;
    }

    private static double ParseDouble(string? value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    private static bool Is(XElement element, string localName) =>
        string.Equals(element.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase);
}
