using System;

/// <summary>
/// Hands the sample's brand palette to the native SDK and keeps the native appearance in step with
/// the sample theme (issue #134). <see cref="OctopusSampleBranding"/> owns the colors; this class
/// only maps them onto the `OctopusSDK.SetLightColorScheme` / `SetDarkColorScheme` / `SetColorSchemeType` seam, once per
/// initialisation, and re-sends the scheme type whenever Settings → Appearance flips the theme.
///
/// Sample only: nothing here is part of the package API, and every call goes through
/// <see cref="IOctopusScenarioSdk"/> so EditMode tests can record it instead of reaching the bridge.
/// </summary>
public static class OctopusSampleNativeTheme
{
    /// <summary>`SetColorSchemeType` value for a forced light appearance.</summary>
    public const int LightSchemeType = (int)OctopusThemeSettings.ColorSchemeType.Light;

    /// <summary>`SetColorSchemeType` value for a forced dark appearance.</summary>
    public const int DarkSchemeType = (int)OctopusThemeSettings.ColorSchemeType.Dark;

    // Process lifetime subscription to a process lifetime event; only the current initialized SDK
    // receives updates. A future SwitchCommunity scenario must re-apply after its asset theme pass.
    private static bool _following;

    /// <summary>The light scheme passed to the SDK, built from the TOKENS §4 colors.</summary>
    public static OctopusColorScheme LightScheme
    {
        get
        {
            return new OctopusColorScheme(
                OctopusSampleBranding.SdkLightPrimaryMain,
                OctopusSampleBranding.SdkLightPrimaryLow,
                OctopusSampleBranding.SdkLightPrimaryHigh,
                OctopusSampleBranding.SdkLightOnPrimary);
        }
    }

    /// <summary>The dark scheme passed to the SDK, built from the TOKENS §4 colors.</summary>
    public static OctopusColorScheme DarkScheme
    {
        get
        {
            return new OctopusColorScheme(
                OctopusSampleBranding.SdkDarkPrimaryMain,
                OctopusSampleBranding.SdkDarkPrimaryLow,
                OctopusSampleBranding.SdkDarkPrimaryHigh,
                OctopusSampleBranding.SdkDarkOnPrimary);
        }
    }

    /// <summary>
    /// The native scheme type matching <paramref name="theme"/>. The sample has no "system"
    /// appearance: its theme is an explicit Light / Dark choice, so the SDK is forced the same way.
    /// </summary>
    public static int SchemeTypeFor(OctopusSampleTheme theme)
    {
        return theme == OctopusSampleTheme.Dark ? DarkSchemeType : LightSchemeType;
    }

    /// <summary>
    /// Sends both schemes and the current scheme type to <paramref name="sdk"/>, then starts
    /// following the sample theme. Called right after `Initialize`; safe to call again.
    /// </summary>
    public static void Apply(IOctopusScenarioSdk sdk)
    {
        if (sdk == null) throw new ArgumentNullException("sdk");
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.SetLightColorScheme", "brand scheme");
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.SetDarkColorScheme", "brand scheme");
        sdk.ApplyTheme(LightScheme, DarkScheme);
        SendSchemeType(sdk, OctopusSampleBranding.Theme);
        if (_following) return;
        _following = true;
        OctopusSampleBranding.ThemeChanged += OnThemeChanged;
    }

    private static void OnThemeChanged()
    {
        // The SDK forgets its theme with the process, so an appearance change before (or without)
        // an initialisation has nothing to update; Apply sends the current one when it runs.
        if (!OctopusSampleState.IsInitialized) return;
        SendSchemeType(OctopusScenarioSdk.Current, OctopusSampleBranding.Theme);
    }

    private static void SendSchemeType(IOctopusScenarioSdk sdk, OctopusSampleTheme theme)
    {
        var type = SchemeTypeFor(theme);
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.SetColorSchemeType", theme.ToString());
        sdk.SetColorSchemeType(type);
    }
}
