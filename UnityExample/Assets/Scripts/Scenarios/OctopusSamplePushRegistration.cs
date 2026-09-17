using System;

/// <summary>Shared token registration gate for the sample's notification callbacks and pilots.</summary>
public static class OctopusSamplePushRegistration
{
    public const string SkippedHeadline = "Push registration skipped by the toggle";

    private static string _lastToken;
    private static Action<string> _registerToken;
    private static bool _pending;

    static OctopusSamplePushRegistration()
    {
        // Tokens can arrive before the first scenario initialises the SDK.
        OctopusSampleState.Changed += OnStateChanged;
    }

    /// <summary>
    /// Caches a token and forwards it only when enabled and initialised. Call on the Unity main thread.
    /// The optional sender lets EditMode tests observe forwarding without reaching the native SDK.
    /// Tokens are never included in the console log.
    /// </summary>
    public static bool Register(string token, Action<string> registerToken = null)
    {
        if (string.IsNullOrEmpty(token)) return false;
        _lastToken = token;
        _registerToken = registerToken;
        _pending = true;
        if (!OctopusSampleFeatureToggles.PushRegistration)
        {
            OctopusSampleLog.Current.LogStateChange(SkippedHeadline,
                OctopusSampleFeatureToggles.PushRegistrationEffect(false));
            return false;
        }

        return ForwardPendingToken();
    }

    /// <summary>Replays the latest token when push registration is enabled again.</summary>
    public static bool RegisterCachedToken()
    {
        if (string.IsNullOrEmpty(_lastToken))
        {
            OctopusSampleLog.Current.LogStateChange("Push registration: device token is not available yet.");
            return false;
        }
        _pending = true;
        return ForwardPendingToken();
    }

    private static void OnStateChanged()
    {
        ForwardPendingToken();
    }

    private static bool ForwardPendingToken()
    {
        if (!_pending || !OctopusSampleState.IsInitialized ||
            !OctopusSampleFeatureToggles.PushRegistration) return false;

        _pending = false;
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.RegisterNotificationsToken");
        if (_registerToken != null) _registerToken(_lastToken);
        else OctopusSDK.RegisterNotificationsToken(_lastToken);
        return true;
    }

    /// <summary>Clears the token and test sender between EditMode tests.</summary>
    public static void Reset()
    {
        _lastToken = null;
        _registerToken = null;
        _pending = false;
    }
}
