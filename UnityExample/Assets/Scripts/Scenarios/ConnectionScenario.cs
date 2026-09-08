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
/// **The entitlement presets are honest about a gap.** The catalogue's presets 2 to 4 ask for a
/// connection carrying entitlements (premium, moderator, both). Entitlements travel in the SSO
/// token issued by the host's backend, and this package's `ConnectUser` takes no entitlements
/// parameter and no per-preset token — the sample has exactly one `authToken` in its config asset.
/// So those three presets pre-fill the entitlements they ask for, make the same SDK call as preset
/// 1, and say so on screen rather than pretending. Closing that gap is an SDK question (a way to
/// pass a token per connection), not a sample one.
///
/// The token itself is read from the gitignored `OctopusExampleConfig` asset at tap time and is
/// never displayed, never logged and never put in a field.
/// </summary>
public sealed class ConnectionScenario : OctopusScenarioPilot
{
    private const string ProfileKey = "profile";
    private const string EntitlementsKey = "entitlements";
    private const string ActionKey = "action";

    private const string SampleProfile = "Default (OctopusExampleConfig)";

    private readonly OctopusScenarioFields _fields = new OctopusScenarioFields(
        new OctopusScenarioField(ProfileKey, "SSO profile"),
        new OctopusScenarioField(EntitlementsKey, "Requested entitlements"),
        new OctopusScenarioField(ActionKey, "Action"));

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
            Connect(2, "Connect as Premium", "premium"),
            Connect(3, "Connect as Moderator", "moderator"),
            Connect(4, "Connect as Premium + Moderator", "premium, moderator"),
            new OctopusScenarioPreset(
                PresetTestId(5),
                PresetLabel(5, "Disconnect"),
                fields =>
                {
                    fields.Set(ProfileKey, SampleProfile);
                    fields.Set(EntitlementsKey, "n/a (disconnect)");
                    fields.Set(ActionKey, "disconnect");
                },
                fields => Disconnect()),
        };
    }

    public override OctopusScenarioFields Fields { get { return _fields; } }

    public override IReadOnlyList<OctopusScenarioPreset> Presets { get { return _presets; } }

    private OctopusScenarioPreset Connect(int index, string description, string entitlements)
    {
        return new OctopusScenarioPreset(
            PresetTestId(index),
            PresetLabel(index, description),
            fields =>
            {
                fields.Set(ProfileKey, SampleProfile);
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

        string busy;
        if (!OctopusScenarioSdk.TryBeginOperation("ConnectUser", out busy))
        {
            Report(busy);
            return;
        }

        var caveat = entitlements == "none"
            ? string.Empty
            : "  Requested entitlements '" + entitlements + "' were NOT sent: this package's " +
              "ConnectUser takes no entitlements parameter and the sample holds a single SSO " +
              "token, so this call is identical to preset 1.";

        // Everything after the slot is taken runs inside the try: an exception anywhere here
        // (a throwing logger included) must release the slot, or every later tap is refused.
        try
        {
            Report("Connecting as '" + profile.userId + "' (mode: " + mode + ")…" + caveat);
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.ConnectUser", "userId=" + profile.userId);
            await OctopusSDK.ConnectUser(profile.userId, profile.nickname, profile.bio,
                                         profile.picture, () => Token(profile));
            // Completion is not success: the native bridges signal the end of the call whether
            // the token was accepted or not, and this package exposes no connection state to
            // read back. Say what is actually known.
            Report("ConnectUser call completed for '" + profile.userId + "' (mode: " + mode +
                   "). This confirms the call returned, not that a session exists — check the " +
                   "Debug console, or open the community from a demo scene, for the real state." +
                   caveat);
        }
        catch (System.Exception e)
        {
            Report("ConnectUser failed: " + e.Message);
        }
        finally
        {
            OctopusScenarioSdk.EndOperation();
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

        try
        {
            Report("Disconnecting…");
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.DisconnectUser");
            await OctopusSDK.DisconnectUser();
            Report("DisconnectUser call completed (mode: " + mode + ").");
        }
        catch (System.Exception e)
        {
            Report("DisconnectUser failed: " + e.Message);
        }
        finally
        {
            OctopusScenarioSdk.EndOperation();
        }
    }

    // The SSO token provider the SDK calls back into. Kept out of every field and every log line:
    // it is a JWT read from the gitignored config asset, and the mirror-export guard scans for
    // exactly that shape.
    private static Task<string> Token(OctopusExampleConfig.ExampleProfile profile)
    {
        return Task.FromResult(profile.authToken);
    }
}
