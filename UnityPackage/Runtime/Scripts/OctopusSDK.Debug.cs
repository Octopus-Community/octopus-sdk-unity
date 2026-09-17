using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

public partial class OctopusSDK
{
    /// <summary>Debug/QA only — do not ship in production builds. Override profile editability; null restores server values. Disabled is meaningful for bio only. Call after Initialize on the Unity main thread.</summary>
    public static void DebugOverrideProfileFieldsLock(OctopusProfileFieldsLock fieldsLock)
    {
        string json = OctopusDebugParsing.ProfileLockToJson(fieldsLock);
#if UNITY_EDITOR
        Mock.RecordDebugOverride("OverrideProfileFieldsLock", fieldsLock);
#elif UNITY_ANDROID
        using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
            plugin.CallStatic("debugOverrideProfileFieldsLock", json);
#elif UNITY_IOS
        OctopusSdkDebugOverrideProfileFieldsLock(json);
#endif
    }

    /// <summary>Debug/QA only — do not ship in production builds. Override creation affordances, not existing content display; null restores server values. Call after Initialize on the Unity main thread.</summary>
    public static void DebugOverrideContentOptions(OctopusContentOptions options)
    {
        string json = OctopusDebugParsing.ContentOptionsToJson(options);
#if UNITY_EDITOR
        Mock.RecordDebugOverride("OverrideContentOptions", options);
#elif UNITY_ANDROID
        using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
            plugin.CallStatic("debugOverrideContentOptions", json);
#elif UNITY_IOS
        OctopusSdkDebugOverrideContentOptions(json);
#endif
    }

    /// <summary>Debug/QA only — do not ship in production builds. Override consent at first contribution; null restores server values. Call after Initialize on the Unity main thread.</summary>
    public static void DebugOverrideTermsAcceptanceMode(OctopusTermsAcceptanceMode? mode)
    {
        string wire = mode.HasValue ? OctopusDebugParsing.TermsToWire(mode.Value) : "null";
#if UNITY_EDITOR
        Mock.RecordDebugOverride("OverrideTermsAcceptanceMode", mode);
#elif UNITY_ANDROID
        using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
            plugin.CallStatic("debugOverrideTermsAcceptanceMode", wire);
#elif UNITY_IOS
        OctopusSdkDebugOverrideTermsAcceptanceMode(wire);
#endif
    }

    /// <summary>Debug/QA only — do not ship in production builds. Force the Unified Profile flag. Profile routing also requires profile-tap interception. Call after Initialize on the Unity main thread.</summary>
    public static void DebugOverrideExposeClientUserId(bool enabled)
    {
        DebugOverrideExposeClientUserId((bool?)enabled);
    }

    /// <summary>Debug/QA only — do not ship in production builds. Force the Unified Profile flag, or pass null to restore the server value. Call after Initialize on the Unity main thread.</summary>
    public static void DebugOverrideExposeClientUserId(bool? enabled)
    {
        string wire = enabled.HasValue ? (enabled.Value ? "true" : "false") : "null";
#if UNITY_EDITOR
        Mock.RecordDebugOverride("OverrideExposeClientUserId", enabled);
#elif UNITY_ANDROID
        using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
            plugin.CallStatic("debugOverrideExposeClientUserId", wire);
#elif UNITY_IOS
        OctopusSdkDebugOverrideExposeClientUserId(wire);
#endif
    }

    /// <summary>Debug/QA only — do not ship in production builds. Read the last effective config (including local overrides); onResult receives null before any config is available. Does not fetch from the network. Callbacks run on the Unity main thread when its loop resumes.</summary>
    public static void DebugGetCommunityConfig(Action<OctopusCommunityConfig> onResult, Action<string> onError)
    {
        int id = RegisterDebugConfigCallbacks(onResult, onError);
        try
        {
#if UNITY_EDITOR
            Mock.Record("DebugGetCommunityConfig");
            ReceiveDebugConfig(id + "\n" + (Mock.DebugCommunityConfigError ?? Mock.DebugCommunityConfigJson),
                Mock.DebugCommunityConfigError != null);
#elif UNITY_ANDROID
            using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
                plugin.CallStatic("debugGetCommunityConfig", id);
#elif UNITY_IOS
            OctopusSdkDebugGetCommunityConfig(id);
#else
            ReceiveDebugConfig(id + "\nDebug configuration requires Android or iOS", true);
#endif
        }
        catch (Exception e) { ReceiveDebugConfig(id + "\n" + e.Message, true); }
    }

    private sealed class DebugConfigCallbacks
    {
        internal Action<OctopusCommunityConfig> Result;
        internal Action<string> Error;
    }
    private static int _nextDebugConfigRequestId;
    private static readonly ConcurrentDictionary<int, DebugConfigCallbacks> _debugConfigCallbacks
        = new ConcurrentDictionary<int, DebugConfigCallbacks>();

    internal static int RegisterDebugConfigCallbacks(Action<OctopusCommunityConfig> result, Action<string> error)
    {
        int id = Interlocked.Increment(ref _nextDebugConfigRequestId);
        _debugConfigCallbacks[id] = new DebugConfigCallbacks { Result = result, Error = error };
        return id;
    }

    internal static void ReceiveDebugConfig(string payload, bool failed)
    {
        int id;
        string json;
        if (string.IsNullOrEmpty(payload) || !SplitRequest(payload, out id, out json)) return;
        DebugConfigCallbacks callbacks;
        if (!_debugConfigCallbacks.TryRemove(id, out callbacks)) return;
        OctopusCommunityConfig config = null;
        string error = failed ? json : null;
        if (!failed)
        {
            try { config = OctopusDebugParsing.ConfigFromJson(json); }
            catch (Exception e) { error = "Malformed community config: " + e.Message; }
        }
        OctopusMainThread.Post(() =>
        {
            if (error != null) callbacks.Error?.Invoke(error);
            else callbacks.Result?.Invoke(config);
        });
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        /// <summary>Debug/QA only — do not ship in production builds. Native configuration result envelope.</summary>
        public void OnDebugGetCommunityConfigResult(string payload) { ReceiveDebugConfig(payload, false); }
        /// <summary>Debug/QA only — do not ship in production builds. Native configuration error envelope.</summary>
        public void OnDebugGetCommunityConfigError(string payload) { ReceiveDebugConfig(payload, true); }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private partial class OctopusBridgeListenerProxy
    {
        /// <summary>Debug/QA only — do not ship in production builds. Android configuration result.</summary>
        public void onDebugGetCommunityConfigResult(string payload) { ReceiveDebugConfig(payload, false); }
        /// <summary>Debug/QA only — do not ship in production builds. Android configuration error.</summary>
        public void onDebugGetCommunityConfigError(string payload) { ReceiveDebugConfig(payload, true); }
    }
#endif

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void OctopusSdkDebugOverrideProfileFieldsLock(string json);
    [DllImport("__Internal")] private static extern void OctopusSdkDebugOverrideContentOptions(string json);
    [DllImport("__Internal")] private static extern void OctopusSdkDebugOverrideTermsAcceptanceMode(string mode);
    [DllImport("__Internal")] private static extern void OctopusSdkDebugOverrideExposeClientUserId(string enabled);
    [DllImport("__Internal")] private static extern void OctopusSdkDebugGetCommunityConfig(int requestId);
#endif
}
