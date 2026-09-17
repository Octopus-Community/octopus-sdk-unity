using System.Collections.Generic;

public sealed class TrackABTestsScenario : OctopusScenarioPilot
{
    private readonly OctopusScenarioFields _fields = new OctopusScenarioFields(
        new OctopusScenarioField("canAccessCommunity", "Can access the community (true or false)"));
    private readonly List<OctopusScenarioPreset> _presets;
    public TrackABTestsScenario() : base("trackABTests")
    {
        _fields.Set("canAccessCommunity", "true");
        _presets = new List<OctopusScenarioPreset>
        {
            new OctopusScenarioPreset(PresetTestId(1), PresetLabel(1, "Open Octopus Home Screen"),
                fields => fields.Set("canAccessCommunity",
                    string.IsNullOrEmpty(fields.Get("canAccessCommunity")) ? "true" : fields.Get("canAccessCommunity")),
                Run)
        };
    }
    public override OctopusScenarioFields Fields { get { return _fields; } }
    public override IReadOnlyList<OctopusScenarioPreset> Presets { get { return _presets; } }
    public override IReadOnlyList<string> ApiSymbols { get { return new[] { "TrackAccessToCommunity", "Open" }; } }
    public override bool CanCustomize { get { return true; } }
    public override string ParameterNotice
    {
        get { return "The host cohort starts ON and is retained by the preset. Run reports the current decision, then opens only for the granted cohort. This analytics call does not override SDK access."; }
    }
    public override void RunCustom() { Run(Fields); }
    private void Run(OctopusScenarioFields fields)
    {
        bool access;
        if (!bool.TryParse(fields.Get("canAccessCommunity"), out access))
        {
            Report("No call made: canAccessCommunity must be true or false.");
            return;
        }
        string busy;
        if (!OctopusScenarioSdk.TryBeginOperation("TrackAccessToCommunity", out busy)) { Report(busy); return; }
        var token = OctopusScenarioSdk.OperationToken;
        try
        {
            string mode;
            if (OctopusScenarioSdk.EnsureInitialized(OctopusScenarioSdk.PilotMode(), OctopusScenarioSdk.PilotModeLabel, out mode) == null)
            { Report(mode); return; }
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.TrackAccessToCommunity", "hasAccess=" + access);
            OctopusScenarioSdk.Current.TrackAccessToCommunity(access);
            if (!access)
            {
                Report("Can access the community is OFF — turn it on to simulate the access-granted cohort before opening Octopus.");
                return;
            }
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.Open");
            OctopusScenarioSdk.Current.Open();
            Report("Opening Octopus Home Screen (access-granted cohort).");
        }
        finally { OctopusScenarioSdk.EndOperation(token); }
    }
}
