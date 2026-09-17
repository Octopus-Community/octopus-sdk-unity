using System;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;

public partial class OctopusSDK
{
    private static int _nextLifecycleRequestId;
    private static int _lifecycleRequestId;
    private static Action _lifecycleCompleted;
    private static Action<string> _lifecycleError;

    /// <summary>
    /// Switches community and connection mode, or initializes if stopped. Disconnects the old
    /// user and replaces community data. Reconnect SSO users after completion. Closes the UI;
    /// the next Open builds it for the new community. Uses the default production server.
    /// Call on the Unity main thread and wait for completion/error before other SDK calls.
    /// Callbacks run on the Unity main thread; failure does not roll back the old community.
    /// </summary>
    public static void SwitchCommunity(string apiKey, ConnectionMode mode,
        Action onCompleted = null, Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(apiKey) || mode == null)
        {
            OctopusMainThread.EnsureExists();
            OctopusMainThread.Post(() => ReportLifecycleError(onError, "apiKey and mode are required."));
            return;
        }
        int id = BeginLifecycleRequest(onCompleted, onError);
        if (id == 0) return;
        ResetCommunityDataState(false);
        try
        {
#if UNITY_EDITOR
            MockBackend.SwitchCommunity(id, mode);
#elif UNITY_ANDROID
            SetUnityTheme();
            RegisterBridgeListener();
            using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
            {
                plugin.CallStatic("switchCommunity", id, apiKey, mode.Mode, mode.AppManagedFieldsAsIntArray);
            }
#elif UNITY_IOS
            SetUnityTheme();
            RegisterIosBridgeCallbacks();
            OctopusSdkSwitchCommunity(id, apiKey, mode.Mode, mode.AppManagedFieldsAsIntArray,
                mode.AppManagedFieldsAsIntArray.Length);
#else
            QueueLifecycleResponse(id + "\nUnsupported platform.", true);
#endif
        }
        catch (Exception)
        {
            QueueLifecycleResponse(id + "\nCould not start community switch.", true);
        }
    }

    /// <summary>Switches to Octopus-managed authentication; otherwise identical to SwitchCommunity.</summary>
    public static void SwitchCommunityOctopusAuth(string apiKey,
        Action onCompleted = null, Action<string> onError = null)
    {
        SwitchCommunity(apiKey, ConnectionMode.OctopusAuth(), onCompleted, onError);
    }

    /// <summary>
    /// Closes the UI and disconnects the user while keeping the SDK initialized on the same
    /// community. Android also clears SDK data and image caches; iOS only disconnects the user.
    /// iOS 1.13.2 cannot reset Octopus Auth: reports onError instead of calling its fatal
    /// SSO-only disconnect primitive. No-op before initialization. Call on the Unity main thread; wait for the main-thread
    /// completion/error callback before other SDK calls. Does not clear Mock.Calls.
    /// </summary>
    public static void Reset(Action onCompleted = null, Action<string> onError = null)
    {
        int id = BeginLifecycleRequest(onCompleted, onError);
        if (id == 0) return;
        ResetCommunityDataState(true);
        try
        {
#if UNITY_EDITOR
            MockBackend.ResetLifecycle(id);
#elif UNITY_ANDROID
            using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
            {
                plugin.CallStatic("reset", id);
            }
#elif UNITY_IOS
            OctopusSdkReset(id);
#else
            QueueLifecycleResponse(id + "\nUnsupported platform.", true);
#endif
        }
        catch (Exception)
        {
            QueueLifecycleResponse(id + "\nCould not start reset.", true);
        }
    }

    /// <summary>
    /// Closes the UI and releases the SDK. Initialize or SwitchCommunity is required before
    /// reuse. Android stops native operations without clearing persistent data; iOS attempts
    /// disconnection then releases the bridge's instance and subscriptions, even if disconnect
    /// fails. In iOS Octopus Auth it releases without disconnecting (no safe native primitive).
    /// No-op when stopped. Call on the Unity main thread and wait for the main-thread
    /// completion/error callback before other SDK calls. C# event subscriptions are retained.
    /// </summary>
    public static void Stop(Action onCompleted = null, Action<string> onError = null)
    {
        int id = BeginLifecycleRequest(onCompleted, onError);
        if (id == 0) return;
        ResetCommunityDataState(true);
        try
        {
#if UNITY_EDITOR
            MockBackend.StopLifecycle(id);
#elif UNITY_ANDROID
            using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
            {
                plugin.CallStatic("stop", id);
            }
#elif UNITY_IOS
            OctopusSdkStop(id);
#else
            QueueLifecycleResponse(id + "\nUnsupported platform.", true);
#endif
        }
        catch (Exception)
        {
            QueueLifecycleResponse(id + "\nCould not start stop.", true);
        }
    }

    /// <summary>
    /// Requests dismissal of the Octopus UI without disconnecting or stopping the SDK.
    /// Safe when already closed. iOS retains navigation state; Android finishes its Octopus
    /// activity. This call does not wait for the dismissal animation.
    /// </summary>
    public static void Close()
    {
#if UNITY_EDITOR
        MockBackend.Close();
#elif UNITY_ANDROID
        using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("close");
        }
#elif UNITY_IOS
        OctopusSdkClose(true);
#endif
    }

    private static int BeginLifecycleRequest(Action onCompleted, Action<string> onError)
    {
        OctopusMainThread.EnsureExists();
        if (_lifecycleRequestId != 0)
        {
            OctopusMainThread.Post(() => ReportLifecycleError(onError, "A lifecycle operation is already in progress."));
            return 0;
        }
#if !UNITY_EDITOR
        OctopusChannel.Initialize();
#endif
        if (_nextLifecycleRequestId == int.MaxValue) _nextLifecycleRequestId = 0;
        ClearClientPostSession();
        _lifecycleRequestId = ++_nextLifecycleRequestId;
        _lifecycleCompleted = onCompleted;
        _lifecycleError = onError;
        return _lifecycleRequestId;
    }

    internal static bool TryParseLifecycleResponse(string payload, out int id, out string message)
    {
        id = 0;
        message = "";
        if (payload == null) return false;
        int newline = payload.IndexOf('\n');
        if (newline < 1 || !int.TryParse(payload.Substring(0, newline), NumberStyles.None,
            CultureInfo.InvariantCulture, out id) || id <= 0) return false;
        message = payload.Substring(newline + 1);
        return true;
    }

    internal static void QueueLifecycleResponse(string payload, bool failed)
    {
        int id;
        string message;
        if (!TryParseLifecycleResponse(payload, out id, out message)) return;
        OctopusMainThread.Post(() =>
        {
            if (id != _lifecycleRequestId) return;
            var completed = _lifecycleCompleted;
            var error = _lifecycleError;
            _lifecycleRequestId = 0;
            _lifecycleCompleted = null;
            _lifecycleError = null;
            if (failed) ReportLifecycleError(error, message);
            else completed?.Invoke();
        });
    }

    private static void ReportLifecycleError(Action<string> callback, string message)
    {
        if (callback != null) callback(message);
        else UnityEngine.Debug.LogError("[Octopus SDK] " + message);
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        /// <summary>Native lifecycle completion landing pad: request id, newline, empty body.</summary>
        public void OnLifecycleResult(string payload) { QueueLifecycleResponse(payload, false); }

        /// <summary>Native lifecycle failure landing pad: request id, newline, error message.</summary>
        public void OnLifecycleError(string payload) { QueueLifecycleResponse(payload, true); }
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkSwitchCommunity(int requestId, string apiKey, string mode,
        int[] fields, int fieldsCount);
    [DllImport("__Internal")] private static extern void OctopusSdkReset(int requestId);
    [DllImport("__Internal")] private static extern void OctopusSdkStop(int requestId);
    [DllImport("__Internal")] private static extern void OctopusSdkClose([MarshalAs(UnmanagedType.I1)] bool keepState);
#endif
}
