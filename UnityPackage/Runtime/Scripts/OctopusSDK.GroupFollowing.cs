using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

/// <summary>The reason an individual group follow or unfollow operation failed.</summary>
public enum OctopusGroupFollowUnfollowErrorCode
{
    /// <summary>An unclassified business, connection, authentication or bridge failure.</summary>
    Unknown,
    /// <summary>No group exists with the supplied identifier.</summary>
    MissingGroup,
    /// <summary>The group's follow state cannot be changed.</summary>
    UnfollowableGroup,
    /// <summary>The group is already followed.</summary>
    GroupAlreadyFollowed,
    /// <summary>The group is already unfollowed.</summary>
    GroupAlreadyUnfollowed,
    /// <summary>Android prevents unfollowing the user's last followed group.</summary>
    LastFollowedGroup
}

/// <summary>A typed failure from FollowGroup or UnfollowGroup, including the native message.</summary>
public sealed class OctopusGroupFollowUnfollowError
{
    /// <summary>The classified failure; unfamiliar native errors map to Unknown.</summary>
    public OctopusGroupFollowUnfollowErrorCode Code { get; private set; }
    /// <summary>The native diagnostic message, or a fallback when none was supplied.</summary>
    public string Message { get; private set; }

    /// <summary>Create a failure, including for an Editor mock response.</summary>
    public OctopusGroupFollowUnfollowError(OctopusGroupFollowUnfollowErrorCode code, string message)
    {
        Code = code;
        Message = string.IsNullOrEmpty(message) ? "Unknown error" : message;
    }
}

internal static class OctopusGroupFollowingParsing
{
    internal static OctopusGroupFollowUnfollowError ErrorFromJson(string json)
    {
        try
        {
            var row = OctopusJson.ParseObject(json);
            string type;
            string message;
            row.TryGetValue("type", out type);
            row.TryGetValue("message", out message);
            var code = OctopusGroupFollowUnfollowErrorCode.Unknown;
            switch (type)
            {
                case "missingGroup": code = OctopusGroupFollowUnfollowErrorCode.MissingGroup; break;
                case "unfollowableGroup": code = OctopusGroupFollowUnfollowErrorCode.UnfollowableGroup; break;
                case "groupAlreadyFollowed": code = OctopusGroupFollowUnfollowErrorCode.GroupAlreadyFollowed; break;
                case "groupAlreadyUnfollowed": code = OctopusGroupFollowUnfollowErrorCode.GroupAlreadyUnfollowed; break;
                case "lastFollowedGroup": code = OctopusGroupFollowUnfollowErrorCode.LastFollowedGroup; break;
            }
            return new OctopusGroupFollowUnfollowError(code, message);
        }
        catch (FormatException)
        {
            return new OctopusGroupFollowUnfollowError(OctopusGroupFollowUnfollowErrorCode.Unknown, "Malformed group error payload");
        }
    }
}

public partial class OctopusSDK
{
    private sealed class GroupFollowingCallbacks
    {
        internal Action Success;
        internal Action<OctopusGroupFollowUnfollowError> Error;
    }

    private static int _nextGroupFollowingRequestId;
    private static readonly ConcurrentDictionary<int, GroupFollowingCallbacks> _groupFollowingCallbacks
        = new ConcurrentDictionary<int, GroupFollowingCallbacks>();

    /// <summary>Raised on the Unity main thread with the id of a locked group the user tried to open.
    /// Delivery waits for the player loop to resume when the native community UI pauses Unity.</summary>
    public static event Action<string> OnGroupAccessDenied;

    /// <summary>Follow a group. Callbacks run on the Unity main thread. On iOS this uses a single
    /// SyncFollowGroups action; applied and skipped statuses succeed. Connection and bridge failures
    /// use Unknown with a diagnostic message.</summary>
    public static void FollowGroup(string groupId, Action onSuccess, Action<OctopusGroupFollowUnfollowError> onError)
    {
        SetGroupFollowing(groupId, true, onSuccess, onError);
    }

    /// <summary>Unfollow a group. Callbacks run on the Unity main thread. Android can return
    /// LastFollowedGroup; iOS has no equivalent restriction and uses a single SyncFollowGroups action.</summary>
    public static void UnfollowGroup(string groupId, Action onSuccess, Action<OctopusGroupFollowUnfollowError> onError)
    {
        SetGroupFollowing(groupId, false, onSuccess, onError);
    }

    private static void SetGroupFollowing(string groupId, bool followed, Action onSuccess,
        Action<OctopusGroupFollowUnfollowError> onError)
    {
        int id = RegisterGroupFollowingCallbacks(onSuccess, onError);
        if (string.IsNullOrEmpty(groupId))
        {
            CompleteGroupFollowing(id, new OctopusGroupFollowUnfollowError(
                OctopusGroupFollowUnfollowErrorCode.MissingGroup, "A group id is required"));
            return;
        }
        try
        {
#if UNITY_EDITOR
            MockBackend.SetGroupFollowing(id, groupId, followed);
#elif UNITY_ANDROID
            using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
            {
                if (followed) plugin.CallStatic("followGroup", id, groupId);
                else plugin.CallStatic("unfollowGroup", id, groupId);
            }
#elif UNITY_IOS
            if (followed) OctopusSdkFollowGroup(id, groupId);
            else OctopusSdkUnfollowGroup(id, groupId);
#else
            CompleteGroupFollowing(id, new OctopusGroupFollowUnfollowError(
                OctopusGroupFollowUnfollowErrorCode.Unknown, "Group following requires Android or iOS"));
#endif
        }
        catch (Exception e)
        {
            CompleteGroupFollowing(id, new OctopusGroupFollowUnfollowError(
                OctopusGroupFollowUnfollowErrorCode.Unknown, e.Message));
        }
    }

    internal static int RegisterGroupFollowingCallbacks(Action success, Action<OctopusGroupFollowUnfollowError> error)
    {
        int id = Interlocked.Increment(ref _nextGroupFollowingRequestId);
        _groupFollowingCallbacks[id] = new GroupFollowingCallbacks { Success = success, Error = error };
        return id;
    }

    internal static void CompleteGroupFollowing(int id, OctopusGroupFollowUnfollowError error)
    {
        GroupFollowingCallbacks callbacks;
        if (!_groupFollowingCallbacks.TryRemove(id, out callbacks)) return;
        OctopusMainThread.Post(() =>
        {
            if (error == null) callbacks.Success?.Invoke();
            else callbacks.Error?.Invoke(error);
        });
    }

    internal static void ReceiveGroupFollowingResult(string payload, bool failed)
    {
        int id;
        string json;
        if (string.IsNullOrEmpty(payload) || !SplitRequest(payload, out id, out json)) return;
        CompleteGroupFollowing(id, failed ? OctopusGroupFollowingParsing.ErrorFromJson(json) : null);
    }

    internal static void ReceiveGroupAccessDenied(string groupId)
    {
        if (!string.IsNullOrEmpty(groupId))
            OctopusMainThread.Post(() => OnGroupAccessDenied?.Invoke(groupId));
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        /// <summary>Native landing pad for a successful individual follow or unfollow request.</summary>
        public void OnGroupFollowUnfollowResult(string payload)
        {
            ReceiveGroupFollowingResult(payload, false);
        }

        /// <summary>Native landing pad for an individual follow or unfollow error envelope.</summary>
        public void OnGroupFollowUnfollowError(string payload)
        {
            ReceiveGroupFollowingResult(payload, true);
        }

        /// <summary>Native landing pad carrying the inaccessible group's identifier.</summary>
        public void OnGroupAccessDenied(string groupId)
        {
            ReceiveGroupAccessDenied(groupId);
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private partial class OctopusBridgeListenerProxy
    {
        /// <summary>Android listener completion envelope.</summary>
        public void onGroupFollowUnfollowResult(string payload) { ReceiveGroupFollowingResult(payload, false); }
        /// <summary>Android listener error envelope.</summary>
        public void onGroupFollowUnfollowError(string payload) { ReceiveGroupFollowingResult(payload, true); }
        /// <summary>Android listener inaccessible group identifier.</summary>
        public void onGroupAccessDenied(string groupId) { ReceiveGroupAccessDenied(groupId); }
    }
#endif

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void OctopusSdkFollowGroup(int requestId, string groupId);
    [DllImport("__Internal")] private static extern void OctopusSdkUnfollowGroup(int requestId, string groupId);
#endif
}
