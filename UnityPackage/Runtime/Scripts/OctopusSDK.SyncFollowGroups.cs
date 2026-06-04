using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public enum OctopusSyncFollowGroupStatus
{
    Applied, Skipped, GroupNotFound, NotFollowable,
    NotUnfollowable, AlreadyFollowed, AlreadyUnfollowed, UnknownError
}

public struct OctopusSyncFollowGroupAction
{
    public string GroupId;
    public bool Followed;
    public DateTime ActionDate;
}

public struct OctopusSyncFollowGroupResult
{
    public string GroupId;
    public OctopusSyncFollowGroupStatus Status;
}

public struct OctopusGroup
{
    public string Id;
    public string Name;
    public bool IsFollowed;
    public bool CanChangeFollowStatus;
}

// Pure, testable conversion helpers shared by both platform bridges.
internal static class OctopusSyncFollowGroupParsing
{
    static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static OctopusSyncFollowGroupStatus StatusFromWire(string s)
    {
        switch (s)
        {
            case "applied": return OctopusSyncFollowGroupStatus.Applied;
            case "skipped": return OctopusSyncFollowGroupStatus.Skipped;
            case "groupNotFound": return OctopusSyncFollowGroupStatus.GroupNotFound;
            case "notFollowable": return OctopusSyncFollowGroupStatus.NotFollowable;
            case "notUnfollowable": return OctopusSyncFollowGroupStatus.NotUnfollowable;
            case "alreadyFollowed": return OctopusSyncFollowGroupStatus.AlreadyFollowed;
            case "alreadyUnfollowed": return OctopusSyncFollowGroupStatus.AlreadyUnfollowed;
            default: return OctopusSyncFollowGroupStatus.UnknownError;
        }
    }

    public static string ActionsToJson(IList<OctopusSyncFollowGroupAction> actions)
    {
        var rows = new List<Dictionary<string, string>>();
        foreach (var a in actions)
        {
            long millis = (long)(a.ActionDate.ToUniversalTime() - Epoch).TotalMilliseconds;
            rows.Add(new Dictionary<string, string>
            {
                { "groupId", a.GroupId ?? "" },
                { "followed", a.Followed ? "true" : "false" },
                { "actionDateMillis", millis.ToString() },
            });
        }
        return OctopusJson.WriteArray(rows, new HashSet<string> { "followed", "actionDateMillis" });
    }

    public static List<OctopusSyncFollowGroupResult> ResultsFromJson(string json)
    {
        var list = new List<OctopusSyncFollowGroupResult>();
        foreach (var row in OctopusJson.ParseArray(json))
        {
            list.Add(new OctopusSyncFollowGroupResult
            {
                GroupId = row.ContainsKey("groupId") ? row["groupId"] : "",
                Status = StatusFromWire(row.ContainsKey("status") ? row["status"] : ""),
            });
        }
        return list;
    }

    public static List<OctopusGroup> GroupsFromJson(string json)
    {
        var list = new List<OctopusGroup>();
        foreach (var row in OctopusJson.ParseArray(json))
        {
            list.Add(new OctopusGroup
            {
                Id = row.ContainsKey("id") ? row["id"] : "",
                Name = row.ContainsKey("name") ? row["name"] : "",
                IsFollowed = row.ContainsKey("isFollowed") && row["isFollowed"] == "true",
                CanChangeFollowStatus = !row.ContainsKey("canChangeFollowStatus") || row["canChangeFollowStatus"] == "true",
            });
        }
        return list;
    }
}

public partial class OctopusSDK
{
    static int _nextRequestId = 1;
    static readonly Dictionary<int, Action<IList<OctopusSyncFollowGroupResult>>> _syncCallbacks
        = new Dictionary<int, Action<IList<OctopusSyncFollowGroupResult>>>();
    static readonly Dictionary<int, Action<IList<OctopusGroup>>> _fetchCallbacks
        = new Dictionary<int, Action<IList<OctopusGroup>>>();
    static readonly Dictionary<int, Action<string>> _errorCallbacks
        = new Dictionary<int, Action<string>>();

    // Continuous stream of the connected user's groups (mirrors native groups flow/publisher).
    public static event Action<IList<OctopusGroup>> OnGroupsChanged;

    public static void SyncFollowGroups(
        IList<OctopusSyncFollowGroupAction> actions,
        Action<IList<OctopusSyncFollowGroupResult>> onCompleted,
        Action<string> onError = null)
    {
        if (actions == null || actions.Count == 0)
        {
            onCompleted?.Invoke(new List<OctopusSyncFollowGroupResult>());
            return;
        }
        int id = _nextRequestId++;
        _syncCallbacks[id] = onCompleted;
        if (onError != null) _errorCallbacks[id] = onError;
        string json = OctopusSyncFollowGroupParsing.ActionsToJson(actions);
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("syncFollowGroups", id, json);
        }
#elif UNITY_IOS && !UNITY_EDITOR
        OctopusSdkSyncFollowGroups(id, json);
#endif
    }

    public static void FetchGroups(
        Action<IList<OctopusGroup>> onCompleted,
        Action<string> onError = null)
    {
        int id = _nextRequestId++;
        _fetchCallbacks[id] = onCompleted;
        if (onError != null) _errorCallbacks[id] = onError;
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("fetchGroups", id);
        }
#elif UNITY_IOS && !UNITY_EDITOR
        OctopusSdkFetchGroups(id);
#endif
    }

    static void TriggerOnGroupsChanged(IList<OctopusGroup> groups) => OnGroupsChanged?.Invoke(groups);

    // payload is "<requestId>\n<json-or-message>"
    static bool SplitRequest(string payload, out int id, out string rest)
    {
        id = 0; rest = "";
        int nl = payload.IndexOf('\n');
        if (nl < 0) return false;
        if (!int.TryParse(payload.Substring(0, nl), out id)) return false;
        rest = payload.Substring(nl + 1);
        return true;
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        public void OnSyncFollowGroupsResult(string payload)
        {
            if (!SplitRequest(payload, out int id, out string json)) return;
            if (_syncCallbacks.TryGetValue(id, out var cb))
            {
                _syncCallbacks.Remove(id); _errorCallbacks.Remove(id);
                cb?.Invoke(OctopusSyncFollowGroupParsing.ResultsFromJson(json));
            }
        }

        public void OnSyncFollowGroupsError(string payload)
        {
            if (!SplitRequest(payload, out int id, out string msg)) return;
            _syncCallbacks.Remove(id);
            if (_errorCallbacks.TryGetValue(id, out var cb)) { _errorCallbacks.Remove(id); cb?.Invoke(msg); }
        }

        public void OnFetchGroupsResult(string payload)
        {
            if (!SplitRequest(payload, out int id, out string json)) return;
            if (_fetchCallbacks.TryGetValue(id, out var cb))
            {
                _fetchCallbacks.Remove(id); _errorCallbacks.Remove(id);
                cb?.Invoke(OctopusSyncFollowGroupParsing.GroupsFromJson(json));
            }
        }

        public void OnFetchGroupsError(string payload)
        {
            if (!SplitRequest(payload, out int id, out string msg)) return;
            _fetchCallbacks.Remove(id);
            if (_errorCallbacks.TryGetValue(id, out var cb)) { _errorCallbacks.Remove(id); cb?.Invoke(msg); }
        }

        public void OnGroupsChanged(string json)
        {
            OctopusSDK.TriggerOnGroupsChanged(OctopusSyncFollowGroupParsing.GroupsFromJson(json));
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void OctopusSdkSyncFollowGroups(int requestId, string actionsJson);
    [DllImport("__Internal")] private static extern void OctopusSdkFetchGroups(int requestId);
#endif
}
