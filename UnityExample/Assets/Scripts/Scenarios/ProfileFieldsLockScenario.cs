using System;
using System.Collections.Generic;

public sealed class ProfileFieldsLockScenario : OctopusScenarioPilot
{
    private readonly OctopusScenarioFields _fields = new OctopusScenarioFields(
        new OctopusScenarioField("nickname", "Nickname", true),
        new OctopusScenarioField("avatar", "Avatar", true),
        new OctopusScenarioField("bio", "Bio", true));
    private readonly List<OctopusScenarioPreset> _presets;

    public ProfileFieldsLockScenario() : base("profileFieldsLock")
    {
        _presets = new List<OctopusScenarioPreset>
        {
            Preset(1, "All editable (default / no-op)", "Editable", "Editable", "Editable"),
            Preset(2, "Pseudo + avatar read-only, bio hidden", "ReadOnly", "ReadOnly", "Disabled"),
            Preset(3, "Bio-only editable", "ReadOnly", "ReadOnly", "Editable"),
            Preset(4, "Clear override (backend default)", "backend", "backend", "backend"),
        };
    }
    public override OctopusScenarioFields Fields { get { return _fields; } }
    public override IReadOnlyList<OctopusScenarioPreset> Presets { get { return _presets; } }
    public override IReadOnlyList<string> ApiSymbols
    {
        get { return new[] { "DebugOverrideProfileFieldsLock" }; }
    }
    public override string ParameterNotice
    {
        get { return "Debug-only, in-memory override. Read-only fields retain their values; " +
                     "a disabled bio disappears. Clear restores the backend configuration."; }
    }

    private OctopusScenarioPreset Preset(int index, string label, string nickname, string avatar, string bio)
    {
        return new OctopusScenarioPreset(PresetTestId(index), index == 4 ? label : PresetLabel(index, label),
            fields =>
            {
                fields.Set("nickname", nickname);
                fields.Set("avatar", avatar);
                fields.Set("bio", bio);
            }, Apply);
    }

    private void Apply(OctopusScenarioFields fields)
    {
        string reason;
        if (OctopusScenarioSdk.EnsurePilotInitialized(out reason) == null) { Report(reason); return; }
        var fieldsLock = fields.Get("nickname") == "backend" ? null : new OctopusProfileFieldsLock(
            State(fields.Get("nickname")), State(fields.Get("avatar")), State(fields.Get("bio")));
        var label = fieldsLock == null ? "none (backend default)" :
            "nickname=" + fieldsLock.Nickname + "; avatar=" + fieldsLock.Avatar + "; bio=" + fieldsLock.Bio;
        try
        {
            OctopusSampleLog.Current.LogApiCall("OctopusSDK.DebugOverrideProfileFieldsLock", label);
            OctopusScenarioSdk.Current.DebugOverrideProfileFieldsLock(fieldsLock);
            Report("Applied: " + label + ". Open the Community tab, then your profile, to see the locked fields.");
        }
        catch (Exception exception) { Report("DebugOverrideProfileFieldsLock failed: " + exception.Message); }
    }

    private static OctopusProfileFieldLockState State(string value)
    {
        return (OctopusProfileFieldLockState)Enum.Parse(typeof(OctopusProfileFieldLockState), value);
    }
}
