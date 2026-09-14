using System.Text.Json;

namespace Xanax.Core;

public enum MissionRelation
{
    Common,
    Reordered,
    Renamed,
    Split,
    Merged,
    Composed,
    ContextShift,
    AOnly,
    BOnly,
    Lossy,
    Unknown
}

public sealed record MissionRoute(
    string Host,
    IReadOnlyList<string> Objects,
    IReadOnlyList<string> Surface,
    IReadOnlyList<string> SemanticState,
    IReadOnlyDictionary<string, string> NativeRoutes);

public sealed record MissionDefinition(
    string Id,
    string CanonicalIntent,
    string MappingStatus,
    MissionRoute? SourceRoute,
    MissionRoute? TargetRoute,
    IReadOnlyDictionary<string, string> NativeRoutes,
    IReadOnlyList<string> LossOrRisk,
    IReadOnlyList<string> Evidence);

public sealed record CanonicalMissionOperation(
    string MissionId,
    string CanonicalIntent,
    IReadOnlyDictionary<string, string> Arguments);

public sealed record MissionPlan(
    CanonicalMissionOperation Operation,
    MissionDefinition Definition,
    MissionRelation Relation,
    MissionRoute Source,
    MissionRoute Target,
    IReadOnlyList<string> Preconditions,
    IReadOnlyList<string> TargetSteps,
    IReadOnlyList<string> LossOrRisk);

public sealed class MissionCatalog
{
    private readonly IReadOnlyDictionary<string, MissionDefinition> definitions;

    private MissionCatalog(IEnumerable<MissionDefinition> definitions)
    {
        this.definitions = definitions.ToDictionary(definition => definition.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<MissionDefinition> Definitions => definitions.Values.ToArray();

    public IReadOnlyList<MissionCase> CreateDocumentedCases(
        string firstHost = "titan",
        string secondHost = "grandma3")
    {
        var cases = new List<MissionCase>();
        foreach (var definition in definitions.Values.OrderBy(value => value.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (definition.SourceRoute is null || definition.TargetRoute is null)
                continue;
            if (!HasHost(definition, firstHost) || !HasHost(definition, secondHost))
                continue;
            cases.Add(MissionCaseFactory.FromDocumentedPlan(Plan(definition.Id, firstHost, secondHost)));
            cases.Add(MissionCaseFactory.FromDocumentedPlan(Plan(definition.Id, secondHost, firstHost)));
        }
        return cases;
    }

    public static MissionCatalog Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("XANAX crosswalk was not found.", path);

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("operations", out var operations) || operations.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The crosswalk has no operations array.");

        var parsed = operations.EnumerateArray().Select(ParseDefinition).ToArray();
        if (parsed.Length == 0)
            throw new InvalidDataException("The crosswalk contains no mission definitions.");
        return new MissionCatalog(parsed);
    }

    public MissionPlan Plan(
        string missionId,
        string sourceHost,
        string targetHost,
        IReadOnlyDictionary<string, string>? arguments = null)
    {
        if (!definitions.TryGetValue(missionId, out var definition))
            throw new KeyNotFoundException($"Mission '{missionId}' is not present in the crosswalk.");

        var source = ResolveRoute(definition, sourceHost, "source");
        var target = ResolveRoute(definition, targetHost, "target");
        var operation = new CanonicalMissionOperation(definition.Id, definition.CanonicalIntent, arguments ?? new Dictionary<string, string>());
        var targetSteps = definition.NativeRoutes.TryGetValue(targetHost, out var nativeRoute)
            ? new[] { nativeRoute }
            : target.Surface;
        return new(
            operation,
            definition,
            Classify(definition.MappingStatus),
            source,
            target,
            source.SemanticState,
            targetSteps,
            definition.LossOrRisk);
    }

    private static MissionRoute ResolveRoute(MissionDefinition definition, string host, string side)
    {
        var route = string.Equals(definition.SourceRoute?.Host, host, StringComparison.OrdinalIgnoreCase)
            ? definition.SourceRoute
            : string.Equals(definition.TargetRoute?.Host, host, StringComparison.OrdinalIgnoreCase)
                ? definition.TargetRoute
                : null;
        return route ?? throw new InvalidOperationException($"Mission '{definition.Id}' has no {side} route for host '{host}'.");
    }

    private static bool HasHost(MissionDefinition definition, string host) =>
        string.Equals(definition.SourceRoute?.Host, host, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(definition.TargetRoute?.Host, host, StringComparison.OrdinalIgnoreCase);

    private static MissionDefinition ParseDefinition(JsonElement element)
    {
        var id = Required(element, "id");
        var canonicalIntent = Required(element, "canonical_intent");
        var mappingStatus = Optional(element, "mapping_status") ?? "unknown";
        var routes = new[]
        {
            ParseRoute(element, "titan_route", "titan"),
            ParseRoute(element, "grandma3_route", "grandma3")
        }.Where(route => route is not null).Cast<MissionRoute>().ToArray();

        return new(
            id,
            canonicalIntent,
            mappingStatus,
            routes.FirstOrDefault(route => route.Host == "titan"),
            routes.FirstOrDefault(route => route.Host == "grandma3"),
            StringMap(element, "native_route"),
            Strings(element, "loss_or_risk"),
            Strings(element, "evidence"));
    }

    private static MissionRoute? ParseRoute(JsonElement operation, string property, string host)
    {
        if (!operation.TryGetProperty(property, out var route) || route.ValueKind != JsonValueKind.Object)
            return null;
        return new(
            host,
            Strings(route, "objects"),
            Strings(route, "surface"),
            Strings(route, "semantic_state"),
            StringMap(route, "native_route"));
    }

    private static MissionRelation Classify(string mappingStatus)
    {
        var status = mappingStatus.ToLowerInvariant();
        if (status.Contains("not_direct") || status.Contains("structural") || status.Contains("separate_subsystem")) return MissionRelation.Composed;
        if (status.Contains("partial") || status.Contains("high_risk") || status.Contains("safety_critical")) return MissionRelation.Lossy;
        if (status.Contains("contextual")) return MissionRelation.ContextShift;
        if (status.Contains("near_compatible")) return MissionRelation.Common;
        if (status.Contains("transport_separate")) return MissionRelation.Renamed;
        return MissionRelation.Unknown;
    }

    private static string Required(JsonElement element, string name) =>
        Optional(element, name) ?? throw new InvalidDataException($"Crosswalk operation is missing '{name}'.");

    private static string? Optional(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static IReadOnlyList<string> Strings(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return Array.Empty<string>();
        if (value.ValueKind == JsonValueKind.String)
            return new[] { value.GetString() ?? string.Empty };
        if (value.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();
        return value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? string.Empty).ToArray();
    }

    private static IReadOnlyDictionary<string, string> StringMap(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return value.EnumerateObject()
            .Where(property => property.Value.ValueKind == JsonValueKind.String)
            .ToDictionary(property => property.Name, property => property.Value.GetString() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }
}
