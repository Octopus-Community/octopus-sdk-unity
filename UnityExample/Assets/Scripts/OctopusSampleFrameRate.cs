using UnityEngine;

/// <summary>
/// Pins the sample's render rate to the display, once, before the first scene loads.
///
/// Unity leaves <c>Application.targetFrameRate</c> at <c>-1</c> unless an application sets it, and
/// <c>-1</c> does not mean "as fast as the screen": on Android and iOS it means the platform
/// default, which is 30 fps. Both quality levels in <c>QualitySettings.asset</c> ship
/// <c>vSyncCount: 0</c>, so nothing else raised it either — the sample was rendering every scroll
/// frame on a 33 ms cadence on a 60, 90 or 120 Hz panel. That is the "laggy, and the scroll feels
/// strange" of issue #294: an inertial fling sampled at 30 Hz reads as stepped rather than slow.
///
/// This type only states the intent that was missing. It deliberately does not clamp for battery,
/// throttle in the background or expose a setting: the sample exists to show the SDK's screens at
/// the rate a real host would draw them.
///
/// On iOS, going above 60 also needs <c>CADisableMinimumFrameDuration</c> in the generated
/// <c>Info.plist</c>; without it a ProMotion device stays at 60 whatever this asks for. That key is
/// not set in this project, so <see cref="Ceiling"/> is reachable on Android today and on iOS only
/// once the key lands — see issue #294.
/// </summary>
public static class OctopusSampleFrameRate
{
    /// <summary>What an unreadable or implausibly low refresh rate falls back to.</summary>
    public const int Fallback = 60;

    /// <summary>
    /// The highest rate the sample asks for. A sample that pegs a 165 Hz panel spends the battery
    /// of a device someone is doing QA on all day for frames nobody is reading.
    /// </summary>
    public const int Ceiling = 120;

    /// <summary>
    /// The rate to request for a display running at <paramref name="refreshRateHz"/>.
    ///
    /// Pure, so the policy is testable without a player: <c>Screen.currentResolution</c> reports
    /// nothing usable in batch mode, and every interesting case here is a number, not a device.
    /// </summary>
    public static int TargetFor(double refreshRateHz)
    {
        if (double.IsNaN(refreshRateHz) || double.IsInfinity(refreshRateHz) || refreshRateHz <= 0d)
        {
            return Fallback;
        }

        // Round before clamping: a 59.94 Hz panel must read as 60, not as 59.
        int rounded = (int)System.Math.Round(refreshRateHz);
        if (rounded <= Fallback)
        {
            // Also the floor for a genuine 48 Hz low-power mode: asking for 60 there costs nothing,
            // and it keeps a single bogus reading from pinning the sample below 60.
            return Fallback;
        }

        return rounded > Ceiling ? Ceiling : rounded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
#if UNITY_ANDROID || UNITY_IOS
        // targetFrameRate is only honoured while vSync is off. Both mobile quality levels already
        // ship vSyncCount: 0; the guard keeps this from dirtying QualitySettings.asset in the
        // Editor, where a play-mode write to an unchanged value still marks the asset.
        if (QualitySettings.vSyncCount != 0)
        {
            QualitySettings.vSyncCount = 0;
        }
#endif
        Application.targetFrameRate = TargetFor(Screen.currentResolution.refreshRateRatio.value);
    }
}
