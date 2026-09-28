using System.Collections.Generic;

public class OctopusScenario
{
    public readonly string Id;

    /// <summary>
    /// The catalogue's own spelling, e.g. "Connection" — copied verbatim from
    /// `scenarios-catalog.yaml`, which is why it reads like an SDK chapter heading rather than
    /// something a tester would say. Nothing renders it: the screens show
    /// <see cref="ProductTitle"/>. It stays because it is the string the shared catalogue and the
    /// QA analyzer spell out, and a reader comparing the two files needs one field that matches
    /// line for line.
    /// </summary>
    public readonly string Title;

    public readonly string Capability;

    /// <summary>
    /// The gesture, in the words of whoever performs it — verb first, e.g. "Switch the community's
    /// language". The card's heading and the scenario screen's app bar, so a tap never changes the
    /// name of what you are looking at.
    ///
    /// Android's `ScenarioSpec.title`, verbatim (`ScenarioCatalog.kt`): the wording model is a
    /// cross-platform contract, and re-writing it per platform is how four samples end up
    /// describing the same SDK four ways. Null on the scenarios this sample cannot demonstrate,
    /// which never reach a card — <see cref="OctopusScenarioCatalogTests"/> holds that line.
    /// </summary>
    public readonly string ProductTitle;

    /// <summary>
    /// What you will observe once it ran — one sentence, no type names, Android's
    /// `ScenarioSpec.subtitle` verbatim. Replaces <see cref="Capability"/> on the card: a list of
    /// the symbols a scenario calls answers "what does this cover?", which is not the question
    /// someone scrolling the Scenarios tab is asking. The symbols are not lost, they move to
    /// <see cref="ApiSymbol"/> and to the scenario screen's chips.
    /// </summary>
    public readonly string Subtitle;

    /// <summary>
    /// The one SDK entry point the card names, in Unity's own spelling, e.g. "OverrideDefaultLocale"
    /// where Android's chip reads `overrideDefaultLocale`.
    ///
    /// One per card, even where the pilot calls several: the chip exists so an integrator can find
    /// the scenario that matches the method they already know. It is checked against the pilot's
    /// own <see cref="OctopusScenarioPilot.ApiSymbols"/>, so it cannot drift into naming a method
    /// the scenario does not call — the confusion the chip exists to prevent. A slash separates the
    /// two halves of a scenario that is genuinely about a pair ("Reset / Stop"); both halves are
    /// checked.
    /// </summary>
    public readonly string ApiSymbol;

    /// <summary>
    /// The legacy `*Example` scene exercising the same capability, or null — the absorption ledger
    /// for issue #100, not a claim that anything is implemented. Rendered nowhere.
    /// </summary>
    public readonly string DemoScene;

    /// <summary>Why the scenario is permanently inapplicable, or null when applicable.</summary>
    public readonly string NotApplicableReason;

    /// <summary>
    /// The `presets[].test_id` values from the catalogue entry, verbatim and in order. Not yet
    /// consumed by any UI — carried here so a future harness doesn't have to re-derive them.
    /// </summary>
    public readonly string[] PresetTestIds;

    /// <summary>The catalogue entry's `result_test_id`, verbatim. Not yet consumed by any UI.</summary>
    public readonly string ResultTestId;

    // Expected outcome from the Android sample catalog; absent entries use neutral guidance.
    public readonly string YouWillSee;

    public OctopusScenario(string id, string title, string capability,
                           string demoScene = null, string notApplicableReason = null,
                           string[] presetTestIds = null, string resultTestId = null,
                           string youWillSee = null, string productTitle = null,
                           string subtitle = null, string apiSymbol = null)
    {
        Id = id;
        Title = title;
        Capability = capability;
        DemoScene = demoScene;
        NotApplicableReason = notApplicableReason;
        PresetTestIds = presetTestIds;
        ResultTestId = resultTestId;
        YouWillSee = youWillSee;
        ProductTitle = productTitle;
        Subtitle = subtitle;
        ApiSymbol = apiSymbol;
    }

    /// <summary>
    /// What the card and the app bar actually print: <see cref="ProductTitle"/> when the scenario
    /// has one, otherwise the catalogue <see cref="Title"/>. The fallback is never reached by a
    /// rendered card — the four scenarios without product wording are the four this sample can
    /// never demonstrate — and exists so that adding a catalogue row yields a readable screen
    /// before it yields a finished one.
    /// </summary>
    public string DisplayTitle
    {
        get { return string.IsNullOrEmpty(ProductTitle) ? Title : ProductTitle; }
    }

    /// <summary>The id the QA pipeline taps, per the catalogue's `&lt;section&gt;-&lt;element&gt;-&lt;action&gt;` rule.</summary>
    public string CardTestId => "scenarios-" + Id + "-card";
}

/// <summary>
/// The cross-platform QA scenario catalogue, as the Unity sample sees it.
///
/// The list below mirrors the shared QA scenario catalog (internal), which is the single
/// source of truth for every Octopus sample AND for the QA pipeline. Ids and titles are copied
/// from it verbatim: the pipeline taps a row by its id, so a spelling that drifts here is a row
/// the pipeline can no longer find. The `capability` strings are deliberately ABRIDGED for a
/// phone-sized row — read the YAML, not this file, when the exact wording matters.
///
/// What the screens *print* is a second layer on top of that copy: `productTitle`, `subtitle` and
/// `apiSymbol`, taken from the Android sample rather than from the YAML, because the YAML names
/// capabilities for a pipeline and a card has to name a gesture for a human. The two layers are
/// kept apart on purpose — a row can stay a faithful copy of the catalogue and still read like
/// product copy on screen.
///
/// Nothing is filtered out *here*. Every one of the 27 entries is kept, including the four that
/// are permanently inapplicable on Unity and say so with their reason, because a missing entry is
/// indistinguishable from a scenario nobody ever thought about. Nothing here reads the YAML at
/// build time, and <see cref="OctopusScenarioCatalogTests"/> does not either: it checks this copy's
/// internal shape — 27 entries, unique ids, the `&lt;id&gt;-result` convention, `qa-preset-` prefixes,
/// and that every `DemoScene` names a scene on disk. Drift against the shared catalog is caught by re-reading
/// the YAML when it changes, not by a test. What the
/// Scenarios tab *renders* is a different question, answered by
/// <see cref="OctopusScenarioSections"/>: TOKENS §1 lists only what the sample can actually
/// demonstrate.
///
/// A row's `DemoScene` names the legacy `*Example` scene that exercises the same SDK capability —
/// the absorption ledger for issue #100, which each scenario PR consumes one line of. It is not a
/// claim that anything is implemented, and since the Scenarios tab stopped listing unimplemented
/// scenarios it reaches no screen; `EveryDemoSceneNamesASceneThatExists` keeps it from rotting into
/// a list of scenes that were renamed away.
/// </summary>
public static class OctopusScenarioCatalog
{
    private const string NoRouteStack =
        "A Unity scene has no route stack, modal or sheet to present into: the whole public " +
        "presentation surface is OctopusSDK.Open / OpenGroup / OpenPost / OpenCreatePost, and " +
        "nothing selects a presentation.";

    public static readonly IReadOnlyList<OctopusScenario> All = new List<OctopusScenario>
    {
        new OctopusScenario("connection", "Connection",
            "connectUser / disconnectUser, connectionState stream",
            "SSOExample",
            presetTestIds: new[] { "qa-preset-connection-1", "qa-preset-connection-2", "qa-preset-connection-3", "qa-preset-connection-4", "qa-preset-connection-5" },
            resultTestId: "connection-result",
            productTitle: "Sign in and out as the demo user",
            subtitle: "The community shows the demo user as signed in: they can post, react and be " +
                      "notified.",
            apiSymbol: "ConnectUser",
            youWillSee: "the Community tab greets the demo user, and the composer unlocks."),
        new OctopusScenario("groups", "Groups",
            "fetchGroups, followGroup / unfollowGroup",
            presetTestIds: new[] { "qa-preset-groups-1" },
            resultTestId: "groups-result",
            productTitle: "Browse the community's groups",
            subtitle: "Every group the community offers, with whether the demo user follows it.",
            apiSymbol: "FetchGroups"),
        new OctopusScenario("syncFollowGroups", "Sync Followed Groups",
            "syncFollowGroups (batch follow / unfollow with per-action timestamps)",
            presetTestIds: new[] { "qa-preset-syncFollowGroups-1", "qa-preset-syncFollowGroups-2", "qa-preset-syncFollowGroups-3" },
            resultTestId: "syncFollowGroups-result",
            productTitle: "Follow or unfollow every group at once",
            subtitle: "The follow state of every group flips in one call, and the community reflects it.",
            apiSymbol: "SyncFollowGroups"),
        new OctopusScenario("notSeenNotifications", "Not-Seen Notifications",
            "notSeenNotificationsCount stream + updateNotSeenNotificationsCount",
            presetTestIds: new[] { "qa-preset-notSeenNotifications-1", "qa-preset-notSeenNotifications-2" },
            resultTestId: "notSeenNotifications-result",
            productTitle: "Set the unread notification count",
            subtitle: "The notification badge in the community carries the number you set.",
            apiSymbol: "UpdateNotSeenNotificationsCount"),
        new OctopusScenario("pushNotifications", "Push Notifications",
            "isOctopusNotification / getOctopusNotification / openNotification",
            "PushNotificationsExample",
            presetTestIds: new[] { "qa-preset-pushNotifications-1" },
            resultTestId: "pushNotifications-result",
            productTitle: "Open a post from a push notification",
            subtitle: "A simulated push is recognised as an Octopus one and opens the post it points at.",
            apiSymbol: "GetOctopusNotification"),
        new OctopusScenario("communityAccess", "Community Access",
            "overrideCommunityAccess, trackAccessToCommunity, hasAccessToCommunity stream",
            "EventsExample",
            presetTestIds: new[] { "qa-preset-communityAccess-1", "qa-preset-communityAccess-2", "qa-preset-communityAccess-3" },
            resultTestId: "communityAccess-result",
            productTitle: "Grant or deny access to the community",
            subtitle: "The community either opens normally, or shows its no-access state.",
            apiSymbol: "OverrideCommunityAccess"),
        new OctopusScenario("customEvents", "Custom Events",
            "track(CustomEvent)",
            "EventsExample",
            presetTestIds: new[] { "qa-preset-customEvents-1", "qa-preset-customEvents-2" },
            resultTestId: "customEvents-result",
            productTitle: "Send a custom analytics event",
            subtitle: "The event shows up in the Events log, with the properties you attached.",
            apiSymbol: "Track",
            youWillSee: "the event at the top of the Events log."),
        new OctopusScenario("locale", "Locale",
            "overrideDefaultLocale",
            "LanguageOverrideExample",
            presetTestIds: new[] { "qa-preset-locale-1", "qa-preset-locale-2", "qa-preset-locale-3" },
            resultTestId: "locale-result",
            productTitle: "Switch the community's language",
            subtitle: "The community's own labels switch to the language you picked.",
            apiSymbol: "OverrideDefaultLocale",
            youWillSee: "the community's buttons and labels in the language you picked."),
        new OctopusScenario("lifecycle", "Lifecycle",
            "switchCommunity, reset, stop, isInitialised stream",
            presetTestIds: new[] { "qa-preset-lifecycle-1", "qa-preset-lifecycle-2", "qa-preset-lifecycle-3" },
            resultTestId: "lifecycle-result",
            productTitle: "Restart or stop the SDK",
            subtitle: "The session ends and the community goes back to its not-connected state.",
            apiSymbol: "Reset / Stop"),
        new OctopusScenario("bridge", "Bridge",
            "fetchOrCreateClientObjectRelatedPost (+ tokenProvider)",
            presetTestIds: new[] { "qa-preset-bridge-1", "qa-preset-bridge-2", "qa-preset-bridge-3", "qa-preset-bridge-4" },
            resultTestId: "bridge-result",
            productTitle: "Open the post attached to a product",
            subtitle: "A post tied to a host object opens — created on the spot the first time you ask for " +
                      "it.",
            apiSymbol: "FetchOrCreateClientObjectRelatedPost"),
        new OctopusScenario("createPost", "Create Post (Bridge Share)",
            "OctopusCreatePostScreen + OctopusPrefilledPost (+ OctopusPostCTA)",
            "OpenScreenExample",
            presetTestIds: new[] { "qa-preset-createPost-1", "qa-preset-createPost-2", "qa-preset-createPost-3", "qa-preset-createPost-4", "qa-preset-createPost-5" },
            resultTestId: "createPost-result",
            productTitle: "Start a post from the host app",
            subtitle: "The composer opens pre-filled with the text, image or link the host handed over.",
            apiSymbol: "OpenCreatePost"),
        new OctopusScenario("reactions", "Reactions",
            "setReaction (set / change / unreact)",
            presetTestIds: new[] { "qa-preset-reactions-1", "qa-preset-reactions-2", "qa-preset-reactions-3", "qa-preset-reactions-4", "qa-preset-reactions-5", "qa-preset-reactions-6", "qa-preset-reactions-7" },
            resultTestId: "reactions-result",
            productTitle: "React to a post from the host app",
            subtitle: "The reaction you picked shows on that post in the community.",
            apiSymbol: "SetReaction"),
        new OctopusScenario("theme", "Theme",
            "custom OctopusTheme (colors, fonts, logo)",
            "CustomThemesExample",
            presetTestIds: new[] { "qa-preset-theme-1", "qa-preset-theme-2", "qa-preset-theme-3", "qa-preset-theme-4", "qa-preset-theme-5" },
            resultTestId: "theme-result",
            productTitle: "Apply the host's brand to the community",
            subtitle: "The community adopts the host's colours, fonts and logo — or drops back to the " +
                      "SDK's own look.",
            apiSymbol: "SetLightColorScheme"),
        new OctopusScenario("communityData", "Community Data (Unified Profile)",
            "fetchCommunityData / communityDataFlow, OctopusCommunityData / OctopusGamification",
            presetTestIds: new[] { "qa-preset-communityData-1", "qa-preset-communityData-2", "qa-preset-communityData-3", "qa-preset-communityData-4", "qa-preset-communityData-5", "qa-preset-communityData-6" },
            resultTestId: "communityData-result",
            productTitle: "Read a member's community profile",
            subtitle: "The member's community profile comes back, and keeps updating while you watch it.",
            apiSymbol: "FetchCommunityData"),
        new OctopusScenario("profileFieldsLock", "Profile Field Lock",
            "per-field profile lock (nickname / avatar / bio)",
            "ManagedFieldsExample",
            presetTestIds: new[] { "qa-preset-profileFieldsLock-1", "qa-preset-profileFieldsLock-2", "qa-preset-profileFieldsLock-3", "qa-preset-profileFieldsLock-clear" },
            resultTestId: "profileFieldsLock-result",
            productTitle: "Lock the profile fields the host owns",
            subtitle: "The locked fields turn read-only in the community's profile editor.",
            apiSymbol: "DebugOverrideProfileFieldsLock"),
        new OctopusScenario("contentOptions", "Content Options",
            "per-content-type content options (pictures post/comment/reply, polls post)",
            presetTestIds: new[] { "qa-preset-contentOptions-1", "qa-preset-contentOptions-2", "qa-preset-contentOptions-3", "qa-preset-contentOptions-4", "qa-preset-contentOptions-5", "qa-preset-contentOptions-6", "qa-preset-contentOptions-clear" },
            resultTestId: "contentOptions-result",
            productTitle: "Change what members are allowed to post",
            subtitle: "The composer offers only the kinds of content the community allows.",
            apiSymbol: "DebugOverrideContentOptions"),
        new OctopusScenario("termsAcceptance", "Terms Acceptance (Consent)",
            "CommunityConfig.termsAcceptanceMode + the consent sheet at the first contribution",
            presetTestIds: new[] { "qa-preset-termsAcceptance-1", "qa-preset-termsAcceptance-2", "qa-preset-termsAcceptance-3", "qa-preset-termsAcceptance-clear" },
            resultTestId: "termsAcceptance-result",
            productTitle: "Change how members accept the terms",
            subtitle: "The community asks for consent the way you picked, or stops asking altogether.",
            apiSymbol: "DebugOverrideTermsAcceptanceMode"),
        new OctopusScenario("events", "Events (Analytics Stream)",
            "OctopusSDK.events typed analytics stream",
            "EventsExample",
            presetTestIds: new[] { "qa-preset-events-1" },
            resultTestId: "events-result",
            productTitle: "Watch what the SDK reports",
            subtitle: "Every event the SDK emits, live, as the community is used.",
            apiSymbol: "OnOctopusEvent"),
        new OctopusScenario("refreshEntitlements", "Refresh Entitlements",
            "refreshEntitlements, observed through OctopusProfile.entitlements",
            presetTestIds: new[] { "qa-preset-refreshEntitlements-1" },
            resultTestId: "refreshEntitlements-result",
            productTitle: "Re-issue the demo user's entitlements",
            subtitle: "The community picks up the entitlements you edited, without signing out.",
            apiSymbol: "RefreshEntitlements"),
        new OctopusScenario("groupAccessDenied", "Group Access Denied Callback",
            "setGroupAccessDeniedCallback",
            presetTestIds: new[] { "qa-preset-groupAccessDenied-1", "qa-preset-groupAccessDenied-2" },
            resultTestId: "groupAccessDenied-result",
            productTitle: "Handle a member reaching a closed group",
            subtitle: "The host is told a group was refused, instead of the community handling it alone.",
            apiSymbol: "OnGroupAccessDenied"),
        new OctopusScenario("initialScreen", "Initial Screen",
            "OctopusInitialScreen variants + the standalone post-details / create-post screens",
            "OpenScreenExample",
            presetTestIds: new[] { "qa-preset-initialScreen-1", "qa-preset-initialScreen-2", "qa-preset-initialScreen-3", "qa-preset-initialScreen-4", "qa-preset-initialScreen-5", "qa-preset-initialScreen-6", "qa-preset-initialScreen-7", "qa-preset-initialScreen-8", "qa-preset-initialScreen-9", "qa-preset-initialScreen-10", "qa-preset-initialScreen-11" },
            resultTestId: "initialScreen-result",
            productTitle: "Open the community on a chosen screen",
            subtitle: "The community opens directly on the member, profile or feed you picked.",
            apiSymbol: "Open"),
        new OctopusScenario("fullscreen", "Fullscreen Presentation",
            "the embedded community as a pushed host route",
            null, NoRouteStack,
            presetTestIds: new[] { "qa-preset-fullscreen-1", "qa-preset-fullscreen-2", "qa-preset-fullscreen-3" },
            resultTestId: "fullscreen-result"),
        new OctopusScenario("modal", "Modal Presentation",
            "the embedded community presented as a full-screen modal",
            null, NoRouteStack,
            presetTestIds: new[] { "qa-preset-modal-1", "qa-preset-modal-2" },
            resultTestId: "modal-result"),
        new OctopusScenario("sheet", "Sheet Presentation",
            "the embedded community inside a non-fullscreen bottom sheet",
            null, NoRouteStack,
            presetTestIds: new[] { "qa-preset-sheet-1", "qa-preset-sheet-2" },
            resultTestId: "sheet-result"),
        new OctopusScenario("embeddedBack", "Embedded Back Button",
            "the embedded community's leading top-app-bar icon (showBackButton / navBarLeadingAction) "
            + "and the callback it fires on the SDK's root screen",
            null,
            "no embedded view exists to put a leading icon on: the public surface is "
            + "OctopusSDK.Open/OpenGroup/OpenPost/OpenCreatePost only, which hands the community to a "
            + "native surface through the bridge — a Unity host owns no container to dismiss and has "
            + "no callback to receive",
            presetTestIds: new[] {
                "qa-preset-embeddedBack-1", "qa-preset-embeddedBack-2", "qa-preset-embeddedBack-3",
                "qa-preset-embeddedBack-4", "qa-preset-embeddedBack-5"
            },
            resultTestId: "embeddedBack-result"),
        new OctopusScenario("trackABTests", "Track A/B Tests (host-decided access)",
            "trackCommunityAccess / trackAccessToCommunity",
            "EventsExample",
            presetTestIds: new[] { "qa-preset-trackABTests-1" },
            resultTestId: "trackABTests-result",
            productTitle: "Report which cohort the member is in",
            subtitle: "The community opens for the cohort you granted, and refuses the other one.",
            apiSymbol: "TrackAccessToCommunity"),
        new OctopusScenario("forceOctopusABTests", "Force Octopus A/B Tests (SDK-side override)",
            "overrideCommunityAccess with its typed OverrideCommunityAccessError branches",
            presetTestIds: new[] { "qa-preset-forceOctopusABTests-1", "qa-preset-forceOctopusABTests-2", "qa-preset-forceOctopusABTests-3" },
            resultTestId: "forceOctopusABTests-result",
            productTitle: "Force Octopus's own A/B decision",
            subtitle: "The community's own access rule is overridden — granted or denied — until you clear " +
                      "it.",
            apiSymbol: "OverrideCommunityAccess"),
    };
}
