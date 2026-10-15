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
    private static bool _navy = true;
    private static int? _appearance;
    private static bool _configuring;

    public static bool NavyTheme { get { return _navy; } }

    public static int CurrentAppearance { get { return _appearance ?? SchemeTypeFor(OctopusSampleBranding.Theme); } }

    internal static void ResetConfiguration() { _navy = true; _appearance = null; }

    internal static void Configure(bool navy, int? appearance)
    {
        _navy = navy;
        _appearance = appearance;
        _configuring = true;
        try
        {
            var choice = appearance ?? SchemeTypeFor(OctopusSampleBranding.Theme);
            OctopusSampleBranding.Theme = choice == 0 ? OctopusSampleSystemAppearance.Read()
                : choice == LightSchemeType ? OctopusSampleTheme.Light : OctopusSampleTheme.Dark;
            OctopusSampleBranding.RefreshViews();
        }
        finally { _configuring = false; }
    }


    public static void RefreshSystemAppearance()
    {
        if (_appearance != 0) return;
        var theme = OctopusSampleSystemAppearance.Read();
        if (theme == OctopusSampleBranding.Theme) return;
        _configuring = true;
        try { OctopusSampleBranding.Theme = theme; }
        finally { _configuring = false; }
    }

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
                OctopusSampleBranding.SdkDarkOnPrimary,
                link: OctopusSampleBranding.AccentDark,
                background: OctopusSampleBranding.DarkBackground);
        }
    }

    /// <summary>
    /// The native scheme type matching an explicit Light / Dark choice. System is tracked
    /// separately as 0, while the shell palette resolves to the current platform appearance.
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
        // A nonnull scheme with all slots unset resets both native palettes to SDK defaults.
        var defaults = new OctopusColorScheme(OctopusSampleBranding.Clear, OctopusSampleBranding.Clear,
            OctopusSampleBranding.Clear, OctopusSampleBranding.Clear);
        sdk.ApplyTheme(_navy ? LightScheme : defaults, _navy ? DarkScheme : defaults);
        SendSchemeType(sdk, OctopusSampleBranding.Theme);
        if (_following) return;
        _following = true;
        OctopusSampleBranding.ThemeChanged += OnThemeChanged;
    }

    private static void OnThemeChanged()
    {
        // The SDK forgets its theme with the process, so an appearance change before (or without)
        // an initialisation has nothing to update; Apply sends the current one when it runs.
        if (!OctopusSampleState.IsInitialized || _configuring) return;
        _appearance = SchemeTypeFor(OctopusSampleBranding.Theme);
        SendSchemeType(OctopusScenarioSdk.Current, OctopusSampleBranding.Theme);
    }

    private static void SendSchemeType(IOctopusScenarioSdk sdk, OctopusSampleTheme theme)
    {
        var type = _appearance ?? SchemeTypeFor(theme);
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.SetColorSchemeType", theme.ToString());
        sdk.SetColorSchemeType(type);
    }
}
