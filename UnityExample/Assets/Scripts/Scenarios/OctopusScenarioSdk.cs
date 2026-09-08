/// <summary>
/// The one place a scenario pilot initialises the SDK.
///
/// `OctopusSDK.Initialize` is not idempotent — it creates a fresh `OctopusChannel` GameObject on
/// every call — and the SDK it configures is a process-wide singleton, so the "have we
/// initialised yet" flag is process-wide too. Two scenario screens visited in a row therefore
/// share one initialisation, and the second one's connection mode is ignored: that is stated
/// rather than hidden, and it is why <see cref="EnsureInitialized"/> reports the mode it actually
/// left the SDK in.
///
/// Nothing here runs on entry to a screen. Every caller is inside an
/// <see cref="OctopusScenarioPreset.Run"/>, i.e. behind a user tap (SDK_STANDARDS §5.2).
/// </summary>
public static class OctopusScenarioSdk
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

    private static bool _initialized;
    private static string _mode;
    private static string _operationInFlight;

    /// <summary>
    /// True when the SDK was initialised (through this class) with <see cref="PilotModeLabel"/>,
    /// i.e. when the connection calls are legal. False before any initialisation and after an
    /// initialisation with another mode.
    /// </summary>
    public static bool IsInPilotMode { get { return _initialized && _mode == PilotModeLabel; } }

    /// <summary>
    /// The package keeps one static completion source per SDK call (`ConnectUser`,
    /// `DisconnectUser`): a second call while the first is in flight replaces it, and the native
    /// callback then completes the wrong task. This serialises those calls across every scenario
    /// screen — process-wide, like the completion sources it protects. Returns false, with the
    /// message to show, while another operation is still running.
    /// </summary>
    public static bool TryBeginOperation(string name, out string busyMessage)
    {
        if (_operationInFlight != null)
        {
            busyMessage = "'" + _operationInFlight + "' is still running — wait for its result " +
                          "before starting '" + name + "'.";
            return false;
        }

        _operationInFlight = name;
        busyMessage = null;
        return true;
    }

    /// <summary>Releases the slot taken by <see cref="TryBeginOperation"/>. Safe to call twice.</summary>
    public static void EndOperation()
    {
        _operationInFlight = null;
    }

    /// <summary>
    /// Initialises the SDK once per process with <paramref name="mode"/>, and returns the profile
    /// the sample is configured with — or null when the gitignored config asset is missing, in
    /// which case <paramref name="reportedMode"/> carries the reason to show the user.
    /// </summary>
    internal static OctopusExampleConfig.ExampleProfile EnsureInitialized(
        ConnectionMode mode, string modeLabel, out string reportedMode)
    {
        var config = OctopusExampleConfig.Instance;
        if (config == null)
        {
            reportedMode = "OctopusExampleConfig asset missing — create it via " +
                           "Assets > Create > Octopus Example Config, then fill in the API key.";
            return null;
        }

        var profile = config.Default;
        if (_initialized)
        {
            reportedMode = _mode == modeLabel
                ? modeLabel
                : _mode + " (already initialised by an earlier scenario; '" + modeLabel +
                  "' was ignored — restart the app to change it)";
            return profile;
        }

        OctopusSampleLog.Current.LogApiCall("OctopusSDK.Initialize", "mode=" + modeLabel);
        OctopusSDK.Initialize(profile.apiKey, mode);
        _initialized = true;
        _mode = modeLabel;
        reportedMode = modeLabel;
        return profile;
    }
}
