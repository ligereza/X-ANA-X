namespace Xanax.Core;

public static class MissionEvidenceIdentity
{
    public static string For(MissionCase missionCase)
    {
        var evidence = missionCase.Evidence
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return evidence.Length == 0
            ? "no_evidence"
            : string.Join("|", evidence).ToLowerInvariant();
    }
}
