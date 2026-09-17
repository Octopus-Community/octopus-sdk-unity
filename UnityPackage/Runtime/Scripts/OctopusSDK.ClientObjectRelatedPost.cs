using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public partial class OctopusSDK
{
    private sealed class ClientPostCallbacks
    {
        internal Action<string> Result;
        internal Action<OctopusClientPostError> Error;
        internal Func<string, Task<string>> Signer;
    }

    private sealed class ClientPostObservation
    {
        internal int Generation;
        internal volatile bool Ready;
    }

    private static int _nextClientPostRequestId;
    private static int _nextClientPostObservation;
    private static readonly ConcurrentDictionary<int, ClientPostCallbacks> _clientPostCallbacks
        = new ConcurrentDictionary<int, ClientPostCallbacks>();
    private static readonly ConcurrentDictionary<string, ClientPostObservation> _clientPostObservations
        = new ConcurrentDictionary<string, ClientPostObservation>();

    /// <summary>Raised on the Unity main thread with the observed object id and its latest post,
    /// or null while no post exists. Subscribe before StartObservingClientObjectRelatedPost.
    /// Delivery waits for the Unity loop to resume when native UI pauses it.</summary>
    public static event Action<string, OctopusPost> OnClientObjectRelatedPostChanged;

    /// <summary>Fetches the existing linked post or creates it from clientObject, returning its post id.
    /// Content is used only on first creation. Call on the Unity main thread after Initialize.
    /// Callbacks run on the Unity main thread. Lifecycle transitions cancel pending calls with Other.
    /// iOS validation, transport and bridge failures use Other; Android reports the first validation error.</summary>
    public static void FetchOrCreateClientObjectRelatedPost(OctopusClientObject clientObject,
        Action<string> onResult, Action<OctopusClientPostError> onError)
    {
        OctopusMainThread.EnsureExists();
        int id = RegisterClientPostCallbacks(onResult, onError, clientObject == null ? null : clientObject.SignBridgeShare);
        if (clientObject == null || !ValidClientObjectId(clientObject.ObjectId))
        {
            CompleteClientPost(id, null, new OctopusClientPostError(OctopusClientPostErrorCode.MissingObjectId,
                "An object id without newline or carriage return is required"));
            return;
        }
        if (!string.IsNullOrEmpty(clientObject.ImagePath) && !string.IsNullOrEmpty(clientObject.ImageUrl))
        {
            CompleteClientPost(id, null, new OctopusClientPostError(OctopusClientPostErrorCode.Other,
                "Set at most one of ImagePath and ImageUrl"));
            return;
        }
        try
        {
#if UNITY_EDITOR
            MockBackend.FetchOrCreateClientObjectRelatedPost(id, clientObject);
#elif UNITY_ANDROID
            RegisterBridgeListener();
            using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
            {
                plugin.CallStatic("fetchOrCreateClientObjectRelatedPost", id,
                    OctopusJson.ClientObjectToJson(clientObject), clientObject.SignBridgeShare != null);
            }
#elif UNITY_IOS
            OctopusSdkSetClientPostSignCallback(_clientPostSignCallback);
            OctopusSdkFetchOrCreateClientObjectRelatedPost(id, OctopusJson.ClientObjectToJson(clientObject),
                clientObject.SignBridgeShare != null ? 1 : 0);
#else
            CompleteClientPost(id, null, new OctopusClientPostError(OctopusClientPostErrorCode.Other, "Unsupported platform"));
#endif
        }
        catch (Exception e)
        {
            CompleteClientPost(id, null, new OctopusClientPostError(OctopusClientPostErrorCode.Other, e.Message));
        }
    }

    /// <summary>Starts one observation per object id and replays its current post (possibly null).
    /// Repeated starts are no-ops. Call on the Unity main thread after Initialize.
    /// Initialize, SwitchCommunity, Reset and Stop cancel all observations; start again afterwards.
    /// Invalid ids throw ArgumentException. Native observation failures are logged.</summary>
    public static void StartObservingClientObjectRelatedPost(string objectId)
    {
        if (!ValidClientObjectId(objectId)) throw new ArgumentException("An object id without newline or carriage return is required", "objectId");
        OctopusMainThread.EnsureExists();
        int generation = RegisterClientPostObservation(objectId);
        if (generation == 0) return;
        try
        {
#if UNITY_EDITOR
            ReceiveClientPostObservationStarted(objectId + "\n" + generation);
            MockBackend.StartObservingClientObjectRelatedPost(objectId);
#elif UNITY_ANDROID
            RegisterBridgeListener();
            using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
            {
                plugin.CallStatic("startObservingClientObjectRelatedPost", objectId, generation);
            }
#elif UNITY_IOS
            OctopusSdkStartObservingClientObjectRelatedPost(objectId, generation);
#endif
        }
        catch
        {
            ClientPostObservation ignored;
            _clientPostObservations.TryRemove(objectId, out ignored);
            throw;
        }
    }

    /// <summary>Stops only this object's observation. Repeated stops and unknown ids are no-ops.
    /// Call on the Unity main thread. C# event subscriptions are retained.</summary>
    public static void StopObservingClientObjectRelatedPost(string objectId)
    {
        ClientPostObservation ignored;
        if (objectId == null || !_clientPostObservations.TryRemove(objectId, out ignored)) return;
#if UNITY_EDITOR
        Mock.Record("StopObservingClientObjectRelatedPost", objectId);
#elif UNITY_ANDROID
        using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("stopObservingClientObjectRelatedPost", objectId);
        }
#elif UNITY_IOS
        OctopusSdkStopObservingClientObjectRelatedPost(objectId);
#endif
    }

    internal static int RegisterClientPostObservation(string objectId)
    {
        var observation = new ClientPostObservation { Generation = Interlocked.Increment(ref _nextClientPostObservation) };
        return _clientPostObservations.TryAdd(objectId, observation) ? observation.Generation : 0;
    }

    private static bool ValidClientObjectId(string id)
    {
        return !string.IsNullOrEmpty(id) && id.IndexOf('\n') < 0 && id.IndexOf('\r') < 0;
    }

    internal static int RegisterClientPostCallbacks(Action<string> result, Action<OctopusClientPostError> error,
        Func<string, Task<string>> signer = null)
    {
        int id = Interlocked.Increment(ref _nextClientPostRequestId);
        _clientPostCallbacks[id] = new ClientPostCallbacks { Result = result, Error = error, Signer = signer };
        return id;
    }

    internal static void CompleteClientPost(int id, string postId, OctopusClientPostError error)
    {
        OctopusMainThread.Post(() =>
        {
            ClientPostCallbacks callbacks;
            if (!_clientPostCallbacks.TryRemove(id, out callbacks)) return;
            if (error != null) callbacks.Error?.Invoke(error);
            else if (string.IsNullOrEmpty(postId)) callbacks.Error?.Invoke(new OctopusClientPostError(
                OctopusClientPostErrorCode.Other, "Native returned an empty post id"));
            else callbacks.Result?.Invoke(postId);
        });
    }

    internal static void ReceiveClientPostResult(string payload, bool failed)
    {
        int id;
        string value;
        if (string.IsNullOrEmpty(payload) || !SplitRequest(payload, out id, out value)) return;
        CompleteClientPost(id, failed ? null : value, failed ? OctopusJson.ClientPostErrorFromJson(value) : null);
    }

    internal static void ReceiveClientObjectRelatedPost(string payload)
    {
        if (string.IsNullOrEmpty(payload)) return;
        int newline = payload.IndexOf('\n');
        if (newline < 1) return;
        QueueClientObjectRelatedPost(payload.Substring(0, newline), OctopusJson.PostFromJson(payload.Substring(newline + 1)));
    }

    internal static void QueueClientObjectRelatedPost(string objectId, OctopusPost post)
    {
        ClientPostObservation observation;
        if (!_clientPostObservations.TryGetValue(objectId, out observation) || !observation.Ready) return;
        OctopusMainThread.Post(() =>
        {
            ClientPostObservation current;
            if (_clientPostObservations.TryGetValue(objectId, out current) && current == observation && current.Ready)
                OnClientObjectRelatedPostChanged?.Invoke(objectId, post);
        });
    }

    // Native sends this marker on the same ordered lane before the first snapshot. Until it
    // arrives, delayed UnitySendMessage values from an older observation are ignored.
    internal static void ReceiveClientPostObservationStarted(string payload)
    {
        if (string.IsNullOrEmpty(payload)) return;
        int newline = payload.IndexOf('\n');
        int generation;
        if (newline < 1 || !int.TryParse(payload.Substring(newline + 1), out generation)) return;
        ClientPostObservation observation;
        if (_clientPostObservations.TryGetValue(payload.Substring(0, newline), out observation)
            && observation.Generation == generation) observation.Ready = true;
    }

    internal static void ClearClientPostSession()
    {
        _clientPostObservations.Clear();
        foreach (var pair in _clientPostCallbacks)
        {
            ClientPostCallbacks callbacks;
            if (_clientPostCallbacks.TryRemove(pair.Key, out callbacks))
                OctopusMainThread.Post(() => callbacks.Error?.Invoke(new OctopusClientPostError(
                    OctopusClientPostErrorCode.Other, "Client post request cancelled by lifecycle transition")));
        }
#if UNITY_EDITOR
        Mock.ClearClientPostState();
#endif
    }

    // Request-scoped signing avoids replacing the editor's prefilled-post signer or another fetch's signer.
    internal static Task<string> SignClientPost(int id, string fingerprint)
    {
        return Task.Run(async () =>
        {
            ClientPostCallbacks callbacks;
            if (!_clientPostCallbacks.TryGetValue(id, out callbacks) || callbacks.Signer == null) return "";
            try { return await callbacks.Signer(fingerprint).ConfigureAwait(false) ?? ""; }
            catch { return ""; }
        });
    }

    private static async void ReceiveClientPostSignRequest(string payload)
    {
        int id;
        string fingerprint;
        if (string.IsNullOrEmpty(payload) || !SplitRequest(payload, out id, out fingerprint)) return;
        string signature = await SignClientPost(id, fingerprint).ConfigureAwait(false);
        try
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureJniAttached();
            using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
            {
                plugin.CallStatic("setClientPostSignature", id, signature);
            }
#elif UNITY_IOS && !UNITY_EDITOR
            OctopusSdkSetClientPostSignature(id, signature);
#endif
        }
        catch (Exception) { CompleteClientPost(id, null, new OctopusClientPostError(OctopusClientPostErrorCode.Other, "Signature delivery failed")); }
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        /// <summary>Native observation boundary: object id, newline, observation generation.</summary>
        public void OnClientPostObservationStarted(string payload) { ReceiveClientPostObservationStarted(payload); }
        /// <summary>Native result: request id, newline, post id.</summary>
        public void OnClientPostResult(string payload) { ReceiveClientPostResult(payload, false); }
        /// <summary>Native failure: request id, newline, typed error JSON.</summary>
        public void OnClientPostError(string payload) { ReceiveClientPostResult(payload, true); }
        /// <summary>Native observation: object id, newline, post JSON or null.</summary>
        public void OnClientObjectRelatedPostChanged(string payload) { ReceiveClientObjectRelatedPost(payload); }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private partial class OctopusBridgeListenerProxy
    {
        /// <summary>Android observation boundary envelope.</summary>
        public void onClientPostObservationStarted(string payload) { ReceiveClientPostObservationStarted(payload); }
        /// <summary>Android request result envelope.</summary>
        public void onClientPostResult(string payload) { ReceiveClientPostResult(payload, false); }
        /// <summary>Android request error envelope.</summary>
        public void onClientPostError(string payload) { ReceiveClientPostResult(payload, true); }
        /// <summary>Android related-post snapshot envelope.</summary>
        public void onClientObjectRelatedPostChanged(string payload) { ReceiveClientObjectRelatedPost(payload); }
        /// <summary>Loop-independent signing request, correlated with its fetch.</summary>
        public void requestClientPostSignature(string payload) { ReceiveClientPostSignRequest(payload); }
    }
#endif

#if UNITY_IOS && !UNITY_EDITOR
    private static readonly SignRequestCallbackDelegate _clientPostSignCallback = OnClientPostSignFromNative;
    [AOT.MonoPInvokeCallback(typeof(SignRequestCallbackDelegate))]
    private static void OnClientPostSignFromNative(IntPtr payload)
    {
        try { ReceiveClientPostSignRequest(Marshal.PtrToStringUTF8(payload)); }
        catch (Exception) { Debug.LogError("[Octopus SDK] Invalid client post signing request"); }
    }
    [DllImport("__Internal")] private static extern void OctopusSdkFetchOrCreateClientObjectRelatedPost(int requestId, string json, int hasSigner);
    [DllImport("__Internal")] private static extern void OctopusSdkStartObservingClientObjectRelatedPost(string objectId, int generation);
    [DllImport("__Internal")] private static extern void OctopusSdkStopObservingClientObjectRelatedPost(string objectId);
    [DllImport("__Internal")] private static extern void OctopusSdkSetClientPostSignCallback(SignRequestCallbackDelegate callback);
    [DllImport("__Internal")] private static extern void OctopusSdkSetClientPostSignature(int requestId, string signature);
#endif
}
