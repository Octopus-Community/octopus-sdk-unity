using System.Collections.Generic;
using UnityEngine;

public static class OctopusSampleDiagnostics
{
    public static IList<OctopusSampleDeveloperToolsView.InfoFact> Facts()
    {
        var draft = OctopusScenarioSdk.ConfigurationDraft();
        return new List<OctopusSampleDeveloperToolsView.InfoFact>
        {
            Row("debug-info-sdk-version", "SDK version", OctopusSDK.Version),
            Row("debug-info-native-build-pins", "Native build pins", OctopusSampleNativePins.Current()),
            Row("debug-info-initialized", "Initialized (sample record)", OctopusSampleState.IsInitialized ? "True" : "False"),
            Row("debug-info-auth-mode", "Auth mode", OctopusSampleState.ModeLabel ?? "Unknown — not initialized"),
            Row("debug-info-last-connection-call", "Last connection call", OctopusSampleHomeView.ConnectionStatusLabel()),
            Row("debug-info-connection", "Native connection state", "Not exposed by the Unity SDK"),
            Row("debug-info-network", "Network", Application.internetReachability == NetworkReachability.NotReachable ? "No network interface" : "Network interface available (not a server reachability check)"),
            Row("debug-info-unread", "Unread notifications", OctopusSampleState.UnseenNotifications < 0 ? "Not reported" : OctopusSampleState.UnseenNotifications.ToString()),
            Row("debug-info-access", "Community access", OctopusSampleState.HasCommunityAccess ? "Granted" : "Not granted"),
            Row("debug-info-force-login", "Force login", OctopusSampleFeatureToggles.ForceLogin ? "On" : "Off"),
            Row("debug-info-push-registration", "Push registration", OctopusSampleFeatureToggles.PushRegistration ? "On" : "Off"),
            Row("debug-info-server-environment", "Server environment", draft.CustomServer ? "Custom" : "Demo"),
            Row("debug-info-host", "Host", OctopusScenarioSdk.RunningServerHost ?? draft.ResolveHost(OctopusScenarioSdk.Current.DemoServerHost)),
            Row("debug-info-community", "Community", draft.Community == 5 ? "Custom community" : "Demo community"),
            Row("debug-info-api-key-source", "API key source", draft.Community == 5 ? "In-memory custom key (masked)" : "Demo preset · " + draft.CommunityLabel),
            Row("debug-info-sso-user", "SSO user", draft.UserId ?? "Not configured"),
            Row("debug-info-entitlements", "Entitlements", "Carried in the demo SSO token"),
            Row("debug-info-theme", "Theme", OctopusSampleSettingsView.AppearanceText()),
            Row("debug-info-language", "Language", OctopusSampleSettingsView.LanguageText()),
            Row("debug-info-profile-route", "Route profile taps", OctopusSampleUnifiedProfile.RouteWired ? "Wired (requires exposed host user id)" : "Not wired"),
            Row("debug-info-profile-edit", "App profile editor", "Not exposed by the Unity SDK"),
            Row("debug-info-login-route", "Open your sign-in screen", "OnLoginRequired → Account (required)"),
            Row("debug-info-client-object-route", "Handle content button taps", OctopusSampleHostIntegration.ClientObject ? "Wired" : "Not wired"),
            Row("debug-info-group-route", "Gate access to locked groups", OctopusSampleHostIntegration.GroupAccess ? "Wired" : "Not wired"),
            Row("debug-info-url-route", "Intercept URL openings", new[] { "Sample rule (default)", "Always open in the app", "Always let Octopus open" }[OctopusSampleHostIntegration.UrlMode]),
            Row("debug-info-platform", "Platform", Application.platform.ToString()),
            Row("debug-info-sample-version", "Sample version", OctopusSampleAboutView.SampleVersionText()),
            Row("debug-info-build", "Build", OctopusSampleAboutView.BuildText()),
            Row("debug-info-unity-version", "Unity version", Application.unityVersion),
            Row("debug-info-device-model", "Device model", SystemInfo.deviceModel),
            Row("debug-info-os", "OS", SystemInfo.operatingSystem)
        };
    }
    public static string Group(string id)
    {
        if (id == "debug-info-force-login" || id == "debug-info-push-registration") return "Feature toggles";
        if (id == "debug-info-server-environment" || id == "debug-info-host" || id == "debug-info-community" || id == "debug-info-api-key-source" || id == "debug-info-sso-user" || id == "debug-info-entitlements" || id == "debug-info-theme" || id == "debug-info-language") return "Configuration";
        if (id.EndsWith("-route") || id == "debug-info-profile-edit") return "Host app integration";
        if (id == "debug-info-platform" || id == "debug-info-sample-version" || id == "debug-info-build" || id == "debug-info-unity-version" || id == "debug-info-device-model" || id == "debug-info-os") return "Build";
        return "SDK";
    }
    private static OctopusSampleDeveloperToolsView.InfoFact Row(string id, string label, string value)
    { return new OctopusSampleDeveloperToolsView.InfoFact(id, label, value); }
}
