/// <summary>
/// Logging seam for the sample's SDK API calls.
///
/// Every scenario and example script logs the SDK calls it makes through
/// <see cref="OctopusSampleLog.Current"/> only — never a `Assets/Debug/` type directly. That is
/// what keeps this sample independent of `Assets/Debug/`: the Debug console is denylisted from
/// the public mirror (see `.gitattributes`), so any script the mirror ships must still compile
/// and run with `Debug/` entirely absent. Remove `Debug/` and every call site here still
/// resolves — it just logs into <see cref="OctopusSampleLog.None"/> instead.
/// </summary>
public interface IOctopusSampleLog
{
    /// <summary>Records one API call the sample made into the Octopus SDK.</summary>
    /// <param name="method">The method name, e.g. "OctopusSDK.Open".</param>
    /// <param name="detail">Optional human-readable argument summary.</param>
    void LogApiCall(string method, string detail = null);
}

/// <summary>
/// Static access point for <see cref="IOctopusSampleLog"/>.
///
/// Defaults to <see cref="None"/> (a no-op) so every script outside `Assets/Debug/` behaves
/// identically whether or not that folder is present. The real recorder that lives under
/// `Assets/Debug/` installs itself into <see cref="Current"/> on its own, via
/// `[RuntimeInitializeOnLoadMethod]` — nothing outside `Debug/` ever needs to call into it, or
/// even name its type (this file stays clean of `Assets/Debug/` type names on purpose — see
/// `ci/mirror-export-guard/check-mirror-export.sh`'s backstop).
/// </summary>
public static class OctopusSampleLog
{
    /// <summary>The no-op implementation used until (or unless) a real log installs itself.</summary>
    public static readonly IOctopusSampleLog None = new NoopSampleLog();

    /// <summary>
    /// The active log. Read by every call site in the sample; written only by the real
    /// recorder's own installer under `Assets/Debug/`.
    /// </summary>
    public static IOctopusSampleLog Current { get; set; } = None;

    private sealed class NoopSampleLog : IOctopusSampleLog
    {
        public void LogApiCall(string method, string detail = null) { }
    }
}
