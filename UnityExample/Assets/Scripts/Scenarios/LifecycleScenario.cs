using System;
using System.Collections.Generic;

public sealed class LifecycleScenario : OctopusScenarioPilot
{
    private int _operationToken;
    private readonly OctopusScenarioFields _fields = new OctopusScenarioFields(
        new OctopusScenarioField("action", "Action", true),
        new OctopusScenarioField("profile", "Target profile", true));
    private readonly List<OctopusScenarioPreset> _presets;
    public LifecycleScenario() : base("lifecycle")
    {
        _presets = new List<OctopusScenarioPreset>
        {
            Preset(1, "switchCommunity (alt key)", "switchCommunity", "Alternate (OctopusExampleConfig)"),
            Preset(2, "reset", "reset", "Current community"),
            Preset(3, "stop", "stop", "Current community")
        };
    }
    public override OctopusScenarioFields Fields { get { return _fields; } }
    public override IReadOnlyList<OctopusScenarioPreset> Presets { get { return _presets; } }
    public override IReadOnlyList<string> ApiSymbols { get { return new[] { "SwitchCommunity", "Reset", "Stop" }; } }
    public override string ParameterNotice
    {
        get { return "Alternate uses the other configured sample profile. Keys are never displayed. Unity has no isInitialised stream (#108); results describe completed calls only. Reset keeps initialization; Stop clears the sample's initialization record."; }
    }
    private OctopusScenarioPreset Preset(int index, string label, string action, string profile)
    {
        return new OctopusScenarioPreset(PresetTestId(index), PresetLabel(index, label),
            fields => { fields.Set("action", action); fields.Set("profile", profile); },
            fields => Run(fields.Get("action")));
    }
    private void Run(string action)
    {
        if (action != "switchCommunity" && action != "reset" && action != "stop")
        { Report("No call made: unknown lifecycle action."); return; }
        if (!OctopusSampleState.IsInitialized)
        { Report("No call made: the SDK is not initialised. Run a connection preset first."); return; }
        string busy;
        if (!OctopusScenarioSdk.TryBeginOperation("Lifecycle." + action, out busy)) { Report(busy); return; }
        _operationToken = OctopusScenarioSdk.OperationToken;
        try
        {
            var sdk = OctopusScenarioSdk.Current;
            if (action == "switchCommunity")
            {
                var target = sdk.AlternateProfile;
                if (target == null || string.IsNullOrWhiteSpace(target.apiKey))
                { Finish("No call made: alternate OctopusExampleConfig profile is missing or has no API key."); return; }
                string reason;
                var current = OctopusScenarioSdk.UsableProfile(out reason);
                if (current != null && current.apiKey == target.apiKey)
                { Finish("No call made: alternate profile must use a different sample community."); return; }
                OctopusScenarioSdk.EnsurePilotObservations();
                OctopusSampleState.EnsureObserving();
                ReportRunning("Switching to the alternate sample profile…");
                OctopusSampleLog.Current.LogApiCall("OctopusSDK.SwitchCommunity", "profile=alternate, mode=SSO");
                OctopusScenarioSdk.ClearCommunityObservations();
                sdk.SwitchCommunity(target.apiKey, OctopusScenarioSdk.PilotMode(),
                    () =>
                    {
                        try
                        {
                            OctopusScenarioSdk.CommunitySwitched(target);
                            Finish("switchCommunity completed. Reconnect the user. No isInitialised stream is available in Unity.");
                        }
                        catch (Exception) { Finish("Community switched, but sample setup failed."); }
                    }, error => Failed(action));
            }
            else
            {
                ReportRunning(action + " running…");
                Action completed = () =>
                {
                    if (action == "stop") OctopusScenarioSdk.LifecycleStopped();
                    else OctopusSampleState.ResetObservations();
                    Finish(action + " completed. " + (action == "stop" ? "Sample initialization record cleared." : "SDK remains on the same community.") +
                        " No isInitialised stream is available in Unity.");
                };
                OctopusSampleLog.Current.LogApiCall(action == "stop" ? "OctopusSDK.Stop" : "OctopusSDK.Reset");
                OctopusScenarioSdk.ClearCommunityObservations();
                if (action == "stop") sdk.Stop(completed, error => Failed(action));
                else sdk.Reset(completed, error => Failed(action));
            }
        }
        catch (Exception) { Failed(action); }
    }
    private void Failed(string action)
    {
        // Switching can fail after the old community has been torn down. Never claim rollback,
        // and never echo a bridge exception that might include the configured credential.
        if (action == "switchCommunity" || action == "stop") OctopusScenarioSdk.LifecycleStopped();
        Finish(action + " failed. Native state is not confirmed; no initialization stream is available.");
    }
    private void Finish(string result)
    {
        OctopusScenarioSdk.EndOperation(_operationToken);
        Report(result);
    }
}
