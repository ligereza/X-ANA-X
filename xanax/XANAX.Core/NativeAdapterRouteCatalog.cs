namespace Xanax.Core;

/// <summary>
/// Target-specific route defaults live at the adapter boundary. Canonical
/// composition never needs to know whether the primitive is sent through
/// Titan HTTP, grandMA3 OSC, or another future adapter.
/// </summary>
public static class NativeAdapterRouteCatalog
{
    public static bool TryResolve(
        string host,
        string canonicalIntent,
        out string route)
    {
        route = string.Empty;
        if (!string.Equals(canonicalIntent, "select_fixture_set", StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.Equals(host, "titan", StringComparison.OrdinalIgnoreCase))
        {
            route = "Titan Web API Programmer.OnOff.Selection.SetNewHandleSelection using user-number identifiers.";
            return true;
        }
        if (string.Equals(host, "grandma3", StringComparison.OrdinalIgnoreCase))
        {
            route = "Official OSC command route: /cmd Fixture selection; Receive Command must be enabled.";
            return true;
        }
        return false;
    }
}
