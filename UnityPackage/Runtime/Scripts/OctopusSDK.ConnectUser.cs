using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public partial class OctopusSDK
{
    private static Func<Task<string>> TokenProvider;
    /// <summary>
    /// Connects a user. Connection and token provider failures fault the returned Task with
    /// InvalidOperationException. Token requests wait for the host provider without a deadline,
    /// including interactive login. On Android, stopping or replacing the SDK session cancels
    /// pending token requests. A null provider faults with InvalidOperationException ("A token
    /// provider is required"); use the callback overload for typed error codes. Await the Task;
    /// never block the main thread on it, since on iOS the native result arrives through a Unity message.
    /// <paramref name="tokenProvider"/> mints a fresh client token on demand from
    /// your backend. <paramref name="nickname"/>, <paramref name="bio"/> and <paramref name="picture"/>
    /// are optional — pass null (or "") for any you don't have.
    ///
    /// IMPORTANT: it may be invoked on a BACKGROUND THREAD while the game loop is suspended (the
    /// Octopus UI is treated like backgrounding the game, and the SDK re-pulls the token mid-session
    /// when the JWT expires). It must therefore complete WITHOUT the Unity player loop: use
    /// loop-independent I/O (System.Net.Http), never UnityWebRequest, and do not await continuations
    /// that marshal back to the main thread. Do not cache tokens — mint on demand.
    /// </summary>
    public static Task ConnectUser(string userId, string nickname, string bio, string picture, Func<Task<string>> tokenProvider)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            ConnectUserCore(userId, nickname, bio, picture, tokenProvider,
                () => completion.TrySetResult(true),
                error => completion.TrySetException(new InvalidOperationException(error.Message)), false);
        }
        catch (Exception e)
        {
            completion.TrySetException(e);
        }
        return completion.Task;
    }

    // Payload on Android: connection id, newline, token request id. iOS keeps its empty payload.
    private static async void TriggerOnTokenRequested(string payload = "")
    {
        int tokenRequestId = 0;
        Func<Task<string>> provider = TokenProvider;
#if UNITY_ANDROID && !UNITY_EDITOR
        int connectionId;
        string tokenId;
        if (string.IsNullOrEmpty(payload) || !SplitRequest(payload, out connectionId, out tokenId)
            || !int.TryParse(tokenId, out tokenRequestId) || tokenRequestId <= 0) return;
        provider = TokenProviderForConnection(connectionId);
#endif
        string token = "";
        try
        {
            if (provider != null) token = await provider().ConfigureAwait(false) ?? "";
        }
        catch (Exception)
        {
            // Always answer, including a synchronous provider throw. Never let async void throw
            // into the host process or leave the native coroutine suspended.
        }
        try
        {
            SetUserToken(token, tokenRequestId);
        }
        catch (Exception)
        {
            // A lifecycle change retires the native request if the JNI transport is unavailable.
        }
    }

    private static void SetUserToken(string token, int tokenRequestId)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        EnsureJniAttached();
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("setUserTokenForRequest", tokenRequestId, token);
        }
#elif UNITY_IOS && !UNITY_EDITOR
        OctopusSdkSetUserToken(token);
#endif
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        public void OnTokenRequested(string _)
        {
            OctopusSDK.TriggerOnTokenRequested(_);
        }

        /// <summary>Legacy native completion pad. Current connections use correlated success/error pads.</summary>
        public void OnConnectUserCompleted(string _)
        {
            // Retained for the bridge contract; no current Task is completed by this message.
        }
    }
    
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkConnectUser(string userId, string nickname, string bio, string picture);

    [DllImport("__Internal")]
    private static extern void OctopusSdkSetUserToken(string token);
#endif
}