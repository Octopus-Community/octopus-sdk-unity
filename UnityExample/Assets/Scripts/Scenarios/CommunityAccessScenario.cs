using System.Collections.Generic;

public sealed class CommunityAccessScenario : CommunityScenarioPilot
{
    private bool _observing;
    private bool _access;
    private string _outcome = "Observing community access.";

    public CommunityAccessScenario() : base("communityAccess",
        new OctopusScenarioField("action", "Action (override or track)"),
        new OctopusScenarioField("hasAccess", "Has access (true or false)"))
    {
        Add(1, PresetLabel(1, "Override · grant access"), "override", "true");
        Add(2, PresetLabel(2, "Override · deny access"), "override", "false");
        Add(3, PresetLabel(3, "Track · has access"), "track", "true");
    }

    public override IReadOnlyList<string> ApiSymbols
    { get { return new[] { "OverrideCommunityAccess", "TrackAccessToCommunity", "HasAccessToCommunity", "OnHasAccessToCommunityChanged" }; } }
    public override string ParameterNotice
    { get { return "Override controls access. Track is analytics only and does not grant access."; } }

    protected override void Run(OctopusScenarioFields fields)
    {
        var action = fields.Get("action").Trim().ToLowerInvariant();
        bool access;
        if ((action != "override" && action != "track") || !bool.TryParse(fields.Get("hasAccess"), out access))
        { Report("No call made: use override or track with hasAccess true or false."); return; }
        var sdk = OctopusScenarioSdk.Current;
        EnsureObserving(sdk);
        Announce("HasAccessToCommunity.get");
        _access = sdk.HasAccessToCommunity;
        if (action == "track")
        {
            Announce("TrackAccessToCommunity", "hasAccess=" + access);
            sdk.TrackAccessToCommunity(access);
            Finish("TrackAccessToCommunity(" + access + ") sent — analytics only; access was not overridden.");
            return;
        }
        ReportRunning("OverrideCommunityAccess(" + access + ")…");
        Announce("OverrideCommunityAccess", "hasAccess=" + access);
        sdk.OverrideCommunityAccess(access,
            () => Finish("OverrideCommunityAccess(" + access + ") → success."),
            error => Finish("OverrideCommunityAccess failed: " + error));
    }

    private void EnsureObserving(IOctopusScenarioSdk sdk)
    {
        if (!_observing)
        {
            Announce("OnHasAccessToCommunityChanged +=");
            Observe<bool>(h => sdk.OnHasAccessToCommunityChanged += h, h => { Announce("OnHasAccessToCommunityChanged -="); sdk.OnHasAccessToCommunityChanged -= h; },
                (owner, value) => ((CommunityAccessScenario)owner).Changed(value));
            _observing = true;
        }
    }

    private void Changed(bool access)
    {
        _access = access;
        if (IsRunning) ReportRunning(Line()); else Report(Line());
    }

    private void Finish(string outcome) { _outcome = outcome; Report(Line()); }
    private string Line() { return _outcome + "\nhasAccessToCommunity: " + _access; }
}
