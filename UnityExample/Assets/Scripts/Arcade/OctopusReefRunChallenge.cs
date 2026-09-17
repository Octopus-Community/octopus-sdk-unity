using System;

/// <summary>
/// The return leg of the bridge: a member taps the CTA on a shared Reef Run post inside the SDK's
/// community, the SDK raises `OctopusSDK.OnNavigateToClientObject` with the object id the host
/// gave it, and the host — this sample — reopens the game on that run's score.
///
/// The observation is deliberately **process-scoped and started by a tap**, never by building a
/// screen. Two constraints meet here. The community is a native screen, so the callback arrives
/// while the game screen is closed and possibly while another tab is showing: a subscription owned
/// by the game object would already be gone. And SDK_STANDARDS §5.1/§5.2 make Home and the shell
/// read-only — a sample that subscribed at start-up would reach the SDK without anyone asking it
/// to. Opening the game is that ask, and it is also the only way a share — and therefore a post to
/// come back from — can exist at all.
///
/// It routes through <see cref="OctopusScenarioSdk"/> rather than `OctopusSDK` directly, because
/// that is the sample's single seam onto the SDK: the `bridge` scenario registers the same two
/// callbacks, the seam registers them once, and an EditMode test can drive the whole path.
/// </summary>
public static class OctopusReefRunChallenge
{
    /// <summary>Raised with the run id and the score to beat, on the Unity thread.</summary>
    public static event Action<string, int> Received;

    /// <summary>The run id of the pending challenge, or null.</summary>
    public static string RunId { get; private set; }

    /// <summary>The score to beat, or null when no Reef Run post has been opened.</summary>
    public static int? Target { get; private set; }

    private static bool _observing;
    private static int _seenFires;

    /// <summary>
    /// Starts observing, once per process. Safe to call on every tap; the seam it delegates to is
    /// idempotent too.
    /// </summary>
    public static void Observe()
    {
        if (_observing) return;
        _observing = true;
        _seenFires = OctopusScenarioSdk.NavigateFireCount;
        OctopusScenarioSdk.EnsureBridgeObserving();
        OctopusScenarioSdk.BridgeStateChanged += OnBridgeState;
    }

    /// <summary>Consumes the pending challenge — the run it describes has been started.</summary>
    public static void Clear()
    {
        RunId = null;
        Target = null;
    }

    /// <summary>Forgets the subscription and the pending challenge. Written for EditMode tests.</summary>
    public static void Reset()
    {
        if (_observing) OctopusScenarioSdk.BridgeStateChanged -= OnBridgeState;
        _observing = false;
        _seenFires = 0;
        Clear();
    }

    /// <summary>
    /// Handles one client-object id. Ids this game did not mint — the `bridge` scenario's recipes,
    /// anything another integration put in the community — leave the pending challenge untouched.
    /// </summary>
    internal static bool Handle(string objectId)
    {
        string runId;
        int score;
        if (!OctopusReefRunShare.TryParse(objectId, out runId, out score)) return false;
        RunId = runId;
        Target = score;
        var handler = Received;
        if (handler != null) handler(runId, score);
        return true;
    }

    private static void OnBridgeState(bool unused)
    {
        // The seam raises this for post snapshots too; only a new navigate fire is a CTA tap.
        var fires = OctopusScenarioSdk.NavigateFireCount;
        if (fires == _seenFires) return;
        _seenFires = fires;
        Handle(OctopusScenarioSdk.LastNavigateObjectId);
    }
}
