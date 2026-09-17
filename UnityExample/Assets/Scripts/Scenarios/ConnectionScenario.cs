using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// The `connection` scenario: `connectUser` / `disconnectUser` over SSO.
///
/// The SDK calls are the ones `SSOExample` has always made — `Initialize(apiKey, ConnectionMode.SSO())`,
/// then `ConnectUser(userId, nickname, bio, picture, tokenProvider)` or `DisconnectUser()`. What
/// changes here is the driving surface: five single-tap presets carrying the catalogue's ids
/// instead of one toggling button, and a live result panel instead of the button's own caption.
///
/// Each connect preset signs a demo SSO token with its requested entitlements using the config's
/// local ssoTokenSecret. Without a secret the static authToken is used unchanged; the result
/// explains that its claims cannot be changed by a preset. Neither credential is displayed or logged.
/// </summary>
public sealed class ConnectionScenario : OctopusScenarioPilot
{
    private const string ProfileKey = "profile";
    private const string EntitlementsKey = "entitlements";
    private const string ActionKey = "action";

    // Which of the config asset's profiles the next initialisation will use — the `Force login`
    // switch in the Sign-in section header picks it, so the field has to be read at preset time
    // rather than baked into a const, or the screen would keep naming a profile the tester moved
    // away from.
    private static string SampleProfile()
    {
        return OctopusSampleFeatureToggles.ForceLogin
            ? "Forced login (OctopusExampleConfig)"
            : "Default (OctopusExampleConfig)";
    }

    private readonly OctopusScenarioFields _fields = new OctopusScenarioFields(
        new OctopusScenarioField(ProfileKey, "SSO profile", true),
        new OctopusScenarioField(EntitlementsKey, "Requested entitlements", true),
        new OctopusScenarioField(ActionKey, "Action (connect or disconnect)"));

    private readonly List<OctopusScenarioPreset> _presets;

    public ConnectionScenario() : base("connection")
    {
        _presets = new List<OctopusScenarioPreset>
        {
            // Labels 1 and 2 are the catalogue's `labels.flutter`. The catalogue gives those two
            // presets a per-platform label and carries no `unity` key; Flutter's is the wording
            // that makes the five-preset set coherent (RN builds a two-preset set, where preset 2
            // is "Disconnect" — taking that here would leave presets 2 and 5 both disconnecting).
            Connect(1, "Connect (no entitlements)", "none"),
            Connect(2, "Connect as Premium", OctopusSampleFixtures.Premium),
            Connect(3, "Connect as Moderator", OctopusSampleFixtures.Moderator),
            Connect(4, "Connect as Premium + Moderator", OctopusSampleFixtures.Premium + ", " + OctopusSampleFixtures.Moderator),
            new OctopusScenarioPreset(
                PresetTestId(5),
                PresetLabel(5, "Disconnect"),
                fields =>
                {
                    fields.Set(ProfileKey, SampleProfile());
                    fields.Set(EntitlementsKey, "n/a (disconnect)");
                    fields.Set(ActionKey, "disconnect");
                },
                fields => Disconnect()),
        };
    }

    public override OctopusScenarioFields Fields { get { return _fields; } }

    public override IReadOnlyList<OctopusScenarioPreset> Presets { get { return _presets; } }

    public override string Capability { get { return "Connect a sample user and inspect the call result."; } }
    public override IReadOnlyList<string> ApiSymbols
    {
        get { return new[] { "ConnectUser", "DisconnectUser" }; }
    }
    public override string ParameterNotice
    {
        get { return "A local demo signing secret adds the requested entitlements to the SSO token. " +
                     "With only a static authToken, its existing claims determine access."; }
    }
    public override bool CanCustomize { get { return true; } }

    public override void RunCustom()
    {
        var action = (Fields.Get(ActionKey) ?? string.Empty).Trim();
        if (string.Equals(action, "connect", System.StringComparison.OrdinalIgnoreCase))
            ConnectAsync(Fields.Get(EntitlementsKey));
        else if (string.Equals(action, "disconnect", System.StringComparison.OrdinalIgnoreCase))
            Disconnect();
        else
            Report("No call made: action must be connect or disconnect.");
    }

    private OctopusScenarioPreset Connect(int index, string description, string entitlements)
    {
        return new OctopusScenarioPreset(
            PresetTestId(index),
            PresetLabel(index, description),
            fields =>
            {
                fields.Set(ProfileKey, SampleProfile());
                fields.Set(EntitlementsKey, entitlements);
                fields.Set(ActionKey, "connect");
            },
            fields => ConnectAsync(fields.Get(EntitlementsKey)));
    }

    private async void ConnectAsync(string entitlements)
    {
        string mode;
        var profile = OctopusScenarioSdk.EnsureInitialized(
            OctopusScenarioSdk.PilotMode(), OctopusScenarioSdk.PilotModeLabel, out mode);
        if (profile == null)
        {
            Report(mode);
            return;
        }

        if (!OctopusScenarioSdk.IsInPilotMode)
        {
            // Never reach the native SDK here: on iOS, ConnectUser on an Octopus-auth
            // initialisation is a preconditionFailure, not a reportable error.
            Report("ConnectUser NOT called: the SDK is initialised with " + mode +
                   ". Restart the app and open this screen first.");
            return;
        }

        string refusal;
        if (!OctopusSampleTokenProvider.CanConnect(profile, out refusal))
        {
            Report(refusal);
            OctopusSampleState.ReportSession(OctopusSampleState.Session.Failed, refusal);
            OctopusSampleLog.Current.LogStateChange("[OctopusQA] scenario=connection state=refused", refusal);
            return;
        }
        var requested = ParseEntitlements(entitlements);
        var provider = new OctopusSampleTokenProvider(profile);

        string busy;
        if (!OctopusScenarioSdk.TryBeginOperation("ConnectUser", out busy))
        {
            Report(busy);
            return;
        }
        var token = OctopusScenarioSdk.OperationToken;

        var caveat = requested.Length == 0 || !string.IsNullOrWhiteSpace(profile.signingSecret)
            ? string.Empty
            : " Requested entitlements '" + entitlements + "' were NOT sent: the static authToken " +
              "is used unchanged. Set the config's ssoTokenSecret to sign per-preset claims.";

        // Everything after the slot is taken runs inside the try: an exception anywhere here
        // (a throwing logger included) must release the slot, or every later tap is refused.
        try
        {
            ReportRunning("Connecting as '" + profile.userId + "' (mode: " + mode + ")…" + caveat);
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.ConnectUser", "userId=" + profile.userId);
            await OctopusScenarioSdk.Current.ConnectUser(profile.userId, profile.nickname, profile.bio,
                                                        profile.picture, () => Task.FromResult(provider.GetToken(profile.userId, requested)));
            // Completion is not success: the native bridges signal the end of the call whether
            // the token was accepted or not, and this package exposes no connection state to
            // read back. Say what is actually known.
            Report("ConnectUser call completed for '" + profile.userId + "' (mode: " + mode +
                   "). This confirms the call returned, not that a session exists — check the " +
                   "Community tab for the real state." +
                   caveat);
            OctopusSampleState.ReportSession(OctopusSampleState.Session.ConnectCompleted,
                                             "Connect call completed for '" + profile.userId + "'");
        }
        catch (System.Exception e)
        {
            Report("ConnectUser failed: " + e.Message);
            OctopusSampleState.ReportSession(OctopusSampleState.Session.Failed,
                                             "ConnectUser failed: " + e.Message);
        }
        finally
        {
            OctopusScenarioSdk.EndOperation(token);
        }
    }

    private async void Disconnect()
    {
        string mode;
        var profile = OctopusScenarioSdk.EnsureInitialized(
            OctopusScenarioSdk.PilotMode(), OctopusScenarioSdk.PilotModeLabel, out mode);
        if (profile == null)
        {
            Report(mode);
            return;
        }

        if (!OctopusScenarioSdk.IsInPilotMode)
        {
            Report("DisconnectUser NOT called: the SDK is initialised with " + mode +
                   ". Restart the app and open this screen first.");
            return;
        }

        string busy;
        if (!OctopusScenarioSdk.TryBeginOperation("DisconnectUser", out busy))
        {
            Report(busy);
            return;
        }
        var token = OctopusScenarioSdk.OperationToken;

        try
        {
            ReportRunning("Disconnecting…");
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.DisconnectUser");
            await OctopusScenarioSdk.Current.DisconnectUser();
            Report("DisconnectUser call completed (mode: " + mode + ").");
            OctopusSampleState.ReportSession(OctopusSampleState.Session.Disconnected,
                                             "Disconnect call completed");
        }
        catch (System.Exception e)
        {
            Report("DisconnectUser failed: " + e.Message);
            OctopusSampleState.ReportSession(OctopusSampleState.Session.Failed,
                                             "DisconnectUser failed: " + e.Message);
        }
        finally
        {
            OctopusScenarioSdk.EndOperation(token);
        }
    }

    private static string[] ParseEntitlements(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "none") return new string[0];
        var result = new List<string>();
        foreach (var entitlement in value.Split(','))
            if (!string.IsNullOrWhiteSpace(entitlement)) result.Add(entitlement.Trim());
        return result.ToArray();
    }
}
