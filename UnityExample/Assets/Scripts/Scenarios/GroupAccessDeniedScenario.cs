using System;
using System.Collections.Generic;

public sealed class GroupAccessDeniedScenario : CommunityScenarioPilot
{
    private Action _unregister;
    private int _fireCount;
    private string _lastGroupId = "—";

    public GroupAccessDeniedScenario() : base("groupAccessDenied",
        new OctopusScenarioField("action", "Action (register or unregister)"))
    {
        Add(1, PresetLabel(1, "Register callback"), "register");
        Add(2, PresetLabel(2, "Unregister callback"), "unregister");
    }

    public override IReadOnlyList<string> ApiSymbols { get { return new[] { "OnGroupAccessDenied" }; } }
    public override string ParameterNotice
    { get { return "Tap Register before opening a locked group in Community. Unregister disarms this pilot; it never opens a paywall or navigates for you."; } }

    protected override void Run(OctopusScenarioFields fields)
    {
        var action = fields.Get("action").Trim().ToLowerInvariant();
        if (action == "register")
        {
            if (_unregister != null)
            { Show("Callback already registered — no-op (still armed)."); return; }
            var sdk = OctopusScenarioSdk.Current;
            Announce("OnGroupAccessDenied +=");
            _unregister = Observe<string>(h => sdk.GroupAccessDenied += h,
                h => { Announce("OnGroupAccessDenied -="); sdk.GroupAccessDenied -= h; },
                (owner, id) => ((GroupAccessDeniedScenario)owner).Received(id));
            Show("Callback registered. It will fire when the user taps a locked group in the Community tab.");
        }
        else if (action == "unregister")
        {
            if (_unregister == null)
            { Show("Callback was already unregistered — no-op."); return; }
            _unregister();
            _unregister = null;
            Show("Callback unregistered. The SDK will no longer notify this scenario when a locked group is tapped.");
        }
        else Report("No call made: action must be register or unregister.");
    }

    private void Received(string id)
    {
        _lastGroupId = id;
        _fireCount++;
        Show("Group access denied callback fired.");
    }

    private void Show(string message)
    {
        Report(message + "\nRegistration: " + (_unregister == null ? "unregistered" : "registered") +
               "\nLast groupId: " + _lastGroupId + "\nFire count: " + _fireCount);
    }
}
