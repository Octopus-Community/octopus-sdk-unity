using System;

/// <summary>
/// The tester's switch for the community's `exposeClientUserId` flag, and the route that makes it
/// observable: with the flag on, tapping a member avatar in the native community opens this app's
/// own profile page (<see cref="OctopusSampleClientProfileView"/>) instead of the SDK profile.
///
/// The flag is server-driven and its value depends on the community and backend host the SDK runs
/// on (off on some demo communities, on on others), so without an override a tester can only
/// observe whatever the community's backend serves. <see cref="OctopusSDK.DebugOverrideExposeClientUserId(bool?)"/>
/// forces it for this process. The switch lives where the Android sample puts its
/// `UnifiedProfileSection` — Scenarios › Sign-in &amp; user, a collapsed section after the cards —
/// with the same three choices, labels and test ids ("Use backend value" / "Force active" /
/// "Force inactive", `expose-override-backend` / `-on` / `-off`), drawn by
/// <see cref="OctopusScenariosListView"/>. The override is debug-only and in-memory on the
/// native side, so it is kept here too — a restart returns to the backend value — and it is
/// re-applied after every initialisation and community switch, where a fresh SDK instance would
/// otherwise start from the backend again.
///
/// The profile route (<see cref="OctopusSDK.NavigateToProfileHandler"/>) is installed before the
/// SDK starts and stays on by default, as the Android sample wires `onNavigateToProfile` by
/// default: the native SDK only intercepts a tap for a member whose client user id is exposed, so
/// with the flag off every avatar keeps opening the SDK profile. <see cref="SetRouteWired"/> takes
/// the route off, like the Flutter sample's `onNavigateToProfile` switch; on iOS the handler is
/// also what turns the native Unified Profile gate on for profile-edit routing, so the switch lets
/// a tester compare both behaviours (#393).
/// </summary>
public static class OctopusSampleUnifiedProfile
{
    /// <summary>The name a tester reads, flag name included so it matches the backend console.</summary>
    public const string Label = "Unified Profile (exposeClientUserId)";

    /// <summary>How other screens point at the section: the read-out line's own label.</summary>
    public const string SectionName = "Unified Profile";

    /// <summary>
    /// The section's intro — Android's sentence, except the last clause: Android points at the live
    /// config line above the list, this sample at the read-out inside the section.
    /// </summary>
    public const string Intro =
        "Overrides the backend exposeClientUserId flag. Use backend value (default), or force Unified Profile " +
        "active/inactive. Watch the \"Unified Profile\" line below flip with it.";

    /// <summary>The console headline written on every change of the override.</summary>
    public const string ChangedHeadline = "Unified Profile override changed";

    /// <summary>The console headline written when the host profile route is wired or unwired.</summary>
    public const string RouteChangedHeadline = "Unified Profile route changed";

    private static bool? _override;
    private static bool? _effective;
    private static bool _effectiveRead;
    private static string _readError;
    private static int _generation;
    private static bool _routeWired = true;

    /// <summary>Raised on the main thread when the override or the effective value changes.</summary>
    public static event Action Changed;

    /// <summary>The forced value; null keeps the backend value.</summary>
    public static bool? Override { get { return _override; } }

    /// <summary>The last effective value read from the SDK; null while unknown.</summary>
    public static bool? Effective { get { return _effective; } }

    /// <summary>
    /// Forces the flag on or off (null restores the backend value). Applied at once when the SDK is
    /// running, otherwise at the next initialisation.
    /// </summary>
    public static void SetOverride(bool? value)
    {
        _override = value;
        OctopusSampleLog.Current.LogStateChange(ChangedHeadline, Label + " → " + OverrideLabel(value));
        if (OctopusSampleState.IsInitialized)
        {
            Apply();
            RefreshEffective();
        }
        else
        {
            ++_generation;
            _effective = null;
            _effectiveRead = false;
            _readError = null;
        }
        Raise();
    }

    /// <summary>
    /// Re-applies a forced value to the SDK that just started or switched community. Makes no call
    /// while the backend value is kept, so a default run is unchanged.
    /// </summary>
    public static void Reapply()
    {
        ++_generation;
        _effective = null;
        _effectiveRead = false;
        _readError = null;
        if (_override.HasValue) Apply();
        // A screen showing the effective value (Scenarios' Unified Profile section) would
        // otherwise stay on "reading…" until the next tap (#393). Nobody listening keeps a default run call-free.
        Raise();
        if (Changed != null && OctopusSampleState.IsInitialized) RefreshEffective();
    }

    /// <summary>Reads the effective flag from the SDK; a no-op before initialisation.</summary>
    public static void RefreshEffective()
    {
        if (!OctopusSampleState.IsInitialized) return;
        var generation = ++_generation;
        try
        {
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.DebugGetCommunityConfig", "");
            OctopusScenarioSdk.Current.DebugGetCommunityConfig(config =>
            {
                if (generation != _generation) return;
                _effectiveRead = true;
                _readError = null;
                _effective = config == null ? (bool?)null : config.ExposeClientUserId;
                Raise();
            }, error =>
            {
                if (generation != _generation) return;
                _effectiveRead = true;
                _readError = error;
                _effective = null;
                Raise();
            });
        }
        catch (Exception exception)
        {
            // Same guard as the callbacks: a newer read owns the displayed value.
            if (generation != _generation) return;
            _effectiveRead = true;
            _readError = exception.Message;
            Raise();
        }
    }

    /// <summary>The label of an override choice — the Android sample's wording, verbatim.</summary>
    public static string OverrideLabel(bool? value)
    {
        return !value.HasValue ? "Use backend value" : value.Value ? "Force active" : "Force inactive";
    }

    /// <summary>
    /// The value of the section's "Unified Profile" read-out line: ✓ on / ✗ off once the SDK has
    /// answered, the same marks as the Android live-config line and the Flutter and React Native
    /// sections.
    /// </summary>
    public static string LiveValue
    {
        get
        {
            if (!OctopusSampleState.IsInitialized) return "— start the SDK";
            if (!_effectiveRead) return "reading…";
            if (_readError != null) return "unreadable here";
            if (!_effective.HasValue) return "— not fetched yet";
            return _effective.Value ? "✓ on" : "✗ off";
        }
    }

    /// <summary>The effective value in words, with where it comes from.</summary>
    public static string EffectiveText
    {
        get
        {
            if (!OctopusSampleState.IsInitialized)
                return _override.HasValue
                    ? OverrideLabel(_override) + " once the SDK starts"
                    : "unknown until the SDK starts";
            if (!_effectiveRead) return "reading…";
            if (_readError != null) return "unavailable (" + _readError + ")";
            if (!_effective.HasValue) return "unavailable (no community config yet)";
            return (_effective.Value ? "On" : "Off") + (_override.HasValue ? " (forced)" : " (backend)");
        }
    }

    /// <summary>The one-line hint the `communityData` scenario screen shows.</summary>
    public static string ScenarioHint
    {
        get
        {
            return "Avatar taps open the host profile only when " + Label + " is on — effective now: " +
                   EffectiveText + ". Change it in Scenarios › Sign-in & user › " + SectionName + ".";
        }
    }

    /// <summary>
    /// Installs the host profile route on the SDK facade. Called before <c>Initialize</c> — both
    /// natives read the switch at launch — and safe to call again.
    /// </summary>
    public static void EnsureRouted()
    {
        OctopusScenarioSdk.Current.SetNavigateToProfileHandler(
            _routeWired ? (Action<string>)(id => OpenHostProfile(id)) : null);
    }

    /// <summary>Whether avatar taps may be routed to the host profile page. On by default.</summary>
    public static bool RouteWired { get { return _routeWired; } }

    /// <summary>
    /// Wires or unwires the host profile route. Applied at once (both natives accept a handler
    /// change on a running SDK) and kept for the next start; in memory only, like the override.
    /// </summary>
    public static void SetRouteWired(bool wired)
    {
        if (_routeWired == wired) return;
        _routeWired = wired;
        OctopusSampleLog.Current.LogStateChange(RouteChangedHeadline,
            "NavigateToProfileHandler → " + (wired ? "wired" : "not wired"));
        EnsureRouted();
        Raise();
    }

    /// <summary>
    /// The route itself: opens the host profile page for the tapped member and fetches the public
    /// community data it renders. The native side has already closed the community.
    /// </summary>
    public static OctopusSampleClientProfileView OpenHostProfile(string clientUserId)
    {
        OctopusSampleLog.Current.LogStateChange("Profile tap routed to the host", "clientUserId");
        var destination = new OctopusScenarioHostProfile(clientUserId);
        var view = OctopusSampleClientProfileView.Open(destination);
        try
        {
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.FetchCommunityData", "clientUserId");
            OctopusScenarioSdk.Current.FetchCommunityData(OctopusCommunityMemberId.FromClientUserId(clientUserId),
                data => destination.Complete(data),
                error => destination.Complete(null, error));
        }
        catch (Exception exception) { destination.Complete(null, exception.Message); }
        return view;
    }

    /// <summary>
    /// Forgets the override and the last read; subscribers are kept. EditMode only, through
    /// <see cref="OctopusScenarioSdk.Use"/>.
    /// </summary>
    public static void Reset()
    {
        ++_generation;
        _override = null;
        _routeWired = true;
        _effective = null;
        _effectiveRead = false;
        _readError = null;
    }

    private static void Apply()
    {
        var value = _override;
        try
        {
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.DebugOverrideExposeClientUserId",
                value.HasValue ? (value.Value ? "true" : "false") : "null");
            OctopusScenarioSdk.Current.DebugOverrideExposeClientUserId(value);
        }
        catch (Exception exception)
        {
            OctopusSampleLog.Current.LogStateChange("Unified Profile override failed", exception.Message);
        }
    }

    private static void Raise()
    {
        var handler = Changed;
        if (handler != null) handler();
    }
}
