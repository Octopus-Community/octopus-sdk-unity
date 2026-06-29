using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public partial class OctopusSDK
{
    private static Func<Task<string>> TokenProvider;
    private static TaskCompletionSource<bool> ConnectUserTaskCompleter;
    /// <summary>
    /// Connects a user. <paramref name="tokenProvider"/> mints a fresh client token on demand from
    /// your backend.
    ///
    /// IMPORTANT: it may be invoked on a BACKGROUND THREAD while the game loop is suspended (the
    /// Octopus UI is treated like backgrounding the game, and the SDK re-pulls the token mid-session
    /// when the JWT expires). It must therefore complete WITHOUT the Unity player loop: use
    /// loop-independent I/O (System.Net.Http), never UnityWebRequest, and do not await continuations
    /// that marshal back to the main thread. Do not cache tokens — mint on demand.
    /// </summary>
    public static async Task ConnectUser(string userId, string nickname, string bio, string picture, Func<Task<string>> tokenProvider)
    {
        TokenProvider = tokenProvider;
        ConnectUserTaskCompleter = new TaskCompletionSource<bool>();
#if UNITY_EDITOR
        MockBackend.ConnectUser(userId, nickname, bio, picture);
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("connectUser", userId, nickname, bio, picture);
        }
#elif UNITY_IOS
        OctopusSdkConnectUser(userId, nickname, bio, picture);
#endif
        await ConnectUserTaskCompleter.Task;
    }

    private static void TriggerOnConnectUserCompleted()
    {
        ConnectUserTaskCompleter?.SetResult(true);
    }

    private static async void TriggerOnTokenRequested()
    {
        if (TokenProvider != null)
        {
            string token = await TokenProvider.Invoke();
            SetUserToken(token);
        }
        else
        {
            throw new Exception("OctopusSDK requested a token but host app has not set a token provider function");
        }
    }

    private static void SetUserToken(string token)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Async token providers resume on a thread-pool thread not attached to the JVM; attach
        // before the JNI reply or it is dropped and the native connect await hangs. See
        // OctopusSDK.EnsureJniAttached (idempotent on the main/JNI threads).
        EnsureJniAttached();
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("setUserToken", token);
        }
#elif UNITY_IOS && !UNITY_EDITOR
        OctopusSdkSetUserToken(token);
#endif
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        public void OnTokenRequested(string _)
        {
            OctopusSDK.TriggerOnTokenRequested();
        }

        public void OnConnectUserCompleted(string _)
        {
            OctopusSDK.TriggerOnConnectUserCompleted();
        }
    }
    
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkConnectUser(string userId, string nickname, string bio, string picture);

    [DllImport("__Internal")]
    private static extern void OctopusSdkSetUserToken(string token);
#endif
}