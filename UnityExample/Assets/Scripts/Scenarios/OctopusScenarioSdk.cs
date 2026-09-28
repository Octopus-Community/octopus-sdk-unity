using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// The one place a scenario pilot initialises the SDK.
///
/// `OctopusSDK.Initialize` re-runs native initialization on every call, and the SDK it configures
/// is a process-wide singleton, so the "have we
/// initialised yet" flag is process-wide too. Two scenario screens visited in a row therefore
/// share one initialisation, and the second one's connection mode is ignored: that is stated
/// rather than hidden, and it is why <see cref="EnsureInitialized"/> reports the mode it actually
/// left the SDK in.
///
/// First initialization requires a user tap. The shell can replay that validated configuration
/// on later launches; mounting a scenario screen still never initializes anything.
/// </summary>
public static partial class OctopusScenarioSdk
{
    /// <summary>
    /// The one connection mode every pilot initialises with. `connection` needs SSO, and neither
    /// `customEvents` nor `locale` cares about the mode — so all three ask for SSO, and visiting
    /// the screens in any order leaves the SDK in a mode the connection presets can legally use.
    /// (Calling `ConnectUser` on an SDK initialised with Octopus auth is a native precondition
    /// failure on iOS, not an error the sample could report.)
    /// </summary>
    public const string PilotModeLabel = "SSO";

    public static ConnectionMode PilotMode() { return ConnectionMode.SSO(); }

    private const string StartupPreferenceKey = "OctopusSample.ValidatedStartupConfig";
    private static string _startupPreferenceKey = StartupPreferenceKey;
    private static bool _startupFailed;

    /// <summary>
    /// Replays a previously successful initialization; a fresh install stays cold.
    ///
    /// A replay that fails forgets the saved selection (#342): the next launch is then a cold one
    /// that asks for a profile, instead of failing on the same saved value on every launch until
    /// the app is reinstalled. Android's sample self-clears a failed restore the same way.
    /// </summary>
    public static void RestoreInitializedSdk()
    {
        if (OctopusSampleState.IsInitialized || _startupPreferenceKey == null) return;
        var wasForceLogin = OctopusSampleFeatureToggles.ForceLogin;
        try
        {
            // Zero is a fresh install; 1 and 2 select the bundled default/forced-login profile.
            // Store no API key or token in PlayerPrefs.
            var saved = PlayerPrefs.GetInt(_startupPreferenceKey, 0);
            if (saved == 0) return;
            if (saved != 1 && saved != 2)
            {
                ReportStartupFailure("Saved configuration is invalid. Choose a configuration to try again.");
                return;
            }
            // A replay of the choice the tester made on an earlier launch, not a flip: no
            // "feature toggled" line in the Debug console (#342).
            OctopusSampleFeatureToggles.Restore(saved == 2);
            string reason;
            if (EnsurePilotInitialized(out reason) == null)
            {
                OctopusSampleFeatureToggles.Restore(wasForceLogin);
                ReportStartupFailure(reason);
            }
        }
        catch (Exception exception)
        {
            // Exception messages may contain native configuration values. Keep credentials out
            // of the dashboard and logs while leaving the failure visible and retryable.
            if (OctopusSampleState.IsInitialized)
            {
                // The SDK is up: only a setup step after Initialize threw (the native theme, the
                // Unified Profile override). The saved profile did work, so forgetting it would
                // turn a cosmetic failure into a cold start on the next launch (#387).
                Debug.LogWarning("[Octopus SDK] Sample setup after initialization failed (" +
                                 exception.GetType().Name + "); the SDK is running and the saved " +
                                 "configuration is kept.");
                return;
            }
            OctopusSampleFeatureToggles.Restore(wasForceLogin);
            ReportStartupFailure(exception.GetType().Name + ". Check the configuration and try a scenario preset.");
        }
    }

    /// <summary>
    /// Why the last startup replay failed, or null. The Configuration screen that opens over Home
    /// after a failed replay shows it, so the reason is not hidden behind the screen asking for
    /// the retry.
    /// </summary>
    public static string StartupFailureReason { get; private set; }

    private static void ReportStartupFailure(string reason)
    {
        _startupFailed = true;
        StartupFailureReason = "Startup initialization failed: " + reason;
        ForgetStartupConfig();
        OctopusSampleState.ReportSession(OctopusSampleState.Session.StartupFailed, StartupFailureReason);
    }

    private static void ForgetStartupConfig()
    {
        try
        {
            PlayerPrefs.DeleteKey(_startupPreferenceKey);
            PlayerPrefs.Save();
        }
        catch (Exception)
        {
            Debug.LogWarning("[Octopus SDK] Sample startup configuration could not be cleared; " +
                             "the next launch may try it again.");
        }
    }

    private static bool RememberInitializedConfig()
    {
        if (_startupPreferenceKey == null) return true;
        try
        {
            PlayerPrefs.SetInt(_startupPreferenceKey, OctopusSampleFeatureToggles.ForceLogin ? 2 : 1);
            PlayerPrefs.Save();
            return true;
        }
        catch (Exception)
        {
            Debug.LogWarning("[Octopus SDK] Sample startup configuration could not be saved; " +
                             "the next launch may require a scenario tap.");
            return false;
        }
    }

    internal static OctopusExampleConfig.ExampleProfile EnsurePilotInitialized(out string reason)
    {
        return EnsureInitialized(PilotMode(), PilotModeLabel, out reason);
    }

    // Initialisation and mode live in OctopusSampleState, not here: the Home dashboard shows them,
    // and two copies of "have we initialised yet" is how a dashboard ends up disagreeing with the
    // screen that did the initialising.
    private static string _operationInFlight;
    private static System.DateTime _operationStartedAt;
    private static int _operationToken;

    /// <summary>
    /// Safety valve for the process-wide operation slot: a native callback that never fires would
    /// otherwise block every pilot in the sample until the app is restarted. After this delay the
    /// stale slot is released by the next caller.
    /// </summary>
    public static readonly System.TimeSpan OperationTimeout = System.TimeSpan.FromSeconds(60);
    /// <summary>Clock behind <see cref="OperationTimeout"/>; tests substitute a fixed one.</summary>
    public static System.Func<System.DateTime> UtcNow = () => System.DateTime.UtcNow;

    /// <summary>The operation currently holding the slot, or null once it has timed out.</summary>
    private static string OperationInFlight
    {
        get
        {
            if (_operationInFlight != null && UtcNow() - _operationStartedAt >= OperationTimeout)
                _operationInFlight = null;
            return _operationInFlight;
        }
    }

    /// <summary>
    /// Identifies the operation that last took the slot. Read it right after a successful
    /// <see cref="TryBeginOperation"/> and hand it back to <see cref="EndOperation(int)"/>: a
    /// callback of an operation that already timed out then leaves the slot of its successor alone.
    /// </summary>
    public static int OperationToken { get { return _operationToken; } }
    private static OctopusExampleConfig.ExampleProfile _activeProfile;
    private static string _runningServerHost;
    private static int _liveSelection;

    /// <summary>
    /// The configuration the running SDK was started or last switched onto through this class
    /// (1 Default, 2 Forced login), or 0 while it is not running. Tracked where the SDK actually
    /// changes, not read back from the Force login switch: the Lifecycle preset
    /// <c>switchCommunity (alt key)</c> changes community without touching it (#387).
    /// </summary>
    public static int LiveSelection { get { return OctopusSampleState.IsInitialized ? _liveSelection : 0; } }

    /// <summary>
    /// The backend host the running SDK was last initialised or switched onto through this class,
    /// or null while it is not running. Home reads it so a switch cannot leave the old host on
    /// screen.
    /// </summary>
    public static string RunningServerHost { get { return _runningServerHost; } }

    /// <summary>
    /// The host every pilot passes for <paramref name="profile"/>: its override, or the demo
    /// backend. Initialize and both SwitchCommunity paths go through it, so a switch never
    /// falls back to the native default (production) the way it did before #391.
    /// </summary>
    internal static string ServerHostFor(OctopusExampleConfig.ExampleProfile profile)
    {
        return OctopusExampleConfig.ResolveApiServerHost(profile == null ? null : profile.apiServerHost);
    }

    private static IOctopusScenarioSdk _sdk = new OctopusLiveScenarioSdk();

    /// <summary>
    /// The SDK the pilots call into — <see cref="OctopusLiveScenarioSdk"/> unless a test installed
    /// a recording one through <see cref="Use"/>.
    /// </summary>
    public static IOctopusScenarioSdk Current { get { return _sdk; } }

    /// <summary>
    /// Installs the SDK the pilots call into (null restores the live one) and forgets everything
    /// this class knows about the previous one: whether it was initialised, in which mode, and
    /// whether an operation was in flight. Those three are process-wide caches ABOUT an SDK, so
    /// keeping them across a swap would describe an SDK nobody is calling any more.
    ///
    /// Written for EditMode tests; the app never calls it. A fake SDK does not write application
    /// preferences unless the test supplies its own isolated <paramref name="startupPreferenceKey"/>.
    /// </summary>
    public static void Use(IOctopusScenarioSdk sdk, string startupPreferenceKey = null)
    {
        ResetCommunityScenarioState();
        ResetPilotObservations();
        _sdk = sdk ?? new OctopusLiveScenarioSdk();
        _startupPreferenceKey = sdk == null ? StartupPreferenceKey : startupPreferenceKey;
        _startupFailed = false;
        StartupFailureReason = null;
        _operationInFlight = null;
        UtcNow = () => System.DateTime.UtcNow;
        _activeProfile = null;
        _runningServerHost = null;
        _liveSelection = 0;
        _bridgeTokenProvider = new OctopusSampleTokenProvider(null);
        OctopusSampleState.Reset();
        OctopusSampleUnifiedProfile.Reset();
    }

    /// <summary>
    /// True when the SDK was initialised (through this class) with <see cref="PilotModeLabel"/>,
    /// i.e. when the connection calls are legal. False before any initialisation and after an
    /// initialisation with another mode.
    /// </summary>
    public static bool IsInPilotMode
    {
        get
        {
            return OctopusSampleState.IsInitialized &&
                   OctopusSampleState.ModeLabel == PilotModeLabel;
        }
    }

    /// <summary>
    /// The package keeps one static completion source per SDK call (`ConnectUser`,
    /// `DisconnectUser`): a second call while the first is in flight replaces it, and the native
    /// callback then completes the wrong task. This serialises those calls across every scenario
    /// screen — process-wide, like the completion sources it protects. Returns false, with the
    /// message to show, while another operation is still running.
    /// </summary>
    public static bool TryBeginOperation(string name, out string busyMessage)
    {
        var running = OperationInFlight;
        if (running != null)
        {
            busyMessage = "'" + running + "' is still running — wait for its result " +
                          "before starting '" + name + "'.";
            return false;
        }

        _operationInFlight = name;
        _operationStartedAt = UtcNow();
        _operationToken++;
        busyMessage = null;
        return true;
    }

    // Keep subsequent SSO presets on the profile selected by SwitchCommunity.
    internal static void CommunitySwitched(OctopusExampleConfig.ExampleProfile profile, int selection)
    {
        _activeProfile = profile;
        _runningServerHost = ServerHostFor(profile);
        _liveSelection = selection;
        _bridgeTokenProvider = new OctopusSampleTokenProvider(profile);
        OctopusSampleState.Reset();
        OctopusSampleState.ReportInitialized(PilotModeLabel);
        OctopusSampleNativeTheme.Apply(_sdk);
        // The debug override lives on the SDK instance the switch replaced.
        OctopusSampleUnifiedProfile.Reapply();
    }

    internal static void LifecycleStopped()
    {
        _activeProfile = null;
        _runningServerHost = null;
        _liveSelection = 0;
        _bridgeTokenProvider = new OctopusSampleTokenProvider(null);
        OctopusSampleState.Reset();
    }

    /// <summary>
    /// Releases the slot taken by <see cref="TryBeginOperation"/>, but only while the operation
    /// identified by <paramref name="token"/> still owns it. After <see cref="OperationTimeout"/>
    /// another pilot may have taken the slot; the late callback of the timed-out operation must
    /// not release that newer one. Safe to call twice.
    /// </summary>
    public static void EndOperation(int token)
    {
        if (token == _operationToken) _operationInFlight = null;
    }

    /// <summary>Unconditionally frees the slot. Written for EditMode test set-up; pilots use <see cref="EndOperation(int)"/>.</summary>
    public static void ResetOperationSlot()
    {
        _operationInFlight = null;
    }

    /// <summary>
    /// The profile the sample can actually initialise with, or null with the reason to show the
    /// user in <paramref name="reason"/>.
    ///
    /// Two states, not one: the asset can be absent (a fresh clone has none — it is gitignored),
    /// and it can be present with no API key in it, which is what a half-finished setup looks like.
    /// Passing a blank key to `Initialize` is worse than refusing: the SDK configures itself, every
    /// screen then reports "initialised", and the first real call fails somewhere far from here.
    ///
    /// Reading this initialises nothing, so a screen can ask "would a door work?" without opening
    /// one — which is how the Community tab draws its blocking band without calling the SDK.
    /// </summary>
    internal static OctopusExampleConfig.ExampleProfile UsableProfile(out string reason)
    {
        var profile = _activeProfile ?? _sdk.Profile;
        if (profile == null)
        {
            reason = "OctopusExampleConfig asset missing — create it via " +
                     "Assets > Create > Octopus Example Config, then fill in the API key.";
            return null;
        }

        if (string.IsNullOrEmpty(profile.apiKey) || profile.apiKey.Trim().Length == 0)
        {
            reason = "OctopusExampleConfig has no API key — fill one in on the asset, then build " +
                     "again. Initialising with a blank key would look like it worked.";
            return null;
        }

        reason = null;
        return profile;
    }

    /// <summary>
    /// Initialises the SDK once per process with <paramref name="mode"/>, and returns the profile
    /// the sample is configured with — or null when <see cref="UsableProfile"/> refuses, in which
    /// case <paramref name="reportedMode"/> carries the reason to show the user.
    /// </summary>
    internal static OctopusExampleConfig.ExampleProfile EnsureInitialized(
        ConnectionMode mode, string modeLabel, out string reportedMode)
    {
        var running = OperationInFlight;
        if (running != null && running.StartsWith("Lifecycle."))
        {
            reportedMode = running + " is still running — wait before another SDK call.";
            return null;
        }
        string refusal;
        var profile = UsableProfile(out refusal);
        if (profile == null)
        {
            reportedMode = refusal;
            return null;
        }

        // Resolve the config on the player loop; the signing callback must never load Resources.
        _bridgeTokenProvider = new OctopusSampleTokenProvider(profile);
        if (OctopusSampleState.IsInitialized)
        {
            var already = OctopusSampleState.ModeLabel;
            reportedMode = already == modeLabel
                ? modeLabel
                : already + " (already initialised by an earlier scenario; '" + modeLabel +
                  "' was ignored — apply a profile in Configuration to change it)";
            return profile;
        }

        // Before Initialize, not after: the SDK can publish a notification count or a community
        // access change as soon as it is initialised, and a listener installed afterwards would
        // miss it with nothing to ask for it again (Home is read-only).
        EnsurePilotObservations();
        OctopusSampleState.EnsureObserving();
        // Same reason, for the client-object bridge: the member who taps "Take the challenge" on a
        // shared Reef Run post is typically the one who has never opened the game, so the route
        // back into it cannot be armed by the game screen. This seam is the earliest point that is
        // shared by the initial user tap and its replay on startup (#236, #332).
        OctopusReefRunView.EnsureRouted();
        // Same reason for member-avatar taps: both natives read the interception switch when the
        // community opens, and a Unified Profile tap must reach the host from the first one.
        OctopusSampleUnifiedProfile.EnsureRouted();

        // Always an explicit host: the native SDKs default to production, where the demo key
        // resolves to a community without the demo's bridge-post setup.
        var host = ServerHostFor(profile);
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.Initialize",
            "mode=" + modeLabel + ", host=" + host);
        _sdk.Initialize(profile.apiKey, mode, host);
        _runningServerHost = host;
        _liveSelection = OctopusSampleFeatureToggles.ForceLogin ? 2 : 1;
        OctopusSampleState.ReportInitialized(modeLabel);
        // After Initialize: the bridge needs the channel the initialisation created, and the
        // native SDK keeps the theme for the life of the process (#134).
        OctopusSampleNativeTheme.Apply(_sdk);
        // After Initialize: a forced exposeClientUserId is an override of the running SDK.
        OctopusSampleUnifiedProfile.Reapply();
        RememberInitializedConfig();
        if (_startupFailed)
        {
            _startupFailed = false;
            StartupFailureReason = null;
            OctopusSampleState.ReportSession(OctopusSampleState.Session.None, null);
        }
        reportedMode = modeLabel;
        return profile;
    }
    // Volatile publishes the provider and its initialized secret to off-loop signing callbacks.
    private static volatile OctopusSampleTokenProvider _bridgeTokenProvider = new OctopusSampleTokenProvider(null);
    private static Func<string, Task<string>> _hostBridgeShareSigner;

    // Demo signing uses the same local secret as SSO. Production hosts supply backend I/O.
    // The SDK invokes this provider off the player loop; null restores the demo default.
    public static Func<string, Task<string>> BridgeShareSigner
    {
        get { return _hostBridgeShareSigner ?? SignDemoBridgeShare; }
        set { _hostBridgeShareSigner = value; }
    }

    // The getter above never returns null, by design: signing must work on the demo build
    // without a host. That also makes "a host installed a backend signer" and "we are on the
    // demo fallback" indistinguishable from outside — the sample cannot show which signer is
    // live, and a host integrating this sample as a reference has nothing to assert on. This
    // reads the raw field, so the fallback semantics are untouched (#252).
    public static bool HasHostBridgeShareSigner
    {
        get { return _hostBridgeShareSigner != null; }
    }

    private static Task<string> SignDemoBridgeShare(string fingerprint)
    {
        try { return Task.FromResult(_bridgeTokenProvider.GetBridgeSignature(fingerprint)); }
        catch (InvalidOperationException error)
        {
            OctopusSampleLog.Current.LogStateChange("Bridge signing refused", error.Message);
            return Task.FromException<string>(error);
        }
        catch (Exception error) { return Task.FromException<string>(error); }
    }

    public const string BridgeSigningNotice =
        "Bridge posts use the configured demo SSO secret or a host backend signer. " +
        "Without either, signing is refused; a static authToken is not enough.";

    internal static string ContentOptionsSummary = "none (backend default)";

    public static string LatestBridgeObjectId { get; private set; }
    public static string LatestBridgePostId { get; private set; }
    public static OctopusPost LatestBridgePost { get; private set; }
    public static string LastNavigateObjectId { get; private set; }
    public static int NavigateFireCount { get; private set; }
    public static event Action<bool> BridgeStateChanged;
    private static bool _bridgeObserving;

    // Process-scoped so leaving the pilot for Community does not lose CTA callbacks or counts.
    internal static void EnsureBridgeObserving()
    {
        if (_bridgeObserving) return;
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.OnClientObjectRelatedPostChanged +=", "");
        _sdk.ClientObjectRelatedPostChanged += BridgePostChanged;
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.OnNavigateToClientObject +=", "");
        _sdk.NavigateToClientObject += BridgeNavigated;
        _bridgeObserving = true;
    }

    internal static void SetBridgePost(string objectId, string postId)
    {
        if (LatestBridgeObjectId != objectId)
        {
            if (LatestBridgeObjectId != null)
            {
                OctopusSampleLog.Current.LogApiCall("OctopusSDK.StopObservingClientObjectRelatedPost", "objectId=" + LatestBridgeObjectId);
                _sdk.StopObservingClientObjectRelatedPost(LatestBridgeObjectId);
            }
            LatestBridgePost = null;
            LatestBridgeObjectId = objectId;
            LatestBridgePostId = postId;
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.StartObservingClientObjectRelatedPost", "objectId=" + objectId);
            _sdk.StartObservingClientObjectRelatedPost(objectId);
        }
        else LatestBridgePostId = postId;
        BridgeStateChanged?.Invoke(true);
    }

    private static void BridgePostChanged(string objectId, OctopusPost post)
    {
        if (objectId != LatestBridgeObjectId) return;
        LatestBridgePost = post;
        LatestBridgePostId = post == null ? null : post.Id;
        BridgeStateChanged?.Invoke(true);
    }

    private static void BridgeNavigated(string objectId)
    {
        LastNavigateObjectId = objectId;
        NavigateFireCount++;
        BridgeStateChanged?.Invoke(true);
    }

    private static void ResetCommunityScenarioState()
    {
        if (_bridgeObserving)
        {
            if (LatestBridgeObjectId != null)
            {
                OctopusSampleLog.Current.LogApiCall("OctopusSDK.StopObservingClientObjectRelatedPost", "objectId=" + LatestBridgeObjectId);
                _sdk.StopObservingClientObjectRelatedPost(LatestBridgeObjectId);
            }
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.OnClientObjectRelatedPostChanged -=", "");
            _sdk.ClientObjectRelatedPostChanged -= BridgePostChanged;
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.OnNavigateToClientObject -=", "");
            _sdk.NavigateToClientObject -= BridgeNavigated;
        }
        ContentOptionsSummary = "none (backend default)";
        _bridgeObserving = false;
        LatestBridgeObjectId = null;
        LatestBridgePostId = null;
        LatestBridgePost = null;
        LastNavigateObjectId = null;
        NavigateFireCount = 0;
        // BridgeStateChanged stays subscribed: its handlers are weak and self-detach, and clearing
        // it would silently orphan a BridgeScenario that believes it is still observing. The
        // host-configured BridgeShareSigner is configuration, not per-SDK state, so it survives too.
    }

}
