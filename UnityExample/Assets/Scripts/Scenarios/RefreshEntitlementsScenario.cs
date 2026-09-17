using System;
using System.Collections.Generic;

public sealed class RefreshEntitlementsScenario : OctopusScenarioPilot
{
    private readonly OctopusScenarioFields _fields = new OctopusScenarioFields(
        new OctopusScenarioField("action", "Action", true));
    private readonly List<OctopusScenarioPreset> _presets;
    private IOctopusScenarioSdk _observedSdk;
    private int _generation;
    private string _status = "Ready";
    private string _entitlements = "— (no profile)";

    public RefreshEntitlementsScenario() : base("refreshEntitlements")
    {
        _presets = new List<OctopusScenarioPreset>
        {
            new OctopusScenarioPreset(PresetTestId(1), PresetLabel(1, "Refresh entitlements"),
                fields => fields.Set("action", "refresh"), fields => Refresh())
        };
    }
    public override OctopusScenarioFields Fields { get { return _fields; } }
    public override IReadOnlyList<OctopusScenarioPreset> Presets { get { return _presets; } }
    public override IReadOnlyList<string> ApiSymbols
    {
        get { return new[] { "RefreshEntitlements", "CurrentProfile", "OnProfileChanged" }; }
    }
    public override string ParameterNotice
    {
        get { return "Refresh completion and profile updates arrive separately. The configured SSO " +
                     "token provider determines the claims; this action does not grant entitlements."; }
    }

    private void Refresh()
    {
        string reason;
        if (OctopusScenarioSdk.EnsurePilotInitialized(out reason) == null) { Report(reason); return; }
        var sdk = OctopusScenarioSdk.Current;
        if (_observedSdk != sdk)
        {
            Dispose();
            _observedSdk = sdk;
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.OnProfileChanged", "subscribe");
            sdk.ProfileChanged += ProfileChanged;
        }
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.CurrentProfile", "read snapshot");
        _entitlements = Describe(sdk.CurrentProfile);
        var generation = ++_generation;
        _status = "Refreshing entitlements…";
        ReportRunning(Line());
        try
        {
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.RefreshEntitlements", "");
            sdk.RefreshEntitlements(() =>
            {
                if (generation != _generation) return;
                _status = "RefreshEntitlements succeeded — updated entitlements arrive through the profile stream.";
                Report(Line());
            }, error =>
            {
                if (generation != _generation) return;
                _status = "RefreshEntitlements failed: " + error.Kind + ": " + error.Message;
                Report(Line());
            });
        }
        catch (Exception exception)
        {
            _status = "RefreshEntitlements threw: " + exception.Message;
            Report(Line());
        }
    }

    private void ProfileChanged(OctopusProfile profile)
    {
        _entitlements = Describe(profile);
        if (IsRunning) ReportRunning(Line());
        else Report(Line());
    }

    private string Line() { return _status + "\nEntitlements: " + _entitlements; }
    private static string Describe(OctopusProfile profile)
    {
        if (profile == null) return "— (no profile)";
        if (profile.Entitlements.Count == 0) return "— (empty set)";
        var values = new List<string>(profile.Entitlements);
        values.Sort(StringComparer.Ordinal);
        return string.Join(", ", values.ToArray());
    }

    public override void Dispose()
    {
        base.Dispose();
        ++_generation;
        var sdk = _observedSdk;
        _observedSdk = null;
        if (sdk == null) return;
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.OnProfileChanged", "unsubscribe");
        sdk.ProfileChanged -= ProfileChanged;
    }
}
