using System;
using UnityEngine;

/// <summary>
/// Whether this Android build carries a Firebase configuration at all. The config files are
/// gitignored (each developer brings their own Firebase project), so a fresh clone — a QA
/// workspace, a CI build — produces an APK without the <c>google_app_id</c> resource. Touching
/// Firebase in that build logs a Firebase error that a development build paints over every screen.
/// Without a config the sample skips Firebase and says so once, as a warning; with one, every
/// Firebase failure is still reported as before.
/// </summary>
public static class OctopusSampleFirebaseConfig
{
    public const string MissingConfigWarning =
        "[Octopus Sample] Firebase is not configured in this build (no google_app_id resource: " +
        "google-services.json was absent at build time). Android push registration and " +
        "notification taps are off; the rest of the sample is unaffected.";

    /// <summary>Test seam: answers whether the Firebase options resource exists.</summary>
    internal static Func<bool> Probe = HasGoogleAppIdResource;

    private static bool? _configured;

    /// <summary>
    /// True when Firebase may be initialised. A missing config logs <see cref="MissingConfigWarning"/>
    /// once per process; a probe that cannot answer counts as configured, so Firebase itself
    /// reports whatever is wrong.
    /// </summary>
    public static bool IsConfigured()
    {
        if (_configured.HasValue) return _configured.Value;
        bool configured;
        try { configured = Probe(); }
        catch (Exception) { configured = true; }
        _configured = configured;
        if (!configured) Debug.LogWarning(MissingConfigWarning);
        return configured;
    }

    internal static void Reset()
    {
        _configured = null;
        Probe = HasGoogleAppIdResource;
    }

    private static bool HasGoogleAppIdResource()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var resources = activity.Call<AndroidJavaObject>("getResources"))
        {
            return resources.Call<int>("getIdentifier", "google_app_id", "string",
                                       activity.Call<string>("getPackageName")) != 0;
        }
#else
        return true;
#endif
    }
}
