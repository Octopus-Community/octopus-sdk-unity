using UnityEngine;

/// <summary>Restores the sample's visible system bars on launch, theme changes and native return.</summary>
public class OctopusSampleSystemBars : MonoBehaviour
{
    private void OnEnable()
    {
        OctopusSampleBranding.ThemeChanged += Apply;
        Apply();
    }

    private void OnDisable() { OctopusSampleBranding.ThemeChanged -= Apply; }
    private void OnApplicationFocus(bool focused) { if (focused) Apply(); }
    private void OnApplicationPause(bool paused) { if (!paused) Apply(); }

    private void Apply()
    {
        if (!Application.isPlaying) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        Screen.fullScreen = false;
        int bottom = Argb(OctopusSampleBranding.Palette.TabBar);
        int legacyBottom = Argb(OctopusSampleBranding.DarkSurfaceLow);
        bool lightNavigation = OctopusSampleBranding.Theme == OctopusSampleTheme.Light;
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                using (var callbackPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var callbackActivity = callbackPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var window = callbackActivity.Call<AndroidJavaObject>("getWindow"))
                using (var decor = window.Call<AndroidJavaObject>("getDecorView"))
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    int api = version.GetStatic<int>("SDK_INT");
                    window.Call("clearFlags", 1024); // FLAG_FULLSCREEN
                    window.Call("addFlags", unchecked((int)0x80000000)); // Draw system bar backgrounds.
                    // Transparent status bar over a player laid out beneath it, on every API: below
                    // 35 the window is not edge-to-edge by default, and an opaque bar would cut the
                    // dark halo at its lower edge and stop the page colour short of the screen top.
                    // API 35+ enforces this layout and ignores both calls. SampleUiSafeArea already
                    // pads the header below the bar, whichever layout the window ends up with.
                    window.Call("setStatusBarColor", 0);
                    window.Call("setNavigationBarColor", api >= 26 ? bottom : legacyBottom);
                    int flags = decor.Call<int>("getSystemUiVisibility");
                    // Visible bars, white status icons on navy/ink, navigation icons from app theme.
                    flags &= ~(4 | 2 | 4096 | 2048 | 8192 | 16);
                    flags |= 1024 | 256; // SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN | SYSTEM_UI_FLAG_LAYOUT_STABLE
                    if (api >= 26 && lightNavigation) flags |= 16;
                    decor.Call("setSystemUiVisibility", flags);
                    if (api >= 29)
                    {
                        window.Call("setStatusBarContrastEnforced", false);
                        window.Call("setNavigationBarContrastEnforced", false);
                    }
                    if (api >= 30)
                    {
                        using (var controller = window.Call<AndroidJavaObject>("getInsetsController"))
                        {
                            controller.Call("show", 3); // statusBars | navigationBars
                            controller.Call("setSystemBarsAppearance", lightNavigation ? 16 : 0, 8 | 16);
                        }
                    }
                }
            }));
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static int Argb(Color color)
    {
        Color32 bytes = color;
        return unchecked((int)(0xff000000u | (uint)bytes.r << 16 | (uint)bytes.g << 8 | bytes.b));
    }
#endif
}
