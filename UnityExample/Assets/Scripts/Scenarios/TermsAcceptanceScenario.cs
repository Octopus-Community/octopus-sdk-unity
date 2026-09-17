using System;
using System.Collections.Generic;

public sealed class TermsAcceptanceScenario : OctopusScenarioPilot
{
    private readonly OctopusScenarioFields _fields = new OctopusScenarioFields(
        new OctopusScenarioField("mode", "Terms acceptance mode", true));
    private readonly List<OctopusScenarioPreset> _presets;
    private int _generation;

    public TermsAcceptanceScenario() : base("termsAcceptance")
    {
        _presets = new List<OctopusScenarioPreset>
        {
            Preset(1, "Implicit (default / no-op)", "Implicit"),
            Preset(2, "Explicit — multi checkbox", "ExplicitMultiCheckbox"),
            Preset(3, "Explicit — single checkbox", "ExplicitSingleCheckbox"),
            Preset(4, "Clear override (backend default)", "backend"),
        };
    }
    public override OctopusScenarioFields Fields { get { return _fields; } }
    public override IReadOnlyList<OctopusScenarioPreset> Presets { get { return _presets; } }
    public override IReadOnlyList<string> ApiSymbols
    {
        get { return new[] { "DebugOverrideTermsAcceptanceMode", "DebugGetCommunityConfig" }; }
    }
    public override string ParameterNotice
    {
        get { return "Debug-only, in-memory override. Open Community and make a first contribution " +
                     "with a user who has not accepted the terms. Clearing restores backend config."; }
    }

    private OctopusScenarioPreset Preset(int index, string label, string mode)
    {
        return new OctopusScenarioPreset(PresetTestId(index), index == 4 ? label : PresetLabel(index, label),
            fields => fields.Set("mode", mode), Apply);
    }

    private void Apply(OctopusScenarioFields fields)
    {
        string reason;
        if (OctopusScenarioSdk.EnsurePilotInitialized(out reason) == null) { Report(reason); return; }
        var value = fields.Get("mode");
        var mode = value == "backend" ? (OctopusTermsAcceptanceMode?)null :
            (OctopusTermsAcceptanceMode)Enum.Parse(typeof(OctopusTermsAcceptanceMode), value);
        var generation = ++_generation;
        var applied = "Applied: " + (mode.HasValue ? value : "none (backend default)") + ". ";
        var sdk = OctopusScenarioSdk.Current;
        try
        {
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.DebugOverrideTermsAcceptanceMode", value);
            sdk.DebugOverrideTermsAcceptanceMode(mode);
            ReportRunning(applied + "Reading effective config…");
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.DebugGetCommunityConfig", "");
            sdk.DebugGetCommunityConfig(config =>
            {
                if (generation != _generation) return;
                Report(applied + (config == null ? "Effective config unavailable." :
                    "Effective TermsAcceptanceMode: " + config.TermsAcceptanceMode + ".") +
                    " Open Community and make a first contribution with a user who has not accepted the terms.");
            }, error =>
            {
                if (generation == _generation) Report(applied + "Config read failed: " + error);
            });
        }
        catch (Exception exception) { Report("Terms acceptance failed: " + exception.Message); }
    }

    public override void Dispose() { base.Dispose(); ++_generation; }
}
