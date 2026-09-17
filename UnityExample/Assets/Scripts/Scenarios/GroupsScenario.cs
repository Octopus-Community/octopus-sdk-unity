using System.Collections.Generic;
using System.Text;

public sealed class GroupsScenario : CommunityScenarioPilot
{
    private bool _observing;

    public GroupsScenario() : base("groups",
        new OctopusScenarioField("action", "Action (fetch, follow, unfollow)"),
        new OctopusScenarioField("groupId", "Group id (for follow / unfollow)"))
    {
        Add(1, PresetLabel(1, "Fetch groups"), "fetch", "none");
    }

    public override IReadOnlyList<string> ApiSymbols
    { get { return new[] { "FetchGroups", "FollowGroup", "UnfollowGroup", "OnGroupsChanged" }; } }

    protected override void Run(OctopusScenarioFields fields)
    {
        var action = fields.Get("action").Trim().ToLowerInvariant();
        var id = fields.Get("groupId").Trim();
        if (action != "fetch" && action != "follow" && action != "unfollow")
        { Report("No call made: action must be fetch, follow or unfollow."); return; }
        if (action != "fetch" && (id.Length == 0 || id == "none"))
        { Report("Fetch groups first, then enter a returned group id in Customize."); return; }
        var sdk = OctopusScenarioSdk.Current;
        EnsureObserving(sdk);
        ReportRunning(action + " groups…");
        if (action == "fetch")
        {
            Announce("FetchGroups");
            sdk.FetchGroups(groups => ShowGroups(groups, true), error => Report("FetchGroups failed: " + error));
            return;
        }
        var method = action == "follow" ? "FollowGroup" : "UnfollowGroup";
        Announce(method, "groupId=" + id);
        if (action == "follow")
            sdk.FollowGroup(id, () => Report(method + " succeeded: " + id),
                error => Report(method + " failed (" + error.Code + "): " + error.Message));
        else
            sdk.UnfollowGroup(id, () => Report(method + " succeeded: " + id),
                error => Report(method + " failed (" + error.Code + "): " + error.Message));
    }

    private void EnsureObserving(IOctopusScenarioSdk sdk)
    {
        if (!_observing)
        {
            Announce("OnGroupsChanged +=");
            Observe<IList<OctopusGroup>>(h => sdk.GroupsChanged += h, h => { Announce("OnGroupsChanged -="); sdk.GroupsChanged -= h; },
                (owner, groups) => ((GroupsScenario)owner).ShowGroups(groups, false));
            _observing = true;
        }
    }

    private void ShowGroups(IList<OctopusGroup> groups, bool completed)
    {
        var result = new StringBuilder("Groups: " + groups.Count);
        if (groups.Count == 0) result.Append(" — provision groups on the backend first.");
        foreach (var group in groups)
            result.Append("\n" + group.Name + " (" + group.Id + "): followed=" + group.IsFollowed +
                ", canChangeFollowStatus=" + group.CanChangeFollowStatus + ", canAccess=" + group.CanAccess +
                ", canCreateChildren=" + group.CanCreateChildren);
        if (!completed && IsRunning) ReportRunning(result.ToString());
        else Report(result.ToString());
    }
}
