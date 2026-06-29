using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public partial class OctopusSDK
{
    // The host's signing callback for the CURRENT create-post session, captured by
    // OpenCreatePost. Stays in C# — never marshalled; only the fingerprint (native->C#) and
    // the JWT (C#->native) cross the boundary, over the same loop-independent lane as the
    // token request.
    private static Func<string, Task<string>> _activeBridgeShareSigner;

    internal static void StashActiveBridgeShareSigner(OctopusPrefilledPost prefilled)
    {
        _activeBridgeShareSigner = prefilled?.SignBridgeShare;
    }

    // Invoked when the native SDK needs a signature for a gated image (mid-session, loop
    // paused). async void mirrors TriggerOnTokenRequested: its continuation runs off the
    // Unity main thread, so it completes while the player loop is suspended. Callers (the
    // Android proxy / iOS MonoPInvokeCallback) wrap this in try/catch for isolation.
    private static async void TriggerBridgeShareSign(string fingerprint)
    {
        string jwt = null;
        bool failed = _activeBridgeShareSigner == null;
        if (!failed)
        {
            try
            {
                jwt = await _activeBridgeShareSigner.Invoke(fingerprint);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                failed = true;
            }
        }
        // A valid JWT signs the post. Any failure (no signer, the host threw, or an empty result)
        // tells native the host could NOT sign, so it aborts the post with a clean error instead of
        // publishing an unsigned image — which a picture-restricted community rejects and which would
        // otherwise leave the editor's publish spinner stuck.
        if (failed || string.IsNullOrEmpty(jwt))
            FailBridgeShareSignature();
        else
            SetBridgeShareSignature(jwt);
    }

    // Success: hand the JWT to native (iOS resumes the sign closure returning it; Android completes
    // its provider deferred with it).
    private static void SetBridgeShareSignature(string jwt)
    {
#if UNITY_EDITOR
        Mock.RecordBridgeShareSignature(jwt);
#elif UNITY_ANDROID
        EnsureJniAttached();
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("setBridgeShareSignature", jwt);
        }
#elif UNITY_IOS
        OctopusSdkSetBridgeShareSignature(jwt);
#endif
    }

    // Failure: signal "couldn't sign". iOS makes the sign closure THROW (the SDK aborts the post
    // cleanly). Android signals it by returning null to its provider (empty maps to null), the
    // platform's "unsigned" value.
    private static void FailBridgeShareSignature()
    {
#if UNITY_EDITOR
        Mock.RecordBridgeShareSignatureFailed();
#elif UNITY_ANDROID
        EnsureJniAttached();
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("setBridgeShareSignature", "");
        }
#elif UNITY_IOS
        OctopusSdkFailBridgeShareSignature();
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // A lane reply (sign/token) can resume on a .NET thread-pool thread when the host provider is
    // async (e.g. an HttpClient call) — that thread is NOT attached to the JVM, so a JNI call from
    // it is dropped and the native await hangs forever. Attach the current thread first; this is
    // idempotent and a no-op on the already-attached Unity main / JNI callback threads.
    // We deliberately do NOT detach: the thread pool is bounded and long-lived, so leaving these
    // threads attached costs a JNIEnv per pooled thread for the process lifetime — safer than
    // detaching a thread that may be reused mid-flight for another attached JNI operation.
    internal static void EnsureJniAttached() => AndroidJNI.AttachCurrentThread();
#endif

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkSetBridgeShareSignature(string jwt);

    [DllImport("__Internal")]
    private static extern void OctopusSdkFailBridgeShareSignature();
#endif
}
