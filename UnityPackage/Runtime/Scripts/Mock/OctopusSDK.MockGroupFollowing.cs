#if UNITY_EDITOR
using System.Collections.Generic;

public partial class OctopusSDK
{
    public static partial class Mock
    {
        /// <summary>Failure consumed by the next individual follow/unfollow call, then cleared.
        /// Null uses the simulated group state. Use this to exercise Android's LastFollowedGroup.</summary>
        public static OctopusGroupFollowUnfollowError NextGroupFollowUnfollowError { get; set; }

        /// <summary>Set session-only group data for FetchGroups and individual follow/unfollow calls.
        /// Copies the list and leaves the persisted mock settings untouched. Reset clears it.</summary>
        public static void SetGroups(IList<OctopusGroup> groups)
        {
            MockBackend._groupFollowingGroups = groups == null
                ? new List<OctopusGroup>() : new List<OctopusGroup>(groups);
        }

        /// <summary>Queue a group-access-denied event on the Unity main thread, even if Mock is disabled.</summary>
        public static void EmitGroupAccessDenied(string groupId)
        {
            ReceiveGroupAccessDenied(groupId);
        }

        private static void ResetGroupFollowing()
        {
            NextGroupFollowUnfollowError = null;
            MockBackend._groupFollowingGroups = null;
        }
    }

    internal static partial class MockBackend
    {
        internal static List<OctopusGroup> _groupFollowingGroups;

        internal static void SetGroupFollowing(int requestId, string groupId, bool followed)
        {
            Mock.Record(followed ? "FollowGroup" : "UnfollowGroup", groupId);
            var error = Mock.NextGroupFollowUnfollowError;
            Mock.NextGroupFollowUnfollowError = null;
            if (error != null)
            {
                CompleteGroupFollowing(requestId, error);
                return;
            }
            if (_groupFollowingGroups == null) _groupFollowingGroups = new List<OctopusGroup>(SeedGroups());
            int index = _groupFollowingGroups.FindIndex(group => group.Id == groupId);
            if (index < 0)
            {
                error = new OctopusGroupFollowUnfollowError(OctopusGroupFollowUnfollowErrorCode.MissingGroup, "Group not found");
            }
            else
            {
                var group = _groupFollowingGroups[index];
                if (!group.CanChangeFollowStatus)
                    error = new OctopusGroupFollowUnfollowError(OctopusGroupFollowUnfollowErrorCode.UnfollowableGroup,
                        "Group follow state cannot be changed");
                else if (group.IsFollowed == followed)
                    error = new OctopusGroupFollowUnfollowError(followed
                        ? OctopusGroupFollowUnfollowErrorCode.GroupAlreadyFollowed
                        : OctopusGroupFollowUnfollowErrorCode.GroupAlreadyUnfollowed, "Group already has the requested follow state");
                else
                {
                    group.IsFollowed = followed;
                    _groupFollowingGroups[index] = group;
                    var snapshot = new List<OctopusGroup>(_groupFollowingGroups);
                    OctopusMainThread.Post(() => TriggerOnGroupsChanged(snapshot));
                }
            }
            CompleteGroupFollowing(requestId, error);
        }
    }
}
#endif
