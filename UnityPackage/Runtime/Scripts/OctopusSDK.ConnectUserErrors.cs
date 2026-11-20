using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public partial class OctopusSDK
{
    private sealed class ConnectUserCallbacks
    {
        internal Action Success;
        internal Action<OctopusClientUserError> Error;
        internal volatile OctopusClientUserError TokenError;
        internal bool PostToMainThread;
    }

    // Read the connection id and provider together: a new connect must never lend its provider
    // to an older native request. Stop/Initialize already release TokenProvider by setting null.
    private static readonly object _tokenProviderLock = new object();
    private static int _tokenProviderConnectionId;

    internal static Func<Task<string>> TokenProviderForConnection(int id)
    {
        lock (_tokenProviderLock)
        {
            return id == _tokenProviderConnectionId ? TokenProvider : null;
        }
    }

    private static int _nextConnectUserRequestId;
    private static readonly ConcurrentDictionary<int, ConnectUserCallbacks> _connectUserCallbacks
        = new ConcurrentDictionary<int, ConnectUserCallbacks>();

    /// <summary>Connects a user with typed success/error callbacks.
    /// Call on the Unity main thread after Initialize in SSO mode. Serialize connection calls:
    /// the SDK retains one token provider for the session. Profile strings may be null.
    /// The provider must use loop-independent I/O and may run on a background thread, including
    /// during later token refreshes. Empty tokens fail with MissingToken; provider exceptions use Other.
    /// Token requests wait for the host provider without a deadline, including interactive login.
    /// On Android, stopping or replacing the SDK session cancels pending token requests.
    /// Callbacks run on a subsequent Unity update and wait while native UI pauses the game.
    /// Unclassified native and transport failures use Other. Android classifies the first validation error.</summary>
    public static void ConnectUser(string userId, string nickname, string bio, string picture,
        Func<Task<string>> tokenProvider, Action onSuccess, Action<OctopusClientUserError> onError)
    {
        ConnectUserCore(userId, nickname, bio, picture, tokenProvider, onSuccess, onError, true);
    }

    private static void ConnectUserCore(string userId, string nickname, string bio, string picture,
        Func<Task<string>> tokenProvider, Action onSuccess, Action<OctopusClientUserError> onError,
        bool postToMainThread)
    {
        OctopusMainThread.EnsureExists();
        int id = RegisterConnectUserCallbacks(onSuccess, onError, postToMainThread);
        if (string.IsNullOrEmpty(userId))
        {
            CompleteConnectUser(id, new OctopusClientUserError(
                OctopusClientUserErrorCode.Other, "A user id is required"));
            return;
        }
        if (tokenProvider == null)
        {
            CompleteConnectUser(id, new OctopusClientUserError(
                OctopusClientUserErrorCode.MissingToken, "A token provider is required"));
            return;
        }
        // Retained for native refresh requests after the initial connect, just like the legacy API.
        Func<Task<string>> provider = () => GetConnectUserToken(id, tokenProvider);
        lock (_tokenProviderLock)
        {
            TokenProvider = provider;
            _tokenProviderConnectionId = id;
        }
        try
        {
#if UNITY_EDITOR
            MockBackend.ConnectUserWithResult(id, userId, nickname ?? "", bio ?? "", picture ?? "", provider);
#elif UNITY_ANDROID
            using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
            {
                plugin.CallStatic("connectUserWithResult", id, userId, nickname ?? "", bio ?? "", picture ?? "");
            }
#elif UNITY_IOS
            OctopusSdkConnectUserWithResult(id, userId, nickname ?? "", bio ?? "", picture ?? "");
#else
            CompleteConnectUser(id, new OctopusClientUserError(
                OctopusClientUserErrorCode.Other, "ConnectUser requires Android or iOS"));
#endif
        }
        catch (Exception e)
        {
            CompleteConnectUser(id, new OctopusClientUserError(OctopusClientUserErrorCode.Other, e.Message));
        }
    }

    private static async Task<string> GetConnectUserToken(int id, Func<Task<string>> provider)
    {
        try
        {
            string token = await provider().ConfigureAwait(false);
            if (!string.IsNullOrEmpty(token)) return token;
            RecordConnectUserTokenError(id, new OctopusClientUserError(
                OctopusClientUserErrorCode.MissingToken, "The token provider returned no token"));
        }
        catch (Exception e)
        {
            RecordConnectUserTokenError(id, new OctopusClientUserError(OctopusClientUserErrorCode.Other, e.Message));
        }
        // Always answer native so a failed provider cannot strand its continuation.
        return "";
    }

    private static void RecordConnectUserTokenError(int id, OctopusClientUserError error)
    {
        ConnectUserCallbacks callbacks;
        if (_connectUserCallbacks.TryGetValue(id, out callbacks)) callbacks.TokenError = error;
    }

    internal static int RegisterConnectUserCallbacks(Action success, Action<OctopusClientUserError> error,
        bool postToMainThread = true)
    {
        int id = Interlocked.Increment(ref _nextConnectUserRequestId);
        _connectUserCallbacks[id] = new ConnectUserCallbacks
        {
            Success = success, Error = error, PostToMainThread = postToMainThread
        };
        return id;
    }

    internal static void CompleteConnectUser(int id, OctopusClientUserError error)
    {
        ConnectUserCallbacks callbacks;
        if (!_connectUserCallbacks.TryRemove(id, out callbacks)) return;
        // Wait for native completion even when token acquisition failed: iOS can connect as a
        // guest after an empty token. That fallback must not report success for the requested user.
        error = callbacks.TokenError ?? error;
        Action complete = () =>
        {
            if (error == null) callbacks.Success?.Invoke();
            else callbacks.Error?.Invoke(error);
        };
        // Task registrations only complete their TCS here; user continuations run asynchronously.
        // Callback registrations retain their documented next-Unity-update delivery.
        if (callbacks.PostToMainThread) OctopusMainThread.Post(complete);
        else complete();
    }

    internal static void ReceiveConnectUserResult(string payload, bool failed)
    {
        int id;
        string json;
        if (string.IsNullOrEmpty(payload) || !SplitRequest(payload, out id, out json) || id <= 0) return;
        CompleteConnectUser(id, failed ? OctopusClientUserErrorParsing.FromJson(json) : null);
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        /// <summary>Native typed-connect success envelope: request id followed by a newline.</summary>
        public void OnConnectUserSucceeded(string payload) { ReceiveConnectUserResult(payload, false); }
        /// <summary>Native typed-connect error envelope: request id, newline, code/message JSON.</summary>
        public void OnConnectUserFailed(string payload) { ReceiveConnectUserResult(payload, true); }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private partial class OctopusBridgeListenerProxy
    {
        /// <summary>Android typed-connect success envelope.</summary>
        public void onConnectUserSucceeded(string payload) { ReceiveConnectUserResult(payload, false); }
        /// <summary>Android typed-connect error envelope.</summary>
        public void onConnectUserFailed(string payload) { ReceiveConnectUserResult(payload, true); }
    }
#endif

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkConnectUserWithResult(int requestId, string userId,
        string nickname, string bio, string picture);
#endif
}
