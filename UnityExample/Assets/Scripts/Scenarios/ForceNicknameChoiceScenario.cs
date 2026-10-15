using System.Collections.Generic;

public sealed class ForceNicknameChoiceScenario : CommunityScenarioPilot
{
    private int _generation;

    public ForceNicknameChoiceScenario() : base("forceNicknameChoice",
        new OctopusScenarioField("enabled", "Force nickname choice (true, false or backend)"))
    {
        Add(1, "On — the member types a nickname", "true");
        Add(2, "Off — the generated nickname is prefilled", "false");
        Add(3, "Clear override (backend default)", "backend");
    }

    public override IReadOnlyList<string> ApiSymbols
    { get { return new[] { "DebugOverrideForceNicknameChoice", "DebugGetCommunityConfig" }; } }
    public override string ParameterNotice
    { get { return "Use a fresh guest or an SSO member with an unconfirmed, editable nickname. Unlock nickname with Profile Field Lock preset 1. Open Community and make a first contribution. The preset changes whether the native field starts empty or prefilled."; } }

    protected override void Run(OctopusScenarioFields fields)
    {
        var value = fields.Get("enabled").Trim().ToLowerInvariant();
        bool parsed;
        if (value != "backend" && !bool.TryParse(value, out parsed))
        { Report("No call made: enabled must be true, false or backend."); return; }
        bool? enabled = value == "backend" ? (bool?)null : bool.Parse(value);
        var sdk = OctopusScenarioSdk.Current;
        var generation = ++_generation;
        Announce("DebugOverrideForceNicknameChoice", value);
        sdk.DebugOverrideForceNicknameChoice(enabled);
        var applied = "Applied: " + value + ". ";
        ReportRunning(applied + "Reading effective config…");
        Announce("DebugGetCommunityConfig");
        sdk.DebugGetCommunityConfig(config =>
        {
            if (generation != _generation) return;
            Report(applied + (config == null ? "Effective config unavailable." :
                "Effective ForceNicknameChoice: " + config.ForceNicknameChoice + ".") +
                " Open Community and make a first contribution with an unconfirmed, editable nickname.");
        }, error =>
        {
            if (generation == _generation) Report(applied + "Config read failed: " + error);
        });
    }

    public override void Dispose() { base.Dispose(); ++_generation; }
}
