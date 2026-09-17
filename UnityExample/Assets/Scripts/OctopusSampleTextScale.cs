using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>Sample-only system text preference. Tests restore Provider after overriding it.</summary>
public static class OctopusSampleTextScale
{
    public static Func<float> Provider { get; set; }
    private static float _systemScale = 1f;
    private static bool _loaded;
    private static float _nextRefresh;

    public static float Current
    {
        get
        {
            if (Provider != null) return Normalize(Provider());
            if (!_loaded || (Application.isPlaying && Time.unscaledTime >= _nextRefresh)) Refresh();
            return _systemScale;
        }
    }

    public static float Normalize(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ? 1f : Mathf.Max(1f, value);
    }

    public static void Refresh()
    {
        _loaded = true;
        _nextRefresh = Time.unscaledTime + 1f;
        _systemScale = 1f;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                if (activity != null)
                {
                    using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
                    using (var settings = new AndroidJavaClass("android.provider.Settings$System"))
                        _systemScale = Normalize(settings.CallStatic<float>("getFloat", resolver, "font_scale", 1f));
                }
            }
        }
        // Best effort: any failure to read the preference keeps the sample's 1.0 default.
        catch (Exception) { }
#elif UNITY_IOS && !UNITY_EDITOR
        _systemScale = Normalize(OctopusSampleReadTextScale());
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern float OctopusSampleReadTextScale();
#endif
}
