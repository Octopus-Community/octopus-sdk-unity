using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>Failure categories returned by the native entitlements refresh.</summary>
public enum OctopusRefreshEntitlementsErrorKind
{
    /// <summary>Refresh requires SSO mode and a client token provider.</summary>
    NoClientTokenProvider,
    /// <summary>No connected non-guest user.</summary>
    UserNotConnected,
    /// <summary>No network connection.</summary>
    NoNetwork,
    /// <summary>The user is banned; Message contains the backend's displayable explanation.</summary>
    UserBanned,
    /// <summary>Backend, bridge, or unrecognized failure.</summary>
    ServerError
}

/// <summary>A typed entitlements refresh failure preserving the native message.</summary>
public sealed class OctopusRefreshEntitlementsError
{
    /// <summary>The failure category.</summary>
    public OctopusRefreshEntitlementsErrorKind Kind { get; private set; }
    /// <summary>The native error message; empty when unavailable.</summary>
    public string Message { get; private set; }
    /// <summary>Creates a refresh error, including for editor simulation.</summary>
    public OctopusRefreshEntitlementsError(OctopusRefreshEntitlementsErrorKind kind, string message = null)
    {
        Kind = kind;
        Message = message ?? "";
    }

    internal static OctopusRefreshEntitlementsError FromJson(string json)
    {
        try
        {
            var fields = OctopusJson.ParseObject(json);
            string type;
            fields.TryGetValue("type", out type);
            OctopusRefreshEntitlementsErrorKind kind;
            switch (type)
            {
                case "noClientTokenProvider": kind = OctopusRefreshEntitlementsErrorKind.NoClientTokenProvider; break;
                case "userNotConnected": kind = OctopusRefreshEntitlementsErrorKind.UserNotConnected; break;
                case "noNetwork": kind = OctopusRefreshEntitlementsErrorKind.NoNetwork; break;
                case "userBanned": kind = OctopusRefreshEntitlementsErrorKind.UserBanned; break;
                default: kind = OctopusRefreshEntitlementsErrorKind.ServerError; break;
            }
            string message;
            fields.TryGetValue("message", out message);
            return new OctopusRefreshEntitlementsError(kind, message);
        }
        catch (Exception)
        {
            return new OctopusRefreshEntitlementsError(OctopusRefreshEntitlementsErrorKind.ServerError);
        }
    }
}

public partial class OctopusSDK
{
    private sealed class EntitlementsRequest
    {
        internal Action Success;
        internal Action<OctopusRefreshEntitlementsError> Error;
    }
    private static int _nextEntitlementsRequestId;
    private static readonly Dictionary<int, EntitlementsRequest> _entitlementsRequests = new Dictionary<int, EntitlementsRequest>();

    /// <summary>Requests a fresh SSO token and refreshes entitlements. Call on the Unity thread after Initialize.
    /// Each request completes once on the Unity thread. Profile changes arrive separately through OnProfileChanged.
    /// Delivery waits while the Unity player loop is paused.</summary>
    public static void RefreshEntitlements(Action onSuccess, Action<OctopusRefreshEntitlementsError> onError)
    {
        int id = RegisterEntitlementsRequest(onSuccess, onError);
        try
        {
#if UNITY_EDITOR
            MockBackend.RefreshEntitlements(id);
#elif UNITY_ANDROID
            using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
            {
                plugin.CallStatic("refreshEntitlements", id);
            }
#elif UNITY_IOS
            OctopusSdkRefreshEntitlements(id);
#else
            CompleteEntitlementsRequest(id, new OctopusRefreshEntitlementsError(OctopusRefreshEntitlementsErrorKind.ServerError, "Unsupported platform"));
#endif
        }
        catch (Exception e)
        {
            CompleteEntitlementsRequest(id, new OctopusRefreshEntitlementsError(OctopusRefreshEntitlementsErrorKind.ServerError, e.Message));
        }
    }

    internal static int RegisterEntitlementsRequest(Action success, Action<OctopusRefreshEntitlementsError> error)
    {
        int id = ++_nextEntitlementsRequestId;
        _entitlementsRequests.Add(id, new EntitlementsRequest { Success = success, Error = error });
        return id;
    }

    internal static void CompleteEntitlementsRequest(int id, OctopusRefreshEntitlementsError error)
    {
        OctopusMainThread.Post(() =>
        {
            EntitlementsRequest request;
            if (!_entitlementsRequests.TryGetValue(id, out request)) return;
            _entitlementsRequests.Remove(id);
            if (error == null) request.Success?.Invoke();
            else request.Error?.Invoke(error);
        });
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        /// <summary>Native refresh success landing pad, carrying request id followed by a newline.</summary>
        public void OnRefreshEntitlementsResult(string payload)
        {
            int id; string rest;
            if (payload != null && SplitRequest(payload, out id, out rest)) CompleteEntitlementsRequest(id, null);
        }
        /// <summary>Native refresh failure landing pad, carrying request id, newline, and typed error JSON.</summary>
        public void OnRefreshEntitlementsError(string payload)
        {
            int id; string json;
            if (payload != null && SplitRequest(payload, out id, out json))
                CompleteEntitlementsRequest(id, OctopusRefreshEntitlementsError.FromJson(json));
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkRefreshEntitlements(int requestId);
#endif
}
