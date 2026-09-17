using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public partial class OctopusSDK
{
    private sealed class CommunityDataRequest
    {
        internal Action<OctopusCommunityData> Result;
        internal Action<string> Error;
    }

    private static readonly Dictionary<int, CommunityDataRequest> _communityDataRequests
        = new Dictionary<int, CommunityDataRequest>();
    private static int _nextCommunityDataRequestId;
    private static bool _observingCommunityData;
    private static OctopusCommunityMemberId _communityDataMember;
    private static int _communityDataGeneration;

    /// <summary>Raised on the Unity main thread with the observed member's public community data.
    /// The snapshot is null when unavailable.</summary>
    public static event Action<OctopusCommunityData> OnCommunityDataChanged;

    /// <summary>Refreshes the specified member's public community data. ClientUserId requires the
    /// community to expose client user ids; ProfileId selects the Octopus profile directly.
    /// Call on the Unity main thread after initialization; callbacks run on that thread.
    /// Native failures surfaced as null remain null; thrown failures use onError.
    /// Lifecycle changes cancel pending requests via onError.</summary>
    /// <exception cref="ArgumentNullException">memberId is null.</exception>
    public static void FetchCommunityData(OctopusCommunityMemberId memberId,
        Action<OctopusCommunityData> onResult, Action<string> onError)
    {
        if (memberId == null) throw new ArgumentNullException("memberId");
        OctopusMainThread.EnsureExists();
        int id = RegisterCommunityDataRequest(onResult, onError);
        try
        {
#if UNITY_EDITOR
            Mock.Record("FetchCommunityData", memberId.ProfileId, memberId.ClientUserId);
            CompleteCommunityDataRequest(id, Mock.GetCommunityData(memberId), null);
#elif UNITY_ANDROID
            OctopusChannel.Initialize();
            RegisterBridgeListener();
            using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
            {
                plugin.CallStatic("fetchCommunityData", id, memberId.ProfileId, memberId.ClientUserId);
            }
#elif UNITY_IOS
            OctopusChannel.Initialize();
            OctopusSdkFetchCommunityData(id, memberId.ProfileId, memberId.ClientUserId);
#else
            CompleteCommunityDataRequest(id, null, "Unsupported platform.");
#endif
        }
        catch (Exception)
        {
            CompleteCommunityDataRequest(id, null, "Could not fetch community data.");
        }
    }

    /// <summary>Starts observing the specified member, replacing any previous observation.
    /// Subscribe to OnCommunityDataChanged first. Rebinds the selected member on Initialize and
    /// SwitchCommunity; Reset/Stop end observation. ClientUserId requires exposed client user ids.
    /// Call on the Unity main thread. Observation replays cached data; FetchCommunityData refreshes it.</summary>
    /// <exception cref="ArgumentNullException">memberId is null.</exception>
    public static void StartObservingCommunityData(OctopusCommunityMemberId memberId)
    {
        if (memberId == null) throw new ArgumentNullException("memberId");
        OctopusMainThread.EnsureExists();
        _communityDataMember = memberId;
        _observingCommunityData = true;
        _communityDataGeneration++;
        try
        {
#if UNITY_EDITOR
            Mock.Record("StartObservingCommunityData", memberId.ProfileId, memberId.ClientUserId);
            QueueCommunityData(Mock.GetCommunityData(memberId));
#elif UNITY_ANDROID
            OctopusChannel.Initialize();
            RegisterBridgeListener();
            using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
            {
                plugin.CallStatic("startObservingCommunityData", memberId.ProfileId, memberId.ClientUserId);
            }
#elif UNITY_IOS
            OctopusChannel.Initialize();
            OctopusSdkStartObservingCommunityData(memberId.ProfileId, memberId.ClientUserId);
#else
            QueueCommunityData(null);
#endif
        }
        catch (Exception)
        {
            _observingCommunityData = false;
            _communityDataMember = null;
            _communityDataGeneration++;
            Debug.LogError("[Octopus SDK] Could not start community-data observation.");
        }
    }

    /// <summary>Stops community-data observation and drops queued C# updates, retaining event subscriptions.
    /// Safe to call repeatedly on the Unity main thread. Does not cancel one-shot fetches.</summary>
    public static void StopObservingCommunityData()
    {
        _observingCommunityData = false;
        _communityDataMember = null;
        _communityDataGeneration++;
#if UNITY_EDITOR
        Mock.Record("StopObservingCommunityData");
#elif UNITY_ANDROID
        using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("stopObservingCommunityData");
        }
#elif UNITY_IOS
        OctopusSdkStopObservingCommunityData();
#endif
    }

    internal static int RegisterCommunityDataRequest(Action<OctopusCommunityData> result, Action<string> error)
    {
        if (_nextCommunityDataRequestId == int.MaxValue) _nextCommunityDataRequestId = 0;
        int id = ++_nextCommunityDataRequestId;
        _communityDataRequests.Add(id, new CommunityDataRequest { Result = result, Error = error });
        return id;
    }

    internal static void ReceiveCommunityDataResponse(string payload, bool failed)
    {
        int id;
        string body;
        if (!TryParseLifecycleResponse(payload, out id, out body)) return;
        CompleteCommunityDataRequest(id, failed ? null : OctopusJson.CommunityDataFromJson(body), failed ? body : null);
    }

    private static void CompleteCommunityDataRequest(int id, OctopusCommunityData data, string error)
    {
        OctopusMainThread.Post(() =>
        {
            CommunityDataRequest request;
            if (!_communityDataRequests.TryGetValue(id, out request)) return;
            _communityDataRequests.Remove(id);
            if (error != null) request.Error?.Invoke(error);
            else request.Result?.Invoke(data);
        });
    }

    internal static void ReceiveCommunityData(string json) { QueueCommunityData(OctopusJson.CommunityDataFromJson(json)); }

    private static void QueueCommunityData(OctopusCommunityData data)
    {
        if (!_observingCommunityData) return;
        int generation = _communityDataGeneration;
        OctopusMainThread.Post(() =>
        {
            if (_observingCommunityData && generation == _communityDataGeneration)
                OnCommunityDataChanged?.Invoke(data);
        });
    }

    internal static void ResetCommunityDataState(bool stopObservation)
    {
        _communityDataGeneration++;
        if (stopObservation)
        {
            _observingCommunityData = false;
            _communityDataMember = null;
        }
        var requests = new List<CommunityDataRequest>(_communityDataRequests.Values);
        _communityDataRequests.Clear();
        foreach (var request in requests)
            OctopusMainThread.Post(() => request.Error?.Invoke("Community-data request cancelled by lifecycle change."));
#if UNITY_EDITOR
        Mock.CommunityData.Clear();
        if (!stopObservation) QueueCommunityData(null);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private partial class OctopusBridgeListenerProxy
    {
        /// <summary>Receives a correlated Android fetch result.</summary>
        public void onFetchCommunityDataResult(string payload) { ReceiveCommunityDataResponse(payload, false); }
        /// <summary>Receives a correlated Android fetch error.</summary>
        public void onFetchCommunityDataError(string payload) { ReceiveCommunityDataResponse(payload, true); }
        /// <summary>Receives an Android community-data snapshot.</summary>
        public void onCommunityDataChanged(string json) { ReceiveCommunityData(json); }
    }
#endif

    public partial class OctopusChannel : MonoBehaviour
    {
        /// <summary>Native fetch result: request id, newline, JSON object or literal null.</summary>
        public void OnFetchCommunityDataResult(string payload) { ReceiveCommunityDataResponse(payload, false); }
        /// <summary>Native fetch error: request id, newline, message.</summary>
        public void OnFetchCommunityDataError(string payload) { ReceiveCommunityDataResponse(payload, true); }
        /// <summary>Native observation snapshot: JSON object or literal null.</summary>
        public void OnCommunityDataChanged(string json) { ReceiveCommunityData(json); }
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void OctopusSdkFetchCommunityData(int requestId, string profileId, string clientUserId);
    [DllImport("__Internal")] private static extern void OctopusSdkStartObservingCommunityData(string profileId, string clientUserId);
    [DllImport("__Internal")] private static extern void OctopusSdkStopObservingCommunityData();
#endif
}
