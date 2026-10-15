using System.Collections.Generic;

/// <summary>The dedicated profile-routing scenario uses the existing sample override facade.</summary>
public sealed class UnifiedProfileScenario : OctopusScenarioPilot
{
    private readonly OctopusScenarioFields _fields = new OctopusScenarioFields();
    public UnifiedProfileScenario() : base("unifiedProfile") { }
    public override OctopusScenarioFields Fields { get { return _fields; } }
    public override IReadOnlyList<OctopusScenarioPreset> Presets { get { return new OctopusScenarioPreset[0]; } }
    public override IReadOnlyList<string> ApiSymbols
    {
        get { return new[] { "DebugOverrideExposeClientUserId", "NavigateToProfileHandler" }; }
    }
}
