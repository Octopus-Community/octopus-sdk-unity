// Groups Example — fetch groups, sync follow/unfollow, open a group feed.
using System;
using System.Collections.Generic;
using UnityEngine;

public class GroupsExample : MonoBehaviour
{
    const string API_KEY = "YOUR_API_KEY";

    void Start()
    {
        // Connect your host user with a backend token provider before authenticated group actions.
        OctopusSDK.Initialize(API_KEY, ConnectionMode.SSO());
        OctopusSDK.OnGroupsChanged += groups =>
            Debug.Log($"Groups updated: {groups.Count}");
    }

    public void LoadGroups()
    {
        OctopusSDK.FetchGroups(
            onCompleted: groups =>
            {
                foreach (var g in groups)
                    Debug.Log($"{g.Id} {g.Name} followed={g.IsFollowed} canChange={g.CanChangeFollowStatus}");
            },
            onError: msg => Debug.LogError("FetchGroups failed: " + msg));
    }

    public void FollowFirstGroup(string groupId)
    {
        var actions = new List<OctopusSyncFollowGroupAction>
        {
            new OctopusSyncFollowGroupAction { GroupId = groupId, Followed = true, ActionDate = DateTime.UtcNow },
        };
        OctopusSDK.SyncFollowGroups(
            actions,
            onCompleted: results =>
            {
                foreach (var r in results)
                    Debug.Log($"sync {r.GroupId} -> {r.Status}");
            },
            onError: msg => Debug.LogError("SyncFollowGroups failed: " + msg));
    }

    public void OpenGroup(string groupId) => OctopusSDK.OpenGroup(groupId);
}
