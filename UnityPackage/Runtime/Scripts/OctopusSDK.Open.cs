using System.Runtime.InteropServices;
using UnityEngine;

public partial class OctopusSDK
{
    /// <summary>Opens the Octopus UI. Pass a notification to navigate to its content
    /// (post / comment / reply / group), or null for the main feed.</summary>
    public static void Open(OctopusNotification notification = null)
    {
        Open(notification, null);
    }

    /// <summary>Open the requested screen with an optional iOS navigation container.
    /// Null preserves the native default; Android ignores navigationMode.</summary>
    public static void Open(OctopusNotification notification = null, OctopusNavigationMode? navigationMode = null)
    {
        NavigationModeCode(navigationMode);
#if UNITY_EDITOR
        MockBackend.Open(notification, navigationMode);
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("open", notification?.DeepLink ?? "", NavigationModeCode(navigationMode));
        }
#elif UNITY_IOS
        // Forward the octopus payload as JSON; native wraps it as userInfo["data"]
        // and lets OctopusHomeScreen(notificationUserInfo:) navigate. Empty = main feed.
        OctopusSdkOpen(notification != null ? notification.DataAsJson : "", NavigationModeCode(navigationMode));
#endif
    }

    /// <summary>Open the main feed with the native default navigation container.</summary>
    public static void Open()
    {
        Open(null, null);
    }

    /// <summary>Open a blank post editor with the native default navigation container.</summary>
    public static void OpenCreatePost()
    {
        OpenCreatePost(null, null);
    }

    /// <summary>Opens the Octopus UI directly on a specific group's feed.</summary>
    public static void OpenGroup(string groupId)
    {
        OpenGroup(groupId, null);
    }

    /// <summary>Open the requested screen with an optional iOS navigation container.
    /// Null preserves the native default; Android ignores navigationMode.</summary>
    public static void OpenGroup(string groupId, OctopusNavigationMode? navigationMode = null)
    {
        NavigationModeCode(navigationMode);
#if UNITY_EDITOR
        MockBackend.OpenGroup(groupId, navigationMode);
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("openGroup", groupId ?? "", NavigationModeCode(navigationMode));
        }
#elif UNITY_IOS
        OctopusSdkOpenGroup(groupId ?? "", NavigationModeCode(navigationMode));
#endif
    }

    /// <summary>Opens the Octopus UI directly on a specific post's detail screen.
    /// An empty/null postId falls back to the main feed.</summary>
    public static void OpenPost(string postId)
    {
        OpenPost(postId, null);
    }

    /// <summary>Open the requested screen with an optional iOS navigation container.
    /// Null preserves the native default; Android ignores navigationMode.</summary>
    public static void OpenPost(string postId, OctopusNavigationMode? navigationMode = null)
    {
        NavigationModeCode(navigationMode);
#if UNITY_EDITOR
        MockBackend.OpenPost(postId, navigationMode);
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("openPost", postId ?? "", NavigationModeCode(navigationMode));
        }
#elif UNITY_IOS
        OctopusSdkOpenPost(postId ?? "", NavigationModeCode(navigationMode));
#endif
    }

    /// <summary>Opens the Octopus UI on the post editor, optionally prefilled.
    /// Pass null (or an all-empty prefill) to open a blank editor.</summary>
    public static void OpenCreatePost(OctopusPrefilledPost prefilled = null)
    {
        OpenCreatePost(prefilled, null);
    }

    /// <summary>Open the requested screen with an optional iOS navigation container.
    /// Null preserves the native default; Android ignores navigationMode.</summary>
    public static void OpenCreatePost(OctopusPrefilledPost prefilled = null, OctopusNavigationMode? navigationMode = null)
    {
        NavigationModeCode(navigationMode);
        OctopusPrefilledPostMarshal.ToArgs(prefilled, out string text, out string topicId,
            out string imagePath, out string ctaLabel, out string ctaUrl);
        StashActiveBridgeShareSigner(prefilled);
#if UNITY_EDITOR
        MockBackend.OpenCreatePost(prefilled, navigationMode);
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("openCreatePost", text, topicId, imagePath, ctaLabel, ctaUrl,
                prefilled?.SignBridgeShare != null, NavigationModeCode(navigationMode));
        }
#elif UNITY_IOS
        OctopusSdkOpenCreatePost(text, topicId, imagePath, ctaLabel, ctaUrl,
            prefilled?.SignBridgeShare != null ? 1 : 0, NavigationModeCode(navigationMode));
#endif
    }

    /// <summary>Open the connected user's profile when clientUserId is null or blank.
    /// Otherwise clientUserId is the host app's client user ID (not an Octopus profile ID),
    /// requiring a community that exposes client user IDs. Unknown IDs show native unavailable UI.
    /// Null navigationMode preserves the native profile default (`NavigationStack`); Android ignores it.</summary>
    public static void OpenProfile(string clientUserId = null, OctopusNavigationMode? navigationMode = null)
    {
        NavigationModeCode(navigationMode);
#if UNITY_EDITOR
        MockBackend.OpenProfile(clientUserId, navigationMode);
#elif UNITY_ANDROID
        using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("openProfile", clientUserId ?? "", NavigationModeCode(navigationMode));
        }
#elif UNITY_IOS
        OctopusSdkOpenProfile(clientUserId ?? "", NavigationModeCode(navigationMode));
#endif
    }

    /// <summary>Open the connected user's activity. Without a connected profile, open the main feed.
    /// Null navigationMode preserves the native home-screen default; Android ignores it.</summary>
    public static void OpenActivity(OctopusNavigationMode? navigationMode = null)
    {
        NavigationModeCode(navigationMode);
#if UNITY_EDITOR
        MockBackend.OpenActivity(navigationMode);
#elif UNITY_ANDROID
        using (var plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("openActivity", NavigationModeCode(navigationMode));
        }
#elif UNITY_IOS
        OctopusSdkOpenActivity(NavigationModeCode(navigationMode));
#endif
    }

    internal static int NavigationModeCode(OctopusNavigationMode? mode)
    {
        if (!mode.HasValue) return -1;
        if (mode != OctopusNavigationMode.NavigationStack && mode != OctopusNavigationMode.Automatic)
            throw new System.ArgumentOutOfRangeException("navigationMode");
        return (int)mode.Value;
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkOpen(string payloadJson, int navigationMode);

    [DllImport("__Internal")]
    private static extern void OctopusSdkOpenGroup(string groupId, int navigationMode);

    [DllImport("__Internal")]
    private static extern void OctopusSdkOpenPost(string postId, int navigationMode);

    [DllImport("__Internal")]
    private static extern void OctopusSdkOpenCreatePost(
        string text, string topicId, string imagePath, string ctaLabel, string ctaUrl, int hasSigner, int navigationMode);
    [DllImport("__Internal")]
    private static extern void OctopusSdkOpenProfile(string clientUserId, int navigationMode);

    [DllImport("__Internal")]
    private static extern void OctopusSdkOpenActivity(int navigationMode);
#endif
}
