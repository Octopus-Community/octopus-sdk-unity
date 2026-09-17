using System;
using System.Runtime.InteropServices;
using UnityEngine;

public partial class OctopusSDK
{
    private static Action<string> _navigateToProfileHandler;

    /// <summary>Set a handler to intercept profile taps; null disables interception (default).
    /// The argument is the host app's client user ID, not an Octopus profile ID.
    /// The community must expose client user IDs. Native dismisses the UI before delivery
    /// on the Unity main thread. Members without a client user ID keep native navigation.</summary>
    public static Action<string> NavigateToProfileHandler
    {
        get { return _navigateToProfileHandler; }
        set
        {
            _navigateToProfileHandler = value;
            SetProfileInterceptionEnabled(value != null);
        }
    }

    /// <summary>Raised on the Unity main thread after an intercepted profile tap dismisses
    /// Octopus. The argument is the host app's client user ID. Set NavigateToProfileHandler
    /// to enable interception; subscribing to this event alone does not enable it.</summary>
    public static event Action<string> OnNavigateToProfile;

    internal static void ReceiveNavigateToProfile(string payload)
    {
        string clientUserId;
        try
        {
            var fields = OctopusJson.ParseObject(payload);
            if (!fields.TryGetValue("clientUserId", out clientUserId) ||
                string.IsNullOrWhiteSpace(clientUserId)) return;
        }
        catch (FormatException) { return; }
        QueueNavigateToProfile(clientUserId);
    }

    private static void QueueNavigateToProfile(string clientUserId)
    {
        // Capture the handler at receipt: replacing it must not reroute an already queued tap.
        var handler = _navigateToProfileHandler;
        var listeners = OnNavigateToProfile;
        OctopusMainThread.Post(() =>
        {
            try { if (handler != null) handler(clientUserId); }
            finally { if (listeners != null) listeners(clientUserId); }
        });
    }

    private static void SetProfileInterceptionEnabled(bool enabled)
    {
#if UNITY_EDITOR
        Mock.Record("SetProfileInterceptionEnabled", enabled);
#elif UNITY_ANDROID
        using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("setProfileInterceptionEnabled", enabled);
        }
#elif UNITY_IOS
        OctopusSdkSetProfileInterceptionEnabled(enabled ? 1 : 0);
#endif
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        /// <summary>Native profile-tap JSON landing pad.</summary>
        public void OnNavigateToProfile(string payload)
        {
            ReceiveNavigateToProfile(payload);
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkSetProfileInterceptionEnabled(int enabled);
#endif
}
