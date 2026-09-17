using System;
using System.Collections.Generic;

public sealed class ForceOctopusABTestsScenario : OctopusScenarioPilot
{
    private readonly OctopusScenarioFields _fields = new OctopusScenarioFields(
        new OctopusScenarioField("hasAccess", "Override (true, false or current)", true));
    private readonly List<OctopusScenarioPreset> _presets;
    private string _outcome;
    private int _operationToken;
    public ForceOctopusABTestsScenario() : base("forceOctopusABTests")
    {
        _presets = new List<OctopusScenarioPreset>
        {
            Preset(1, "Force cohort · grant community access", "true"),
            Preset(2, "Force cohort · deny community access", "false"),
            Preset(3, "Re-apply current SDK value", "current")
        };
        OctopusScenarioSdk.Observe(this);
        ObservationsChanged();
    }
    public override OctopusScenarioFields Fields { get { return _fields; } }
    public override IReadOnlyList<OctopusScenarioPreset> Presets { get { return _presets; } }
    public override IReadOnlyList<string> ApiSymbols { get { return new[] { "OverrideCommunityAccess", "HasAccessToCommunity", "OnHasAccessToCommunityChanged" }; } }
    public override string ParameterNotice
    {
        get { return "Overrides the current user's SDK cohort permanently. Unity reports string errors; typed Flutter error branches are unavailable. Current means the last value published by the SDK; an unknown value is never treated as false."; }
    }
    private OctopusScenarioPreset Preset(int index, string label, string value)
    {
        return new OctopusScenarioPreset(PresetTestId(index), PresetLabel(index, label),
            fields => fields.Set("hasAccess", value), fields => Run(fields.Get("hasAccess")));
    }
    private void Run(string value)
    {
        string busy;
        if (!OctopusScenarioSdk.TryBeginOperation("OverrideCommunityAccess", out busy)) { Report(busy); return; }
        _operationToken = OctopusScenarioSdk.OperationToken;
        try
        {
            string mode;
            if (OctopusScenarioSdk.EnsureInitialized(OctopusScenarioSdk.PilotMode(), OctopusScenarioSdk.PilotModeLabel, out mode) == null)
            { Finish(mode); return; }
            bool access;
            if (value == "current")
            {
                if (!OctopusScenarioSdk.ObservedAccess.HasValue)
                { Finish("hasAccessToCommunity is not yet known — wait for the SDK value, then retry this preset."); return; }
                access = OctopusScenarioSdk.ObservedAccess.Value;
            }
            else if (!bool.TryParse(value, out access)) { Finish("No call made: hasAccess must be true, false or current."); return; }
            ReportRunning("Overriding community access…");
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.OverrideCommunityAccess", "hasAccess=" + access);
            OctopusScenarioSdk.Current.OverrideCommunityAccess(access,
                () => Finish("forceOctopusABTest(hasAccess=" + access.ToString().ToLowerInvariant() + ") → success. The cohort attribution has been overridden for the current user."),
                error => Finish("OverrideCommunityAccess failed (string error): " + error));
        }
        catch (Exception error) { Finish("OverrideCommunityAccess failed: " + error.Message); }
    }
    private void Finish(string outcome)
    {
        OctopusScenarioSdk.EndOperation(_operationToken);
        _outcome = outcome;
        Report(outcome + AccessLine());
    }
    internal override void ObservationsChanged()
    {
        if (!IsRunning) Report((_outcome ?? "Ready — no override made yet.") + AccessLine());
    }
    private static string AccessLine()
    {
        return "\nhasAccessToCommunity: " + (OctopusScenarioSdk.ObservedAccess.HasValue ?
            OctopusScenarioSdk.ObservedAccess.Value.ToString().ToLowerInvariant() : "—");
    }
}
