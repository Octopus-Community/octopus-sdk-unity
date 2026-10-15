using UnityEngine;

public enum OctopusSampleTheme
{
    Light = 0,
    Dark = 1
}

/// <summary>
/// The sample token source, mapped from Android SampleTokens.kt (DS v15).
/// Colours are opaque sRGB values; uGUI's linear-colour conversion stays at the rendering boundary.
/// Unity keeps its own identity and existing OFL Inter/TMP font hook.
/// </summary>
public static class OctopusSampleBranding
{
    public const string AppName = "Octopus Sample for Unity";
    public const string PlatformLabel = "Unity";
    public const string PlatformAttribution =
        "This application is not sponsored by or affiliated with Unity Technologies or its affiliates.";

    public static readonly Color Navy = Hex(0x0F1B2D);
    public static readonly Color Accent = Hex(0x1D88FE);
    public static readonly Color LightAccentText = Hex(0x095FBF);
    public static readonly Color AccentDark = Hex(0x7FB8FF);
    public static readonly Color AccentInk = Hex(0x070D17);
    public static readonly Color LightBackground = Hex(0xF5F7FA);
    public static readonly Color TintedBackground = Hex(0xE4E8EE);
    public static readonly Color LightSurface3 = Hex(0xDAE0E8);
    public static readonly Color LightMuted = Hex(0x566170);
    public static readonly Color LightBorder = Hex(0xC2CAD5);
    public static readonly Color LightControlBorder = Hex(0x747E8C);
    public static readonly Color LightAccentTint = Hex(0xE8F2FF);
    public static readonly Color LightAccentContainer = Hex(0xD4E6FF);
    public static readonly Color LightOnAccentContainer = Hex(0x0A3D7A);
    public static readonly Color DarkBackground = Hex(0x070D17);
    public static readonly Color DarkSurfaceLow = Hex(0x18253B);
    public static readonly Color DarkSurface2 = Hex(0x22324C);
    public static readonly Color DarkElevated = Hex(0x2E405C);
    public static readonly Color DarkBorder = Hex(0x4A5F80);
    public static readonly Color DarkBorderStrong = Hex(0x8093AB);
    public static readonly Color DarkControlBorder = DarkBorderStrong;
    public static readonly Color DarkCardPressed = DarkSurface2;
    public static readonly Color DarkTitle = Hex(0xF2F5F9);
    public static readonly Color DarkBody = DarkTitle;
    public static readonly Color DarkMuted = Hex(0xA3B0C2);
    public static readonly Color DarkButton = Hex(0x1570D9);
    public static readonly Color DarkAccentTint = Hex(0x1B3556);
    public static readonly Color DarkAccentContainer = Hex(0x2A5EA0);
    public static readonly Color DarkOnAccentContainer = Hex(0xE3EFFF);
    public static readonly Color Amber = Hex(0x7A5716);
    public static readonly Color AmberDark = Hex(0xE2BD78);
    public static readonly Color Success = Hex(0x176830);
    public static readonly Color SuccessDark = Hex(0x8FE0A8);
    public static readonly Color Danger = Hex(0xB03A4E);
    public static readonly Color DangerDark = Hex(0xF0A0A8);
    public static readonly Color LightWarningSurface = Hex(0xF9F2E6);
    public static readonly Color DarkWarningSurface = Hex(0x3A3222);
    public static readonly Color LightDangerSurface = Hex(0xF8EDEF);
    public static readonly Color DarkDangerSurface = Hex(0x3F2E43);
    public static readonly Color PlatformSlotLight = Hex(0x414141);
    // Unity's achromatic marker stays distinct from semantic text/accent roles.
    public static readonly Color PlatformSlotDark = Hex(0xC2C2C2);
    public const float HaloAlpha = 0.12f;
    public static readonly Color DarkHalo = new Color(Accent.r, Accent.g, Accent.b, HaloAlpha);
    public static readonly Color Clear = Color.clear;
    public static readonly Color ShapeInk = Color.white;

    // Existing native-theme surface. Gray/hover additions belong to a separate package API phase.
    public static readonly Color SdkLightPrimaryMain = Navy;
    public static readonly Color SdkLightPrimaryLow = LightAccentContainer;
    public static readonly Color SdkLightPrimaryHigh = Accent;
    public static readonly Color SdkLightOnPrimary = Color.white;
    public static readonly Color SdkDarkPrimaryMain = AccentDark;
    public static readonly Color SdkDarkPrimaryLow = DarkAccentTint;
    public static readonly Color SdkDarkPrimaryHigh = Hex(0x8FC6FF);
    public static readonly Color SdkDarkOnPrimary = DarkBackground;

    // dp/sp measurements: convert once, at the Canvas boundary (3 units per dp).
    public const float UnitsPerDp = 3f;
    public const float SpaceXs = 4f;
    public const float SpaceSm = 8f;
    public const float SpaceMd = 12f;
    public const float SpaceLg = 16f;
    public const float SpaceXl = 20f;
    public const float Space2Xl = 24f;
    public const float Space3Xl = 32f;
    public const float GroupGap = Space2Xl;
    public const float RadiusSm = 6f;
    // One card radius for every surface that frames content (Material3's 12dp medium shape, the
    // Android sample's cards): the Scenarios list and the cards of every other screen used to
    // disagree, 12dp there and 8dp everywhere else.
    public const float CardRadius = 12f;
    public const float FieldRadius = 8f;
    public const float BannerRadius = 12f;
    public const float SheetRadius = 16f;
    public const float ButtonRadius = 999f;
    public const float Stroke = 1f;
    public const float FocusStroke = 2f;
    public const float PressedTint = 0.08f;
    public const float RowWithSubtitleHeight = 56f;
    public const float ScenarioRowHeight = 64f;
    public const float SwitchWidth = 52f;
    public const float SwitchHeight = 32f;
    public const float SwitchKnob = 22f;
    public const float AppBarUnits = 64f * UnitsPerDp;
    public const float MinTextSp = 12f;
    public const float MinTouchDp = 48f;
    public const float MinInteractiveIconDp = 24f;
    public const int MinTextUnits = 36;
    public const float MinTouchUnits = MinTouchDp * UnitsPerDp;
    public const float TabTextScaleCap = 1.3f;
    public const float ContentAlphaFloor = 0.74f;
    public const float ChromeControlTint = 0.36f;
    public const int BodyFontWeight = 400;
    public const int StrongFontWeight = 700;
    public const int TitleTextUnits = 22 * 3;
    public const int HeadlineTextUnits = 18 * 3;
    public const int BodyTextUnits = 16 * 3;
    public const int CaptionTextUnits = 12 * 3;
    public const int LabelTextUnits = 14 * 3;
    public const float TitleLineSp = 28f;
    public const float HeadlineLineSp = 24f;
    public const float BodyLineSp = 22f;
    public const float CaptionLineSp = 16f;
    // Android's bodyMedium as measured on its Settings rows (2-line subtitle = 36.6dp), not the
    // nominal 20sp: a 56dp row and the 73dp three-line Developer tools row both follow from it.
    public const float LabelLineSp = 18.3f;
    public const float SummaryDividerInsetDp = 52f;
    public static readonly Color LightElevationInk = Color.black;
    public static readonly Color DarkElevationInk = Color.white;
    public const float ElevationOpacity = 0.12f;
    public const float ElevationOffset = 1f;
    public const float ElevationBlur = 2f;
    public static int OverlayContentTopUnits { get { return Mathf.RoundToInt(AppBarUnits + Dp(SpaceLg)); } }
    public static float Dp(float dp) { return dp * UnitsPerDp; }
    public static int Sp(float sp) { return Mathf.RoundToInt(Mathf.Max(sp, MinTextSp) * UnitsPerDp); }

    // --- Theme ---------------------------------------------------------------------------------

    private static OctopusSampleTheme _theme = OctopusSampleTheme.Dark;
    private static OctopusSamplePalette _palette = OctopusSamplePalette.Dark();
    // Enabled at player startup, not by EditMode callers of the palette or Theme setter.
    private static string _themePreferenceKey;
    // A QA launch's `qaTheme`: wins over the saved choice for this process, and is never saved.
    private static OctopusSampleTheme? _launchTheme;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeTheme()
    {
        RestoreTheme("OctopusSample.Theme");
    }

    // A separate key lets tests exercise the real PlayerPrefs path without changing a user's choice.
    internal static void RestoreTheme(string preferenceKey)
    {
        _themePreferenceKey = preferenceKey;
        if (_launchTheme.HasValue)
        {
            SetTheme(_launchTheme.Value, false);
            return;
        }
        var saved = preferenceKey == null ? (int)OctopusSampleTheme.Dark
            : PlayerPrefs.GetInt(preferenceKey, (int)OctopusSampleTheme.Dark);
        SetTheme(saved == (int)OctopusSampleTheme.Light ? OctopusSampleTheme.Light
            : OctopusSampleTheme.Dark, false);
    }

    /// <summary>
    /// Applies a QA launch's theme for this process without saving it, or clears it with null.
    /// <c>adb install -r</c> keeps PlayerPrefs, so a run that switched to Light would otherwise start
    /// the next run in Light. Both this and <see cref="RestoreTheme"/> run before the first scene
    /// in an order Unity leaves undefined, so each honours the other: whichever runs last, the
    /// launch theme is the one in force.
    /// </summary>
    internal static void ApplyLaunchTheme(OctopusSampleTheme? theme)
    {
        _launchTheme = theme;
        if (theme.HasValue) SetTheme(theme.Value, false);
    }

    /// <summary>
    /// Raised after <see cref="Theme"/> changes, so an open screen can rebuild itself. Screens are
    /// built in code here, so "rebuild" is the whole mechanism — there is no style sheet to reload.
    /// </summary>
    public static event System.Action ThemeChanged;

    /// <summary>
    /// The theme in force, restored before the first scene and saved whenever it changes.
    /// Defaults to <see cref="OctopusSampleTheme.Dark"/> if no valid choice has been saved.
    /// </summary>
    public static OctopusSampleTheme Theme
    {
        get { return _theme; }
        set { SetTheme(value, true); }
    }

    private static void SetTheme(OctopusSampleTheme value, bool persist)
    {
        if (value != OctopusSampleTheme.Light && value != OctopusSampleTheme.Dark)
            throw new System.ArgumentOutOfRangeException("value");
        if (_theme == value) return;
        _theme = value;
        _palette = value == OctopusSampleTheme.Light
            ? OctopusSamplePalette.Light()
            : OctopusSamplePalette.Dark();
        if (persist && _themePreferenceKey != null)
        {
            try
            {
                PlayerPrefs.SetInt(_themePreferenceKey, (int)value);
                PlayerPrefs.Save();
            }
            catch (System.Exception)
            {
                // Storage failure must not prevent the open screens from adopting the palette.
                Debug.LogWarning("[Octopus SDK] Sample appearance could not be saved; " +
                    "the next launch may use the previous theme.");
            }
        }
        var handler = ThemeChanged;
        if (handler != null) handler();
    }

    internal static void RefreshViews()
    {
        var handler = ThemeChanged;
        if (handler != null) handler();
    }

    /// <summary>The palette for <see cref="Theme"/>. Every colour a screen draws comes from here.</summary>
    public static OctopusSamplePalette Palette
    {
        get { return _palette; }
    }

    /// <summary>The nav accent for the theme in force: navy in light, <see cref="AccentDark"/> in dark.</summary>
    public static Color NavAccent
    {
        get { return _palette.Accent; }
    }

    private static Color Hex(int rgb)
    {
        return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }

    /// <summary>Blends <paramref name="color"/> over <paramref name="over"/> at <paramref name="alpha"/>.</summary>
    /// <remarks>
    /// Composited to an opaque colour rather than drawn as a translucent fill: the navigation
    /// indicator sits on the bar's own container, and a real alpha would also let whatever the bar
    /// covers show through it.
    /// </remarks>
    public static Color Tint(Color color, Color over, float alpha)
    {
        return Color.Lerp(over, color, alpha);
    }
}

/// <summary>Resolved theme roles: screens and shared builders name a role, never a hue.</summary>
public sealed class OctopusSamplePalette
{
    public readonly Color Page, Surface, SurfaceHigh, Border, ControlBorder, Chrome, OnChrome, ChromeControl;
    public readonly Color Title, Muted, Accent, OnAccent, AccentIndicator, OnAccentIndicator, ButtonBackground;
    public readonly Color Attention, Positive, Negative, PlatformSlot, WarningSurface, DangerSurface;
    private readonly bool _dark;
    public Color ElevationInk { get { return _dark ? OctopusSampleBranding.DarkElevationInk : OctopusSampleBranding.LightElevationInk; } }
    /// <summary>Android's `NavigationBar` container, `surface-2`: #E4E8EE light, #22324C dark.</summary>
    public Color TabBar { get { return SurfaceHigh; } }
    public Color TabIndicator { get { return _dark ? OctopusSampleBranding.DarkAccentTint : OctopusSampleBranding.LightAccentTint; } }
    public Color DisabledSurface { get { return Surface; } }
    public Color DisabledInk { get { return Muted; } }
    public Color Body { get { return Title; } }
    public Color Elevated { get { return _dark ? OctopusSampleBranding.DarkElevated : OctopusSampleBranding.LightSurface3; } }
    // Accessible focus on the entire surface ladder, including light surface-2/3.
    public Color Focus { get { return Accent; } }
    public Color FilledControlFocus { get { return OnAccent; } }
    public Color ChipFill { get { return TabIndicator; } }
    public Color ChipBorder { get { return Border; } }
    public Color ChipInk { get { return Accent; } }
    public Color Placeholder { get { return Muted; } }
    public Color BorderStrong { get { return _dark ? OctopusSampleBranding.DarkBorderStrong : Border; } }
    public Color Halo { get { return _dark ? OctopusSampleBranding.DarkHalo : OctopusSampleBranding.Clear; } }
    public Color Header { get { return _dark ? OctopusSampleBranding.Clear : Chrome; } }
    /// <summary>
    /// A top-level bar that sits on the page, as Android's `SampleTopAppBar` does (container `bg`,
    /// <c>sampleTopBarColor</c>): the page colour in both themes. The role only: a uGUI tint of it
    /// does not render the page in dark (8-bit linear vertex colour, #070D17 comes out #0D0D16), so
    /// such a bar is painted with <see cref="SampleUiHalo.PaintPage"/>, which samples the page
    /// ground's own texels. Its content is drawn in <see cref="Title"/>.
    /// </summary>
    public Color PageHeader { get { return Page; } }
    public Color Dock { get { return _dark ? OctopusSampleBranding.Clear : Surface; } }

    private OctopusSamplePalette(bool dark)
    {
        _dark = dark;
        Page = dark ? OctopusSampleBranding.DarkBackground : OctopusSampleBranding.LightBackground;
        Surface = dark ? OctopusSampleBranding.DarkSurfaceLow : Color.white;
        SurfaceHigh = dark ? OctopusSampleBranding.DarkSurface2 : OctopusSampleBranding.TintedBackground;
        Border = dark ? OctopusSampleBranding.DarkBorder : OctopusSampleBranding.LightBorder;
        ControlBorder = dark ? OctopusSampleBranding.DarkControlBorder : OctopusSampleBranding.LightControlBorder;
        Chrome = dark ? Page : OctopusSampleBranding.Navy;
        OnChrome = dark ? OctopusSampleBranding.DarkTitle : Color.white;
        ChromeControl = OctopusSampleBranding.Tint(OnChrome, Chrome, dark ? 0.40f : OctopusSampleBranding.ChromeControlTint);
        Title = dark ? OctopusSampleBranding.DarkTitle : OctopusSampleBranding.Navy;
        Muted = dark ? OctopusSampleBranding.DarkMuted : OctopusSampleBranding.LightMuted;
        Accent = dark ? OctopusSampleBranding.AccentDark : OctopusSampleBranding.LightAccentText;
        OnAccent = Color.white;
        ButtonBackground = dark ? OctopusSampleBranding.DarkButton : OctopusSampleBranding.Navy;
        AccentIndicator = dark ? OctopusSampleBranding.DarkAccentContainer : OctopusSampleBranding.LightAccentContainer;
        OnAccentIndicator = dark ? OctopusSampleBranding.DarkOnAccentContainer : OctopusSampleBranding.LightOnAccentContainer;
        Attention = dark ? OctopusSampleBranding.AmberDark : OctopusSampleBranding.Amber;
        Positive = dark ? OctopusSampleBranding.SuccessDark : OctopusSampleBranding.Success;
        Negative = dark ? OctopusSampleBranding.DangerDark : OctopusSampleBranding.Danger;
        PlatformSlot = dark ? OctopusSampleBranding.PlatformSlotDark : OctopusSampleBranding.PlatformSlotLight;
        WarningSurface = dark ? OctopusSampleBranding.DarkWarningSurface : OctopusSampleBranding.LightWarningSurface;
        DangerSurface = dark ? OctopusSampleBranding.DarkDangerSurface : OctopusSampleBranding.LightDangerSurface;
    }

    public static OctopusSamplePalette Light() { return new OctopusSamplePalette(false); }
    public static OctopusSamplePalette Dark() { return new OctopusSamplePalette(true); }
}
