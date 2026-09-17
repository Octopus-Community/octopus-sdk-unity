#if UNITY_EDITOR
using System;
using System.Collections.Generic;

public partial class OctopusSDK
{
    public static partial class Mock
    {
        internal static readonly Dictionary<string, OctopusCommunityData> CommunityData
            = new Dictionary<string, OctopusCommunityData>();

        /// <summary>Stores a member's fetch result and queues a change if that member is observed.
        /// Null clears that member's snapshot. Works even when mock recording is disabled.
        /// Profile and client user ids are separate keys; aliases are not resolved by the Mock.</summary>
        /// <exception cref="ArgumentNullException">memberId is null.</exception>
        public static void SetCommunityData(OctopusCommunityMemberId memberId, OctopusCommunityData data)
        {
            if (memberId == null) throw new ArgumentNullException("memberId");
            if (data == null) CommunityData.Remove(memberId.Key);
            else CommunityData[memberId.Key] = data;
            if (_communityDataMember != null && _communityDataMember.Key == memberId.Key)
                QueueCommunityData(data);
        }

        internal static OctopusCommunityData GetCommunityData(OctopusCommunityMemberId memberId)
        {
            OctopusCommunityData data;
            return CommunityData.TryGetValue(memberId.Key, out data) ? data : null;
        }

        internal static void ResetCommunityDataMock()
        {
            ResetCommunityDataState(true);
        }
    }
}
#endif
