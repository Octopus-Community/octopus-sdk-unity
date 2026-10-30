using System.Collections.Generic;

/// <summary>Formats the sample record of SDK calls and key-free configuration facts.</summary>
public static class OctopusSampleStatus
{
    public const string ConnectionScenarioId = "connection";
    public const string ServerEnvironmentLabel = "Server environment";

    public static string SdkStatusLabel()
    {
        return OctopusSampleState.IsInitialized ? "READY" : "Not initialized";
    }

    public static string ConnectionStatusLabel()
    {
        switch (OctopusSampleState.ConnectionSession)
        {
            case OctopusSampleState.Session.ConnectCompleted: return "CONNECT OK";
            case OctopusSampleState.Session.Disconnected: return "DISCONNECT OK";
            case OctopusSampleState.Session.Failed: return "CALL FAILED";
            case OctopusSampleState.Session.StartupFailed: return "STARTUP FAILED";
            default: return "NO CALL";
        }
    }

    internal static string ConnectionDetail()
    {
        var session = OctopusSampleState.ConnectionSession;
        var detail = OctopusSampleState.SessionDetail;

        if (session == OctopusSampleState.Session.None || string.IsNullOrEmpty(detail))
        {
            return "No connection call yet. The Connection scenario, linked below, makes one.";
        }

        if (session == OctopusSampleState.Session.StartupFailed)
        {
            // Not a connection call at all: the saved profile could not be replayed at launch.
            return detail + " — no connection call was made. Choose a profile in Configuration " +
                   "to start the SDK again.";
        }

        if (session == OctopusSampleState.Session.Failed)
        {
            // Emphatically not "the call returned": it threw. And a disconnect that throws leaves
            // whatever session existed before it untouched, which is the case a reader is most
            // likely to misread.
            return detail + " — the call did not complete, so it changed nothing the sample can " +
                   "vouch for.";
        }

        return detail + " — that is the call completing, not a session: this package publishes no " +
               "connection state, so whether the SDK accepted it cannot be read back here.";
    }

    public static List<KeyValuePair<string, string>> ConfigurationLines()
    {
        var profile = OctopusScenarioSdk.Current.Profile;
        var missing = profile == null;
        var lines = new List<KeyValuePair<string, string>>();

        // The pilots always pass a host (the demo backend unless the asset overrides it): the one
        // the running SDK was initialised or last switched onto, else the one Initialize will get.
        lines.Add(Line(ServerEnvironmentLabel, OctopusScenarioSdk.RunningServerHost ??
            OctopusScenarioSdk.ServerHostFor(missing ? null : profile)));
        // Never the key itself, and never a client name: the community is identified by the key,
        // which is a secret the mirror-export guard scans for. Its source is what a reader can act
        // on.
        lines.Add(Line("Community", missing
            ? "Unknown — no API key configured"
            : "The community behind the configured API key"));
        lines.Add(Line("API key source", missing
            ? "Missing — create it via Assets > Create > Octopus Example Config"
            : "OctopusExampleConfig (git-ignored asset)"));
        lines.Add(Line("SSO user", missing ? "—" : Describe(profile)));
        lines.Add(Line("Entitlements", "Carried in the SSO token; presets set claims when demo signing is configured"));
        // "Sample theme": the sample's own appearance (Settings › Appearance), not the SDK theme
        // the Theme scenario applies to the community screens.
        lines.Add(Line("Sample theme", OctopusSampleBranding.Theme.ToString()));
        lines.Add(Line("Language", string.IsNullOrEmpty(OctopusSampleState.LocaleOverride)
            ? "System default (no override)"
            : OctopusSampleState.LocaleOverride));
        return lines;
    }

    private static string Describe(OctopusExampleConfig.ExampleProfile profile)
    {
        if (string.IsNullOrEmpty(profile.userId)) return "Configured, no user id";
        return string.IsNullOrEmpty(profile.nickname)
            ? profile.userId
            : profile.nickname + " · " + profile.userId;
    }

    private static KeyValuePair<string, string> Line(string label, string value)
    {
        return new KeyValuePair<string, string>(label, value);
    }
}
