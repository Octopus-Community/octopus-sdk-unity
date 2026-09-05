using System.Collections.Generic;

/// <summary>Whether the Unity sample has a QA harness for a scenario at all.</summary>
public enum ScenarioStatus
{
    /// <summary>No QA harness for it in this sample yet.</summary>
    NotImplemented,

    /// <summary>The concept does not exist on Unity — a permanent absence, not a gap.</summary>
    NotApplicable,
}

public class OctopusScenario
{
    public readonly string Id;
    public readonly string Title;
    public readonly string Capability;
    public readonly ScenarioStatus Status;

    /// <summary>Existing example scene exercising the same capability, or null. Not a claim.</summary>
    public readonly string DemoScene;

    /// <summary>Why the scenario is inapplicable, for <see cref="ScenarioStatus.NotApplicable"/>.</summary>
    public readonly string NotApplicableReason;

    public OctopusScenario(string id, string title, string capability, ScenarioStatus status,
                           string demoScene = null, string notApplicableReason = null)
    {
        Id = id;
        Title = title;
        Capability = capability;
        Status = status;
        DemoScene = demoScene;
        NotApplicableReason = notApplicableReason;
    }

    /// <summary>The id the QA pipeline taps, per the catalogue's `&lt;section&gt;-&lt;element&gt;-&lt;action&gt;` rule.</summary>
    public string CardTestId => "scenarios-" + Id + "-card";
}

/// <summary>
/// The cross-platform QA scenario catalogue, as the Unity sample sees it.
///
/// The list below mirrors `shared/config/scenarios-catalog.yaml` in pm-tools, which is the single
/// source of truth for every Octopus sample AND for the QA pipeline. Ids and titles are copied
/// from it verbatim: the pipeline taps a row by its id, so a spelling that drifts here is a row
/// the pipeline can no longer find. The `capability` strings are deliberately ABRIDGED for a
/// phone-sized row — read the YAML, not this file, when the exact wording matters.
///
/// Nothing is filtered out. A scenario the Unity sample does not implement is rendered as
/// "Not implemented" rather than omitted, because a missing row is indistinguishable from a
/// scenario nobody ever thought about, and the whole point of the catalogue is to make the gap
/// countable. Three scenarios are permanently inapplicable here and say so with their reason.
///
/// A row's `DemoScene` is not a claim that a scenario is implemented. It points at the
/// existing example scene that exercises the same SDK capability, so a QA pass can reach it;
/// none of these scenes ships the `qa-preset-*` ids the pipeline drives, which is exactly why
/// every row's status is still <see cref="ScenarioStatus.NotImplemented"/>.
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
            ScenarioStatus.NotImplemented, "SSOExample"),
        new OctopusScenario("groups", "Groups",
            "fetchGroups, followGroup / unfollowGroup",
            ScenarioStatus.NotImplemented),
        new OctopusScenario("syncFollowGroups", "Sync Followed Groups",
            "syncFollowGroups (batch follow / unfollow with per-action timestamps)",
            ScenarioStatus.NotImplemented),
        new OctopusScenario("notSeenNotifications", "Not-Seen Notifications",
            "notSeenNotificationsCount stream + updateNotSeenNotificationsCount",
            ScenarioStatus.NotImplemented),
        new OctopusScenario("pushNotifications", "Push Notifications",
            "isOctopusNotification / getOctopusNotification / openNotification",
            ScenarioStatus.NotImplemented, "PushNotificationsExample"),
        new OctopusScenario("communityAccess", "Community Access",
            "overrideCommunityAccess, trackAccessToCommunity, hasAccessToCommunity stream",
            ScenarioStatus.NotImplemented, "EventsExample"),
        new OctopusScenario("customEvents", "Custom Events",
            "track(CustomEvent)",
            ScenarioStatus.NotImplemented, "EventsExample"),
        new OctopusScenario("locale", "Locale",
            "overrideDefaultLocale",
            ScenarioStatus.NotImplemented, "LanguageOverrideExample"),
        new OctopusScenario("lifecycle", "Lifecycle",
            "switchCommunity, reset, stop, isInitialised stream",
            ScenarioStatus.NotImplemented),
        new OctopusScenario("bridge", "Bridge",
            "fetchOrCreateClientObjectRelatedPost (+ tokenProvider)",
            ScenarioStatus.NotImplemented),
        new OctopusScenario("createPost", "Create Post (Bridge Share)",
            "OctopusCreatePostScreen + OctopusPrefilledPost (+ OctopusPostCTA)",
            ScenarioStatus.NotImplemented, "OpenScreenExample"),
        new OctopusScenario("reactions", "Reactions",
            "setReaction (set / change / unreact)",
            ScenarioStatus.NotImplemented),
        new OctopusScenario("theme", "Theme",
            "custom OctopusTheme (colors, fonts, logo)",
            ScenarioStatus.NotImplemented, "CustomThemesExample"),
        new OctopusScenario("communityData", "Community Data (Unified Profile)",
            "fetchCommunityData / communityDataFlow, OctopusCommunityData / OctopusGamification",
            ScenarioStatus.NotImplemented),
        new OctopusScenario("profileFieldsLock", "Profile Field Lock",
            "per-field profile lock (nickname / avatar / bio)",
            ScenarioStatus.NotImplemented, "ManagedFieldsExample"),
        new OctopusScenario("contentOptions", "Content Options",
            "per-content-type content options (pictures post/comment/reply, polls post)",
            ScenarioStatus.NotImplemented),
        new OctopusScenario("termsAcceptance", "Terms Acceptance (Consent)",
            "CommunityConfig.termsAcceptanceMode + the consent sheet at the first contribution",
            ScenarioStatus.NotImplemented),
        new OctopusScenario("events", "Events (Analytics Stream)",
            "OctopusSDK.events typed analytics stream",
            ScenarioStatus.NotImplemented, "EventsExample"),
        new OctopusScenario("refreshEntitlements", "Refresh Entitlements",
            "refreshEntitlements, observed through OctopusProfile.entitlements",
            ScenarioStatus.NotImplemented),
        new OctopusScenario("groupAccessDenied", "Group Access Denied Callback",
            "setGroupAccessDeniedCallback",
            ScenarioStatus.NotImplemented),
        new OctopusScenario("initialScreen", "Initial Screen",
            "OctopusInitialScreen variants + the standalone post-details / create-post screens",
            ScenarioStatus.NotImplemented, "OpenScreenExample"),
        new OctopusScenario("fullscreen", "Fullscreen Presentation",
            "the embedded community as a pushed host route",
            ScenarioStatus.NotApplicable, null, NoRouteStack),
        new OctopusScenario("modal", "Modal Presentation",
            "the embedded community presented as a full-screen modal",
            ScenarioStatus.NotApplicable, null, NoRouteStack),
        new OctopusScenario("sheet", "Sheet Presentation",
            "the embedded community inside a non-fullscreen bottom sheet",
            ScenarioStatus.NotApplicable, null, NoRouteStack),
        new OctopusScenario("trackABTests", "Track A/B Tests (host-decided access)",
            "trackCommunityAccess / trackAccessToCommunity",
            ScenarioStatus.NotImplemented, "EventsExample"),
        new OctopusScenario("forceOctopusABTests", "Force Octopus A/B Tests (SDK-side override)",
            "overrideCommunityAccess with its typed OverrideCommunityAccessError branches",
            ScenarioStatus.NotImplemented),
    };
}
