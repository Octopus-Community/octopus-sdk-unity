/// <summary>
/// One request for a native community screen, shared by the Community tab and the Scenarios tab's
/// "Open the community" tiles so both take the same path: the SDK is initialised first (the pilot
/// profile, as a scenario would), the call is logged, an exception becomes a message rather than a
/// crash, and the player's pause/resume tells when the tester is back from the native screen.
/// </summary>
public sealed class OctopusSampleNativeRequest
{
    /// <summary>What the caller shows when the native call itself threw.</summary>
    public const string FailedMessage = "Could not request the native screen. Check your configuration and try again.";

    /// <summary>A native screen was requested and the tester has not come back from it yet.</summary>
    public bool Requested { get; private set; }

    /// <summary>The player paused after the request: the native screen took over.</summary>
    public bool LeftPlayer { get; private set; }

    /// <summary>
    /// Initialises the SDK if needed, logs <paramref name="method"/>, then runs <paramref name="open"/>.
    /// Returns null once the request went out; otherwise the reason it did not — the SDK could not
    /// start (the reason it gave, <paramref name="notReady"/> true), or the call threw
    /// (<see cref="FailedMessage"/>).
    /// </summary>
    public string Request(string method, string detail, System.Action open, out bool notReady)
    {
        notReady = false;
        try
        {
            string reason;
            if (OctopusScenarioSdk.EnsurePilotInitialized(out reason) == null)
            {
                notReady = true;
                return string.IsNullOrEmpty(reason) ? FailedMessage : reason;
            }
            OctopusSampleLog.Current.LogApiCall(method, detail);
            Requested = true;
            LeftPlayer = false;
            open();
            return null;
        }
        catch (System.Exception exception)
        {
            OctopusSampleLog.Current.LogStateChange("Native screen request failed", exception.Message);
            Requested = false;
            return FailedMessage;
        }
    }

    /// <summary>
    /// Feeds the player's pause/resume. Returns true exactly once, on the resume that follows the
    /// pause the native screen caused: the tester is back.
    /// </summary>
    public bool OnPause(bool paused)
    {
        if (!Requested) return false;
        if (paused) { LeftPlayer = true; return false; }
        if (!LeftPlayer) return false;
        Requested = false;
        LeftPlayer = false;
        return true;
    }

    /// <summary>Restores the bookkeeping a rebuilt view captured from the one it replaces.</summary>
    public void Restore(bool requested, bool leftPlayer)
    {
        Requested = requested;
        LeftPlayer = leftPlayer;
    }
}
