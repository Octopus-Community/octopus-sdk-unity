using System;
using System.Runtime.InteropServices;
using UnityEngine;

// Reads platform appearance and the iOS build number from the sample, without changing the SDK.
// iOS goes through the sample-only Plugins/iOS/OctopusSampleSystemInfo.mm shim.
public static class OctopusSampleSystemAppearance
{
    internal static Func<OctopusSampleTheme> Reader;
    public static OctopusSampleTheme Read()
    {
        if (Reader != null) return Reader();
#if UNITY_ANDROID && !UNITY_EDITOR
        // The activity declares uiMode in configChanges, so read the process-wide night mode
        // first: it moves with the OS toggle even when the activity configuration lags behind.
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                using (var manager = activity.Call<AndroidJavaObject>("getSystemService", "uimode"))
                {
                    int mode = manager == null ? 0 : manager.Call<int>("getNightMode");
                    if (mode == 2) return OctopusSampleTheme.Dark;
                    if (mode == 1) return OctopusSampleTheme.Light;
                }
                using (var resources = new AndroidJavaClass("android.content.res.Resources"))
                using (var system = resources.CallStatic<AndroidJavaObject>("getSystem"))
                using (var config = system.Call<AndroidJavaObject>("getConfiguration"))
                    return (config.Get<int>("uiMode") & 0x30) == 0x20 ? OctopusSampleTheme.Dark : OctopusSampleTheme.Light;
            }
        }
        catch (Exception)
        {
            return OctopusSampleTheme.Light;
        }
#elif UNITY_IOS && !UNITY_EDITOR
        try { return OctopusSampleReadInterfaceStyle() == 2 ? OctopusSampleTheme.Dark : OctopusSampleTheme.Light; }
        catch (Exception) { return OctopusSampleTheme.Light; }
#elif UNITY_EDITOR
        return UnityEditor.EditorGUIUtility.isProSkin ? OctopusSampleTheme.Dark : OctopusSampleTheme.Light;
#else
        return OctopusSampleTheme.Light;
#endif
    }

    /// <summary>The iOS CFBundleVersion, or null off iOS or when it cannot be read.</summary>
    public static string IosBundleVersion()
    {
#if UNITY_IOS && !UNITY_EDITOR
        try { return OctopusSampleReadBundleVersion(); }
        catch (Exception) { return null; }
#else
        return null;
#endif
    }
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int OctopusSampleReadInterfaceStyle();
    [DllImport("__Internal")]
    private static extern string OctopusSampleReadBundleVersion();
#endif
}
