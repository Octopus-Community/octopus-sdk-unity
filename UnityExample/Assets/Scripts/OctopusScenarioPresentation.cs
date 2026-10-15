using System.Collections.Generic;

/// <summary>Android card identities, kept separate from the C# entry points the pilots call.</summary>
public static class OctopusScenarioPresentation
{
    private static readonly Dictionary<string, string> Apis = new Dictionary<string, string>
    {
        { "unifiedProfile", "onNavigateToProfile" }, { "connection", "connectUser" },
        { "groups", "fetchGroups" }, { "syncFollowGroups", "syncFollowGroups" },
        { "notSeenNotifications", "updateNotSeenNotificationsCount" },
        { "pushNotifications", "getOctopusNotification" }, { "communityAccess", "overrideCommunityAccess" },
        { "customEvents", "track" }, { "locale", "overrideDefaultLocale" }, { "lifecycle", "reset / stop" },
        { "bridge", "fetchOrCreateClientObjectRelatedPost" }, { "createPost", "navigateToOctopusCreatePost" },
        { "reactions", "setReaction" }, { "theme", "OctopusTheme" }, { "communityData", "fetchCommunityData" },
        { "profileFieldsLock", "debugOverrideProfileFieldsLock" },
        { "contentOptions", "debugOverrideContentOptions" }, { "termsAcceptance", "debugOverrideTermsAcceptanceMode" },
        { "events", "OctopusSDK.events" }, { "refreshEntitlements", "refreshEntitlements" },
        { "groupAccessDenied", "setGroupAccessDeniedCallback" }, { "initialScreen", "navigateToOctopusHome" },
        { "trackABTests", "trackAccessToCommunity" }, { "forceOctopusABTests", "overrideCommunityAccess" }
    };

    public static string ApiFor(string id)
    {
        string api;
        return Apis.TryGetValue(id, out api) ? api : id;
    }

    // The reference argument is shown where Unity can drive the same value. C# differences
    // are explained alongside the controls rather than advertised as new SDK types.
    public static string[] ParametersFor(string id)
    {
        switch (id)
        {
            case "communityData": return new[] { "userId: String", "the id typed below; initially the user.other fixture or the last lookup" };
            case "connection": return new[] { "userId: String", "the SSO user stored in Configuration" };
            case "syncFollowGroups": return new[] { "actions: List<OctopusSyncFollowGroupAction>", "one action per followable group, dated now + the offset picked below" };
            case "communityAccess":
            case "forceOctopusABTests":
            case "trackABTests": return new[] { "hasAccess: Boolean", "true to grant, false to deny" };
            case "customEvents": return new[] { "eventName: String", "properties: IDictionary<String, String>" };
            case "locale": return new[] { "languageCode: String", "fr, en, or empty to reset" };
            default: return new string[0];
        }
    }

    public static bool CanVerify(string id)
    {
        switch (id)
        {
            case "unifiedProfile": case "connection": case "groups": case "syncFollowGroups":
            case "communityAccess": case "forceOctopusABTests": case "reactions": case "bridge":
            case "createPost": case "theme": case "locale": case "profileFieldsLock":
            case "contentOptions": case "termsAcceptance": case "notSeenNotifications":
            case "refreshEntitlements": return true;
            default: return false;
        }
    }
}
