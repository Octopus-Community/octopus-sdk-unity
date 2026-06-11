using System.Runtime.InteropServices;
using UnityEngine;

public partial class OctopusSDK
{
    /// <summary>
    /// Overrides the default locale used by the SDK (e.g. "fr", "en-US").
    /// </summary>
    public static void OverrideDefaultLocale(string languageCode)
    {
#if UNITY_EDITOR
        MockBackend.OverrideDefaultLocale(languageCode);
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("overrideDefaultLocale", languageCode);
        }
#elif UNITY_IOS
        OctopusSdkOverrideDefaultLocale(languageCode);
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkOverrideDefaultLocale(string languageCode);
#endif
}
