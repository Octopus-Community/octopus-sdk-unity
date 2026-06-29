using System.Runtime.InteropServices;
using UnityEngine;

public partial class OctopusSDK
{
    // Opens the Octopus UI. Pass a notification to navigate to its content
    // (post / comment / reply / group), or null for the main feed.
    public static void Open(OctopusNotification notification = null)
    {
#if UNITY_EDITOR
        MockBackend.Open(notification);
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("open", notification?.DeepLink ?? "");
        }
#elif UNITY_IOS
        // Forward the octopus payload as JSON; native wraps it as userInfo["data"]
        // and lets OctopusHomeScreen(notificationUserInfo:) navigate. Empty = main feed.
        OctopusSdkOpen(notification != null ? notification.DataAsJson : "");
#endif
    }

    // Opens the Octopus UI directly on a specific group's feed.
    public static void OpenGroup(string groupId)
    {
#if UNITY_EDITOR
        MockBackend.OpenGroup(groupId);
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("openGroup", groupId ?? "");
        }
#elif UNITY_IOS
        OctopusSdkOpenGroup(groupId ?? "");
#endif
    }

    // Opens the Octopus UI directly on a specific post's detail screen.
    // An empty/null postId falls back to the main feed.
    public static void OpenPost(string postId)
    {
#if UNITY_EDITOR
        MockBackend.OpenPost(postId);
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("openPost", postId ?? "");
        }
#elif UNITY_IOS
        OctopusSdkOpenPost(postId ?? "");
#endif
    }

    // Opens the Octopus UI on the post editor, optionally prefilled.
    // Pass null (or an all-empty prefill) to open a blank editor.
    public static void OpenCreatePost(OctopusPrefilledPost prefilled = null)
    {
        OctopusPrefilledPostMarshal.ToArgs(prefilled, out string text, out string topicId,
            out string imagePath, out string ctaLabel, out string ctaUrl);
        StashActiveBridgeShareSigner(prefilled);
#if UNITY_EDITOR
        MockBackend.OpenCreatePost(prefilled);
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("openCreatePost", text, topicId, imagePath, ctaLabel, ctaUrl,
                prefilled?.SignBridgeShare != null);
        }
#elif UNITY_IOS
        OctopusSdkOpenCreatePost(text, topicId, imagePath, ctaLabel, ctaUrl,
            prefilled?.SignBridgeShare != null ? 1 : 0);
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkOpen(string payloadJson);

    [DllImport("__Internal")]
    private static extern void OctopusSdkOpenGroup(string groupId);

    [DllImport("__Internal")]
    private static extern void OctopusSdkOpenPost(string postId);

    [DllImport("__Internal")]
    private static extern void OctopusSdkOpenCreatePost(
        string text, string topicId, string imagePath, string ctaLabel, string ctaUrl, int hasSigner);
#endif
}
