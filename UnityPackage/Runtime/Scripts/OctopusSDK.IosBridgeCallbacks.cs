#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

// iOS loop-independent lane (reverse P/Invoke). C# registers C function pointers; the Swift plugin
// calls them DIRECTLY (off the Unity player loop) instead of UnitySendMessage, so events, the token
// request, and the bridge-share signature request all work while the Octopus UI is shown and the
// loop is paused.
public partial class OctopusSDK
{
    private delegate void EventCallbackDelegate(IntPtr utf8Json);
    private delegate void TokenRequestCallbackDelegate();
    private delegate void SignRequestCallbackDelegate(IntPtr utf8Fingerprint);

    // Held in static fields so the delegates (and the function pointers handed to native) are not GC'd.
    private static EventCallbackDelegate _eventCallback;
    private static TokenRequestCallbackDelegate _tokenRequestCallback;
    private static SignRequestCallbackDelegate _signRequestCallback;

    [MonoPInvokeCallback(typeof(EventCallbackDelegate))]
    private static void OnEventFromNative(IntPtr utf8Json)
    {
        try
        {
            string json = Marshal.PtrToStringUTF8(utf8Json);
            if (!string.IsNullOrEmpty(json)) OctopusEventDispatcher.Enqueue(json);
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    [MonoPInvokeCallback(typeof(TokenRequestCallbackDelegate))]
    private static void OnTokenRequestFromNative()
    {
        try { TriggerOnTokenRequested(); }
        catch (Exception e) { Debug.LogException(e); }
    }

    [MonoPInvokeCallback(typeof(SignRequestCallbackDelegate))]
    private static void OnBridgeShareSignRequestFromNative(IntPtr utf8Fingerprint)
    {
        try
        {
            string fingerprint = Marshal.PtrToStringUTF8(utf8Fingerprint);
            TriggerBridgeShareSign(fingerprint ?? "");
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    internal static void RegisterIosBridgeCallbacks()
    {
        _eventCallback = OnEventFromNative;
        _tokenRequestCallback = OnTokenRequestFromNative;
        _signRequestCallback = OnBridgeShareSignRequestFromNative;
        OctopusSdkSetBridgeCallbacks(_eventCallback, _tokenRequestCallback, _signRequestCallback);
    }

    [DllImport("__Internal")]
    private static extern void OctopusSdkSetBridgeCallbacks(
        EventCallbackDelegate eventCb, TokenRequestCallbackDelegate tokenCb, SignRequestCallbackDelegate signCb);
}
#endif
