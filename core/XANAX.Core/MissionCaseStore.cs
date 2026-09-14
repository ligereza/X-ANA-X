using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xanax.Core;

public sealed partial record MissionCase(
    string Id,
    string MissionId,
    string CanonicalIntent,
    string SourceHost,
    string TargetHost,
    string Relation,
    IReadOnlyDictionary<string, string> Context,
    IReadOnlyDictionary<string, string> Signature,
    IReadOnlyList<string> SourceGesture,
    IReadOnlyList<string> TargetGesture,
    IReadOnlyList<string> Loss,
    string Reversibility,
    string EvidenceLevel,
    IReadOnlyList<string> Evidence,
    DateTimeOffset CapturedAt,
    string ModelVersion = "learning-rel-v1");

public static class MissionCaseSignature
{
    public static MissionCase AttachTrajectory(
        MissionCase missionCase,
        LiveShowSignature signature)
    {
        signature.Validate();
        var numeric = signature.Axes
            .Select((axis, index) => new KeyValuePair<string, double>(axis, signature.Values[index]))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var textual = missionCase.Signature
            .Concat(signature.Axes.Select((axis, index) => new KeyValuePair<string, string>(
                $"trajectory:{axis}",
                signature.Values[index].ToString("R", System.Globalization.CultureInfo.InvariantCulture))))
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);
        return missionCase with
        {
            Signature = textual,
            NumericSignature = numeric,
            ModelVersion = "learning-rel-v2"
        };
    }
}

public sealed partial record MissionCase
{
    public IReadOnlyDictionary<string, double> NumericSignature { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
}

public sealed record MissionCaseMatch(
    MissionCase Case,
    double Score,
    IReadOnlyList<string> SharedAxes,
    IReadOnlyList<string> ConflictingAxes);

public static class MissionCaseFactory
{
    public static MissionCase FromDocumentedPlan(MissionPlan plan)
    {
        var intent = plan.Operation.CanonicalIntent;
        var signature = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["causality"] = "operator",
            ["feedback"] = "state_change",
            ["mission"] = intent,
            ["persistence"] = intent switch
            {
                "trigger_or_adjust_live_playback" => "held_or_latched",
                "stop_or_release_active_output" => "momentary",
                "store_or_edit_time_ordered_look" => "stored",
                _ => "held"
            },
            ["spatial_topology"] = intent switch
            {
                "transform_ordered_fixture_selection" => "ordered_or_grid",
                "select_fixture_set" => "ordered",
                _ => "none"
            },
            ["memory_behavior"] = intent switch
            {
                "recall_reusable_attribute_values" => "reference_or_recipe",
                "store_or_edit_time_ordered_look" => "snapshot_or_sequence",
                _ => "none"
            }
        };
        return new(
            $"documented:{plan.Operation.MissionId}:{plan.Source.Host}:{plan.Target.Host}",
            plan.Operation.MissionId,
            intent,
            plan.Source.Host,
            plan.Target.Host,
            plan.Relation.ToString(),
            plan.Source.SemanticState
                .Select(value => new KeyValuePair<string, string>(value, "required"))
                .Concat(plan.Target.SemanticState.Select(value => new KeyValuePair<string, string>($"target:{value}", "required")))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            signature,
            plan.Source.Surface,
            plan.TargetSteps,
            plan.LossOrRisk,
            plan.LossOrRisk.Count == 0 ? "unknown" : "partial_or_requires_review",
            "documented",
            plan.Definition.Evidence,
            DateTimeOffset.UtcNow);
    }
}

public static class MissionCaseStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void Append(string path, MissionCase missionCase)
    {
        ArgumentNullException.ThrowIfNull(missionCase);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.AppendAllText(path, JsonSerializer.Serialize(missionCase, JsonOptions) + Environment.NewLine);
    }

    public static IReadOnlyList<MissionCase> Read(string path)
    {
        if (!File.Exists(path))
            return Array.Empty<MissionCase>();
        var cases = new List<MissionCase>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var missionCase = JsonSerializer.Deserialize<MissionCase>(line, JsonOptions);
            if (missionCase is not null)
                cases.Add(missionCase);
        }
        return cases;
    }
}

/// <summary>
/// Deterministic retrieval for verified cases. It is the seed layer before any
/// learned embedding is introduced; a model may consume the same case vectors.
/// </summary>
public static class MissionCaseSimilarity
{
    public static IReadOnlyList<MissionCaseMatch> Rank(
        MissionCase query,
        IEnumerable<MissionCase> cases,
        int limit = 8)
    {
        if (limit < 1)
            return Array.Empty<MissionCaseMatch>();

        return cases
            .Where(candidate => !string.Equals(candidate.Id, query.Id, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => Score(query, candidate))
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Case.Id, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    private static MissionCaseMatch Score(MissionCase query, MissionCase candidate)
    {
        var shared = new List<string>();
        var conflicts = new List<string>();
        var score = 0d;
        if (string.Equals(query.CanonicalIntent, candidate.CanonicalIntent, StringComparison.OrdinalIgnoreCase)) score += 4;
        else conflicts.Add("canonical_intent");
        if (string.Equals(query.Relation, candidate.Relation, StringComparison.OrdinalIgnoreCase)) score += 2;
        else conflicts.Add("relation");
        if (string.Equals(query.SourceHost, candidate.SourceHost, StringComparison.OrdinalIgnoreCase)) score += 1;
        if (string.Equals(query.TargetHost, candidate.TargetHost, StringComparison.OrdinalIgnoreCase)) score += 1;

        foreach (var axis in query.Signature.Keys.Union(candidate.Signature.Keys, StringComparer.OrdinalIgnoreCase))
        {
            query.Signature.TryGetValue(axis, out var queryValue);
            candidate.Signature.TryGetValue(axis, out var candidateValue);
            if (string.IsNullOrWhiteSpace(queryValue) || string.IsNullOrWhiteSpace(candidateValue))
                continue;
            if (string.Equals(queryValue, candidateValue, StringComparison.OrdinalIgnoreCase))
            {
                shared.Add(axis);
                score += 1;
            }
            else
            {
                conflicts.Add(axis);
                score -= 0.5;
            }
        }

        foreach (var context in query.Context.Keys.Intersect(candidate.Context.Keys, StringComparer.OrdinalIgnoreCase))
            if (string.Equals(query.Context[context], candidate.Context[context], StringComparison.OrdinalIgnoreCase))
                score += 0.5;

        foreach (var axis in query.NumericSignature.Keys.Intersect(candidate.NumericSignature.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var distance = Math.Abs(query.NumericSignature[axis] - candidate.NumericSignature[axis]);
            if (distance <= 0.1)
            {
                shared.Add($"trajectory:{axis}");
                score += 1;
            }
            else
            {
                conflicts.Add($"trajectory:{axis}");
                score -= Math.Min(1, distance);
            }
        }

        return new(candidate, score, shared, conflicts);
    }
}
