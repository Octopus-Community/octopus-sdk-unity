using System;
using System.Collections.Generic;

public sealed class NotSeenNotificationsScenario : OctopusScenarioPilot
{
    private readonly OctopusScenarioFields _fields = new OctopusScenarioFields(
        new OctopusScenarioField("action", "Action", true));
    private readonly List<OctopusScenarioPreset> _presets;
    private string _outcome;
    public NotSeenNotificationsScenario() : base("notSeenNotifications")
    {
        _presets = new List<OctopusScenarioPreset>
        {
            Preset(1, "Open Octopus (full-page route)", "open"),
            Preset(2, "Refresh not-seen count", "refresh")
        };
        OctopusScenarioSdk.Observe(this);
        ObservationsChanged();
    }
    public override OctopusScenarioFields Fields { get { return _fields; } }
    public override IReadOnlyList<OctopusScenarioPreset> Presets { get { return _presets; } }
    public override IReadOnlyList<string> ApiSymbols { get { return new[] { "OnNotSeenNotificationsCount", "UpdateNotSeenNotificationsCount", "Open" }; } }
    public override string ParameterNotice
    {
        get { return "The result follows the SDK count; — means no count has arrived. Refresh runs only on a tap. Open uses the native community screen in Unity."; }
    }
    private OctopusScenarioPreset Preset(int index, string label, string action)
    {
        return new OctopusScenarioPreset(PresetTestId(index), PresetLabel(index, label),
            fields => fields.Set("action", action), fields => Run(fields.Get("action")));
    }
    private void Run(string action)
    {
        string busy;
        if (!OctopusScenarioSdk.TryBeginOperation("NotSeenNotifications", out busy)) { Report(busy); return; }
        var token = OctopusScenarioSdk.OperationToken;
        try
        {
            string mode;
            if (OctopusScenarioSdk.EnsureInitialized(OctopusScenarioSdk.PilotMode(), OctopusScenarioSdk.PilotModeLabel, out mode) == null)
            { Finish(mode); return; }
            if (action == "open")
            {
                OctopusSampleLog.Current.LogApiCall("OctopusSDK.Open");
                OctopusScenarioSdk.Current.Open();
                Finish("Opening Octopus Home Screen.");
            }
            else if (action == "refresh")
            {
                OctopusSampleLog.Current.LogApiCall("OctopusSDK.UpdateNotSeenNotificationsCount");
                OctopusScenarioSdk.Current.UpdateNotSeenNotificationsCount();
                Finish("Refresh requested. Latest notSeenNotificationsCount follows below.");
            }
            else Finish("No call made: action must be open or refresh.");
        }
        catch (Exception error) { Finish("Not-seen notifications failed: " + error.Message); }
        finally { OctopusScenarioSdk.EndOperation(token); }
    }
    private void Finish(string result) { _outcome = result; Report(result + CountLine()); }
    internal override void ObservationsChanged()
    {
        if (!IsRunning) Report((_outcome ?? "Ready — no SDK call made by this pilot yet.") + CountLine());
    }
    private static string CountLine()
    {
        return "\nnotSeenNotificationsCount: " + (OctopusScenarioSdk.ObservedNotSeenCount.HasValue ?
            OctopusScenarioSdk.ObservedNotSeenCount.Value.ToString() : "—");
    }
}
