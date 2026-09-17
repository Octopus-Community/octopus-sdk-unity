/// <summary>
/// A boolean the tester flips from the header of the Scenarios section it belongs to — and from
/// nowhere else.
///
/// The shared design contract states the rule twice, because it is the one that keeps the sample
/// readable: *"a feature toggle lives in the section header — and nowhere else"*, and *"a toggle
/// without an observable effect is a bug, not a setting"*. Android encodes the same two in
/// `FeatureToggle.kt`, and the labels and effect sentences here are that file's, verbatim: three
/// samples describing one switch in three wordings is how a tester ends up not trusting any of
/// them.
///
/// **What flipping it does here, exactly.** `Force login` is a
/// host-integration decision rather than a live SDK switch — as it is on Android, where the KDoc
/// says the same: *"flipping one rewrites the persisted sample config, which is what a host app
/// would ship"*. `forceLoginOnStrongActions` is not a parameter of `ConnectionMode.SSO` on any
/// platform; it is a field of the **server-driven** community configuration. So neither sample
/// forwards it: Android's switch picks a different named API key — a different demo community,
/// configured that way on the backend — and this one picks between
/// <see cref="OctopusExampleConfig.Default"/> and <see cref="OctopusExampleConfig.ForcedLogin"/>,
/// which is the same gesture with the same two keys. Issue #110 asked for a package API to carry
/// the flag; there is no native parameter to carry it to.
///
/// `Push registration` decides whether new device tokens are forwarded to Octopus. It defaults
/// on, as on Android, and does not revoke previously registered tokens. Both switches retain
/// their values in static fields for this process, across tab, theme and scene rebuilds.
///
/// **When Force login applies.** At the next `OctopusSDK.Initialize`, not retroactively: the API key is an
/// argument of that one call, and this sample initialises once per process
/// (`OctopusScenarioSdk.EnsureInitialized`). Before the first scenario runs, the switch is live —
/// flip it, then run a connection preset, and the SDK comes up on the other community. After that
/// only Force login is locked. Push registration stays live and enabling it forwards the latest
/// cached device token when the SDK is initialised.
/// </summary>
public static class OctopusSampleFeatureToggles
{
    /// <summary>The switch's `GameObject.name`, matching Android's `scenarios-toggle-force-login`.</summary>
    public const string ForceLoginId = "scenarios-toggle-force-login";

    /// <summary>The label a tester reads, as on Android.</summary>
    public const string ForceLoginLabel = "Force login";

    /// <summary>Android's `effectWhenOn`, verbatim.</summary>
    public const string ForceLoginEffectOn = "Visitors must sign in before they can read or post.";

    /// <summary>Android's `effectWhenOff`, verbatim.</summary>
    public const string ForceLoginEffectOff = "Visitors can browse the community before signing in.";

    /// <summary>
    /// The console headline the design contract requires on every flip, and the one Android writes
    /// (`MainScaffold.kt`). The arrow is the character the contract uses, not "->".
    /// </summary>
    public const string ToggledHeadline = "feature toggled → SDK config rebuilt";

    /// <summary>Android's test tag; the row appends `-row`.</summary>
    public const string PushRegistrationId = "scenarios-toggle-push-registration";
    public const string PushRegistrationLabel = "Push registration";
    public const string PushRegistrationEffectOn = "New device tokens go to Octopus, so this device can be notified.";
    public const string PushRegistrationEffectOff = "Device tokens stay in the app; already-registered ones live until a reset.";

    private static bool _forceLogin;
    private static bool _pushRegistration = true;

    /// <summary>Whether device tokens may be registered; this choice can change after initialization.</summary>
    public static bool PushRegistration { get { return _pushRegistration; } }

    public static bool SetPushRegistration(bool enabled)
    {
        if (_pushRegistration == enabled) return false;
        _pushRegistration = enabled;
        OctopusSampleLog.Current.LogStateChange(
            ToggledHeadline,
            PushRegistrationLabel + " → " + (enabled ? "on" : "off") + "\n" + PushRegistrationEffect(enabled));
        if (enabled) OctopusSamplePushRegistration.RegisterCachedToken();
        return true;
    }

    public static string PushRegistrationEffect(bool enabled)
    {
        return enabled ? PushRegistrationEffectOn : PushRegistrationEffectOff;
    }

    /// <summary>
    /// Whether the next initialisation uses the forced-login community. Default off, which is
    /// Android's default too (`FeatureToggles.Default`).
    /// </summary>
    public static bool ForceLogin { get { return _forceLogin; } }

    /// <summary>
    /// Moves the switch and writes the contract's line into the Debug console. Returns false when
    /// the position did not change — because it was already there, or because the SDK is up and
    /// the switch has stopped deciding anything.
    ///
    /// The second refusal is deliberately silent and deliberately here rather than only in the
    /// header, which draws the switch dead once <see cref="ForceLoginLockedNote"/> speaks. That drawing is a
    /// snapshot: a header built before a scenario initialised the SDK is still on screen behind it,
    /// with a live-looking switch, until something repaints it. A flip accepted through that stale
    /// control would move the profile the presets name away from the one the running SDK actually
    /// came up with — the two would disagree for the rest of the process. The header repaints
    /// itself on <see cref="OctopusSampleState.Changed"/>; this is the backstop for the window
    /// before it does.
    /// </summary>
    public static bool SetForceLogin(bool enabled)
    {
        if (_forceLogin == enabled) return false;
        if (OctopusSampleState.IsInitialized) return false;
        _forceLogin = enabled;
        OctopusSampleLog.Current.LogStateChange(
            ToggledHeadline,
            ForceLoginLabel + " → " + (enabled ? "on" : "off") + "\n" + ForceLoginEffect(enabled));
        return true;
    }

    /// <summary>The sentence under the switch, for the position given.</summary>
    public static string ForceLoginEffect(bool enabled)
    {
        return enabled ? ForceLoginEffectOn : ForceLoginEffectOff;
    }

    /// <summary>
    /// What the Force login header says beneath the effect line once the SDK is up: the switch has stopped
    /// deciding anything until the app is restarted. Empty while it is still live, so the header
    /// draws no line at all rather than a reassuring one.
    /// </summary>
    public static string ForceLoginLockedNote()
    {
        return OctopusSampleState.IsInitialized
            ? "Locked: the SDK is already initialised. Restart the app to apply a change."
            : string.Empty;
    }

    /// <summary>
    /// Puts the switch back to its default. Written for EditMode tests — the app never calls it,
    /// because a process that flips it back has restarted anyway.
    /// </summary>
    public static void Reset()
    {
        _forceLogin = false;
        _pushRegistration = true;
    }
}
