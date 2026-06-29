using System;
using System.Runtime.InteropServices;
using UnityEngine;

// Strategy the host returns from NavigateToUrlHandler to decide who opens a URL.
public enum UrlOpeningStrategy
{
    HandledByApp,      // the host app handled it; Octopus does nothing
    HandledByOctopus,  // Octopus opens it in the device's system browser
}

public partial class OctopusSDK
{
    private static Func<string, UrlOpeningStrategy> _navigateToUrlHandler;

    // Set a handler to intercept URLs tapped inside Octopus (CTA buttons and post links).
    // When null (default) interception is off and Octopus opens every URL in its own
    // in-app browser, as before. When a handler is set and it returns HandledByOctopus,
    // the URL is opened in the device's system browser instead.
    public static Func<string, UrlOpeningStrategy> NavigateToUrlHandler
    {
        get => _navigateToUrlHandler;
        set
        {
            _navigateToUrlHandler = value;
            SetUrlInterceptionEnabled(value != null);
        }
    }

    // Pure, testable: the strategy for a URL given the current handler.
    internal static UrlOpeningStrategy ResolveUrlStrategy(string url)
    {
        var handler = _navigateToUrlHandler;
        return handler == null ? UrlOpeningStrategy.HandledByOctopus : handler(url);
    }

    // Wire form of ResolveUrlStrategy for the native bridge: matches UrlOpeningStrategy ordinals
    // (HandledByApp = 0, HandledByOctopus = 1). Pure + Editor-testable; the AndroidJavaProxy that
    // calls this is compiled only in Android player builds.
    internal static int ResolveUrlStrategyCode(string url) => (int)ResolveUrlStrategy(url);

    private static void SetUrlInterceptionEnabled(bool enabled)
    {
#if UNITY_EDITOR
        MockBackend.SetUrlInterceptionEnabled(enabled);
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("setUrlInterceptionEnabled", enabled);
        }
#elif UNITY_IOS
        OctopusSdkSetUrlInterceptionEnabled(enabled ? 1 : 0);
#endif
    }

    private static void OpenUrlInOctopus(string url)
    {
#if UNITY_EDITOR
        MockBackend.OpenUrlInOctopus(url);
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("openUrlInOctopus", url ?? "");
        }
#elif UNITY_IOS
        OctopusSdkOpenUrlInOctopus(url ?? "");
#endif
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        // iOS / Lane-B path: native forwards a tapped URL here via UnitySendMessage while
        // interception is enabled. On Android the decision is made synchronously in the bridge
        // (OctopusBridgeListener.resolveUrlStrategy), so this method is not reached there.
        public void OnNavigateToUrl(string url)
        {
            if (ResolveUrlStrategy(url) == UrlOpeningStrategy.HandledByOctopus)
            {
                OpenUrlInOctopus(url);
            }
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkSetUrlInterceptionEnabled(int enabled);

    [DllImport("__Internal")]
    private static extern void OctopusSdkOpenUrlInOctopus(string url);
#endif
}
