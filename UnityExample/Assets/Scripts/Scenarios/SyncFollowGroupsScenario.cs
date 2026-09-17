using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public sealed class SyncFollowGroupsScenario : CommunityScenarioPilot
{
    public SyncFollowGroupsScenario() : base("syncFollowGroups",
        new OctopusScenarioField("action", "Action (invert, follow, unfollow)"),
        new OctopusScenarioField("offsetMinutes", "actionDate offset in minutes (-5, 0, 5)"))
    {
        Add(1, PresetLabel(1, "Invert all"), "invert", "0");
        Add(2, PresetLabel(2, "FOLLOW all"), "follow", "0");
        Add(3, PresetLabel(3, "UNFOLLOW all"), "unfollow", "0");
    }

    public override IReadOnlyList<string> ApiSymbols
    { get { return new[] { "FetchGroups", "SyncFollowGroups" }; } }

    protected override void Run(OctopusScenarioFields fields)
    {
        var action = fields.Get("action").Trim().ToLowerInvariant();
        int offset;
        if ((action != "invert" && action != "follow" && action != "unfollow") ||
            !int.TryParse(fields.Get("offsetMinutes"), NumberStyles.Integer, CultureInfo.InvariantCulture, out offset) ||
            (offset != -5 && offset != 0 && offset != 5))
        { Report("No call made: use invert, follow or unfollow and an offset of -5, 0 or 5 minutes."); return; }
        var sdk = OctopusScenarioSdk.Current;
        ReportRunning("Fetching community groups…");
        Announce("FetchGroups");
        sdk.FetchGroups(groups =>
        {
            try
            {
                if (groups.Count == 0)
                { Report("No groups in this community to sync. Provision at least one group on the backend — the SDK only follows groups that exist."); return; }
                var date = DateTime.UtcNow.AddMinutes(offset);
                var actions = new List<OctopusSyncFollowGroupAction>();
                var names = new Dictionary<string, string>();
                foreach (var group in groups)
                {
                    names[group.Id] = group.Name;
                    if (!group.CanChangeFollowStatus) continue;
                    actions.Add(new OctopusSyncFollowGroupAction
                    {
                        GroupId = group.Id, ActionDate = date,
                        Followed = action == "invert" ? !group.IsFollowed : action == "follow"
                    });
                }
                if (actions.Count == 0)
                { Report("No groups with canChangeFollowStatus = true. Every group here is force-followed / locked, so a batch would leave them all untouched."); return; }
                Announce("SyncFollowGroups", "action=" + action + ", count=" + actions.Count + ", actionDate=" + date.ToString("O"));
                sdk.SyncFollowGroups(actions, results =>
                {
                    var line = new StringBuilder("syncFollowGroups (offset " + offset + " min) returned " + results.Count + " result(s):");
                    foreach (var result in results)
                    {
                        string name;
                        if (!names.TryGetValue(result.GroupId, out name)) name = result.GroupId;
                        line.Append("\n" + name + ": " + result.Status);
                    }
                    Report(line.ToString());
                }, error => Report("syncFollowGroups failed: " + error));
            }
            catch (Exception error) { Report("syncFollowGroups failed: " + error.Message); }
        }, error => Report("FetchGroups failed: " + error));
    }
}
