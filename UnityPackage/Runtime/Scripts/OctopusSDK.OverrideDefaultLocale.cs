using System.Runtime.InteropServices;
using UnityEngine;

public partial class OctopusSDK
{
    /// <summary>
    /// Overrides the default locale used by the SDK (e.g. "fr", "en-US").
    /// </summary>
    public static void OverrideDefaultLocale(string languageCode)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("overrideDefaultLocale", languageCode);
        }
#elif UNITY_IOS && !UNITY_EDITOR
        OctopusSdkOverrideDefaultLocale(languageCode);
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkOverrideDefaultLocale(string languageCode);
#endif
}
