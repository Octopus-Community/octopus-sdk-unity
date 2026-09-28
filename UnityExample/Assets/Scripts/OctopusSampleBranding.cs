using UnityEngine;

/// <summary>
/// Which half of the token palette a screen draws with. The value is persisted as its number under the
/// <c>OctopusSample.Theme</c> PlayerPrefs key, so the numbers are a stored contract: never renumber
/// or reorder the members, and give a new one the next free value.
/// </summary>
public enum OctopusSampleTheme
{
    /// <summary>Light appearance. Persisted as 0 — never renumber.</summary>
    Light = 0,
    /// <summary>Dark appearance, the default. Persisted as 1 — never renumber.</summary>
    Dark = 1
}

/// <summary>
/// The sample's own design tokens — the Unity half of the cross-platform sample contract
/// (shared design token catalog, internal).
///
/// Android's `SampleBranding.kt`, Flutter's `branding.dart` and React Native's `branding.ts` carry
/// the same values under the same names; TOKENS.md asks each platform for exactly this file
/// ("Unity has none yet, create a static `branding` class"). It exists so a hue is declared once
/// rather than typed as a literal into a screen, which is what let four samples drift apart before.
///
/// Nothing here belongs to the SDK. <see cref="SdkLightPrimaryMain"/> and its siblings are the
/// quadruple the sample *hands to* the SDK (TOKENS §4); they are declared here with the rest so the
/// shell and the SDK's own controls cannot be themed from two different tables.
///
/// Every contrast figure in the comments below is measured against the surface the colour is
/// actually drawn on, not against the nominal page — the distinction TOKENS §3 records as the one
/// that killed two earlier drafts of this palette.
/// </summary>
public static class OctopusSampleBranding
{
    // --- Identity ------------------------------------------------------------------------------

    /// <summary>
    /// The app name, attributive form (TOKENS §5, 24 characters). Every platform brand guideline
    /// forbids a "&lt;Brand&gt; &lt;Product&gt;" juxtaposition for a third-party app, so this form
    /// is not a stylistic choice. Mirrored in `ProjectSettings.productName`.
    /// </summary>
    public const string AppName = "Octopus Sample for Unity";

    /// <summary>The platform name the Home chip shows, as plain text — no logo is permitted there.</summary>
    public const string PlatformLabel = "Unity";

    /// <summary>
    /// The verbatim non-affiliation notice Unity's brand guidelines require of a third-party app
    /// that names the engine (TOKENS §6). Shown in About, next to the official "Made with Unity"
    /// badge, and never paraphrased.
    /// </summary>
    public const string PlatformAttribution =
        "This application is not sponsored by or affiliated with Unity Technologies or its affiliates.";

    // --- Brand hues ----------------------------------------------------------------------------

    /// <summary>
    /// The sample's light chrome colour: app bar, primary button fill and the whole accent
    /// role in light theme. 17.28:1 on white, 15.38:1 on <see cref="TintedBackground"/>, 12.70:1 as
    /// a label over its own 15% tint (the navigation indicator).
    /// </summary>
    public static readonly Color Navy = Hex(0x0F1B2D);

    /// <summary>
    /// The brand blue. A fill, never a label: it reads 3.50:1 on white, under the text floor, which
    /// is why the light-theme accent *role* is <see cref="Navy"/> and the dark one
    /// <see cref="AccentDark"/>. Its own readable ink is <see cref="AccentInk"/>.
    /// </summary>
    public static readonly Color Accent = Hex(0x1D88FE);

    /// <summary>Signal blue-soft: dark links, icons and button fills.</summary>
    public static readonly Color AccentDark = Hex(0x66B0FF);

    /// <summary>Signal ink on dark accent fills.</summary>
    public static readonly Color AccentInk = Hex(0x070D17);

    /// <summary>Tinted ground for light-theme cards and framed blocks.</summary>
    public static readonly Color TintedBackground = Hex(0xEDF2FA);

    /// <summary>Border for framed blocks and secondary buttons, light theme.</summary>
    public static readonly Color LightBorder = Hex(0xB9C9E0);

    // Dark ladder, aligned on the octopuscommunity.com site: ink → navy → navy-2, each step a
    // bluish navy rather than a neutral grey, so depth reads as depth instead of flat grey slabs.
    // Flat sRGB; Unity exposes no SDK gray/surface/hover tokens, so only Background, the primary
    // quadruple and Link reach the native SDK — the rest is the shell's alone.
    // Page → #070D17 (site --ink) → SDK Background; shell Page / Chrome.
    // Card / cell → #0F1B2D (brand navy) → shell Surface / SurfaceHigh only.
    // Hairline → #1E2A3D (site --line) → shell Border only.
    // Strong hairline → #243349 → shell BorderStrong only (the current-configuration frame).
    // Elevated → #16243A (site --navy-2) → shell Elevated only (dialogs / sheets / menus).
    // Card hover / pressed → #1D2E48 → shell CardPressed only.
    // Headings → #F2F6FC → shell Title / OnChrome only.
    // Body → #E9F0FA → shell Body only.
    // Captions / meta → #8C9AB0 → shell Muted only.
    // Accent → #66B0FF → SDK Primary / Link; shell Accent.
    // On accent → #070D17 → SDK OnPrimary; shell OnAccent.
    // Tinted container → #16273C → SDK PrimaryLow; shell AccentIndicator.
    // Progress / focus → #8FC6FF → SDK PrimaryHigh; shell Focus.
    // Text ladder measured on page / card / elevated / hover: title 17.95 / 15.94 / 14.37 /
    // 12.60:1, body 16.97 / 15.07 / 13.59 / 11.92:1, muted 6.83 / 6.06 / 5.46 / 4.79:1.
    public static readonly Color DarkBackground = Hex(0x070D17);
    public static readonly Color DarkSurfaceLow = Navy;
    public static readonly Color DarkElevated = Hex(0x16243A);
    // Decorative only (1.20:1 on the card) — never the boundary of a control.
    public static readonly Color DarkBorder = Hex(0x1E2A3D);
    // One step above DarkBorder for a block that has to stand out; still decorative (1.35:1).
    public static readonly Color DarkBorderStrong = Hex(0x243349);
    public static readonly Color DarkCardPressed = Hex(0x1D2E48);
    public static readonly Color DarkTitle = Hex(0xF2F6FC);
    public static readonly Color DarkBody = Hex(0xE9F0FA);
    public static readonly Color DarkMuted = Hex(0x8C9AB0);

    /// <summary>
    /// How strongly the dark-theme halo tints the page at its centre: the peak of
    /// <see cref="DarkHalo"/>, identical on the four samples.
    /// </summary>
    public const float HaloAlpha = 0.12f;

    /// <summary>
    /// The dark-theme halo: the brand blue at <see cref="HaloAlpha"/>, fading to fully transparent
    /// over a radius of one screen width from the top-right corner (<see cref="SampleUiHalo"/>).
    /// At its peak it flattens to #0A1C33 over the page, where <see cref="DarkMuted"/> still
    /// reads 6.00:1. Decorative — every text block sits on an opaque card above it.
    /// </summary>
    public static readonly Color DarkHalo = new Color(Accent.r, Accent.g, Accent.b, HaloAlpha);

    /// <summary>
    /// Degraded/warning text — a gap to close. A permanent absence uses muted text instead.
    ///
    /// One step darker than the amber the other samples carry (`#B45309`), and the divergence is
    /// forced rather than stylistic: this text is drawn on the card ground, which on Unity is the
    /// tinted background rather than white. The lighter amber measures 4.47:1 there — under the
    /// TOKENS §2 floor by a hair, and only *on* white does it clear it (5.02:1). This one measures
    /// 6.31:1 on the tinted ground and 7.09:1 on white, so it holds on both.
    /// </summary>
    public static readonly Color Amber = Hex(0x92400E);

    /// <summary>Dark-theme warning text.</summary>
    public static readonly Color AmberDark = Hex(0xF1D390);

    /// <summary>
    /// "READY" / "CONNECT OK" — a live state, or an operation, that worked. The status dot draws its label in this
    /// colour too, so it is text and the 4.5:1 floor applies to it, not the 3:1 graphical one.
    ///
    /// One step darker than the green the other samples carry (`#189437`), and forced by the same
    /// thing the amber divergence is: this label sits on the card ground, which on Unity is the
    /// tinted background rather than white. `#189437` measures 3.51:1 there and 3.94:1 on white —
    /// under the floor on both. This one measures 4.86:1 on the card and 5.47:1 on white.
    /// </summary>
    public static readonly Color Success = Hex(0x0F7A2C);

    /// <summary>
    /// Dark-theme success text, distinct from the darker light-theme status color.
    /// </summary>
    public static readonly Color SuccessDark = Hex(0x5FD07E);

    /// <summary>
    /// "DOWN" / "CALL FAILED" — a live state, or an operation, that did not work. Android's hue, unchanged: it measures
    /// 6.73:1 on the tinted card ground, so nothing forces a divergence here.
    /// </summary>
    public static readonly Color Danger = Hex(0x9E243F);

    /// <summary>Dark-theme error text.</summary>
    public static readonly Color DangerDark = Hex(0xE98098);

    // --- Platform slot hue ---------------------------------------------------------------------

    /// <summary>
    /// This platform's slot hue: OKLCh(0.375, 0.0, —), achromatic — hue is meaningless at zero
    /// chroma, and that is Unity's slot in the shared generator, not an approximation of it.
    /// 10.21:1 on white, 7.88:1 as a label on its own 15% tint.
    ///
    /// Reserved for identity — the launcher icon badge and the Home platform chip — and never
    /// applied to a semantic element (a status, an accent, an error). Those are identical on every
    /// platform by contract, and tinting one with the slot hue is exactly what makes two samples
    /// look like two different products.
    /// </summary>
    public static readonly Color PlatformSlotLight = Hex(0x414141);

    /// <summary>
    /// Dark-theme half of the slot hue: the same achromatic recipe with lightness raised to 0.76,
    /// the way Android splits its own pair. <see cref="PlatformSlotLight"/> measures 1.38:1 on the
    /// dark surface — far under the floor — so the pair is split rather than forced through both
    /// themes. 8.06:1 on <see cref="DarkSurfaceLow"/>, 4.89:1 on its own 15% tint.
    /// </summary>
    public static readonly Color PlatformSlotDark = Hex(0xB1B1B1);

    // --- The theme handed to the SDK -----------------------------------------------------------

    /// <summary>Light `primaryMain` passed to the SDK (TOKENS §4).</summary>
    public static readonly Color SdkLightPrimaryMain = Hex(0x0F1B2D);

    /// <summary>Light `primaryLow` passed to the SDK.</summary>
    public static readonly Color SdkLightPrimaryLow = Hex(0xDCE9FC);

    /// <summary>Light `primaryHigh` passed to the SDK.</summary>
    public static readonly Color SdkLightPrimaryHigh = Hex(0x1D88FE);

    /// <summary>Light `onPrimary` passed to the SDK.</summary>
    public static readonly Color SdkLightOnPrimary = Hex(0xFFFFFF);

    /// <summary>Dark `primaryMain` passed to the SDK.</summary>
    public static readonly Color SdkDarkPrimaryMain = AccentDark;

    /// <summary>Dark `primaryLow` passed to the SDK.</summary>
    public static readonly Color SdkDarkPrimaryLow = Hex(0x16273C);

    /// <summary>Dark `primaryHigh` passed to the SDK.</summary>
    public static readonly Color SdkDarkPrimaryHigh = Hex(0x8FC6FF);

    /// <summary>
    /// Dark `onPrimary`. The dark surface, not black, so SDK controls match the sample shell.
    /// </summary>
    public static readonly Color SdkDarkOnPrimary = DarkBackground;

    // Component roles and geometry: sample harmonisation reference, accepted 2026-09-11.
    public static readonly Color LightControlBorder = Hex(0x68768A);
    // off-table: identifiable field/control boundaries need 3:1 (WCAG 1.4.11) on every step of the
    // ladder: #6A7D9B is 4.65:1 on the page, 4.13:1 on the card, 3.72:1 elevated, 3.27:1 on hover.
    public static readonly Color DarkControlBorder = Hex(0x6A7D9B);
    public static readonly Color LightWarningSurface = Hex(0xFFF7E6);
    // off-table: warning-tinted surface; #F1D390 warning text measures 9.47:1 on #382B18.
    public static readonly Color DarkWarningSurface = Hex(0x382B18);
    public static readonly Color LightDangerSurface = Hex(0xFCEEF1);
    // off-table: error-tinted surface; #E98098 error text measures 5.26:1 on #3B2639.
    public static readonly Color DarkDangerSurface = Hex(0x3B2639);
    public static readonly Color Clear = Color.clear;
    public static readonly Color ShapeInk = Color.white;

    // Measurements are dp; convert only at the uGUI boundary.
    // Inter Regular for body/caption; real Bold for titles, actions and selected tab labels.
    public const int BodyFontWeight = 400;
    public const int StrongFontWeight = 700;
    public static readonly Color LightElevationInk = Hex(0x000000);
    public static readonly Color DarkElevationInk = Hex(0xFFFFFF);
    public const float ElevationOpacity = 0.12f;
    public const float ElevationOffset = 1f;
    public const float ElevationBlur = 2f;
    public const float AppBarUnits = 64f * UnitsPerDp;
    public static int OverlayContentTopUnits
    {
        get { return Mathf.RoundToInt(AppBarUnits + Dp(SpaceLg)); }
    }
    public const float SpaceXs = 4f;
    public const float SpaceSm = 8f;
    public const float SpaceMd = 12f;
    public const float SpaceLg = 16f;
    public const float SpaceXl = 20f;
    public const float Space2Xl = 24f;
    public const float Space3Xl = 32f;
    public const float CardRadius = 16f;
    public const float FieldRadius = 12f;
    public const float ButtonRadius = 24f;
    public const float Stroke = 1f;
    public const float FocusStroke = 2f;
    public const float PressedTint = 0.08f;
    public const float RowWithSubtitleHeight = 64f;
    public const float SwitchWidth = 52f;
    public const float SwitchHeight = 32f;
    public const float SwitchKnob = 22f;

    // --- Readability floor (TOKENS §2) ---------------------------------------------------------

    /// <summary>
    /// The 1080x1920 canvas reference maps to a 360dp-wide reference phone, so **one canvas unit is
    /// a third of a dp**. Every floor below is expressed through this rather than as a bare number,
    /// because a size in canvas units means nothing without the reference it was measured in.
    /// </summary>
    public const float UnitsPerDp = 3f;

    /// <summary>The lowest text size TOKENS §2 allows, in dp/sp.</summary>
    public const float MinTextSp = 12f;

    /// <summary>The lowest touch target TOKENS §2 allows, in dp.</summary>
    public const float MinTouchDp = 48f;

    /// <summary>The lowest interactive icon size TOKENS §2 allows, in dp.</summary>
    public const float MinInteractiveIconDp = 24f;

    /// <summary>
    /// The lowest alpha any meaning-carrying content may be drawn at (≈ 4.5:1 on its own ground).
    /// Separators and backgrounds are exempt — they carry nothing to read.
    ///
    /// Applied by resolving the composite into an opaque role, never by handing uGUI a translucent
    /// colour: see <see cref="OctopusSamplePalette"/>'s muted roles for why the difference is a
    /// contrast bug rather than a matter of taste.
    /// </summary>
    public const float ContentAlphaFloor = 0.74f;

    /// <summary>
    /// How much <see cref="OctopusSamplePalette.OnChrome"/> is washed over the app bar to fill a
    /// control that sits on it. 0.36 is the setting where both floors clear at once: the chip reads
    /// 3.31:1 against the bar (TOKENS §2 asks 3:1 of a control boundary) and its own label reads
    /// 5.22:1 on the chip. Below 0.34 the chip vanishes into the bar; above 0.40 the label falls
    /// under 4.5:1.
    /// </summary>
    public const float ChromeControlTint = 0.36f;

    /// <summary><see cref="MinTextSp"/> in canvas units: the clamp <c>SampleUi.Label</c> applies.</summary>
    public const int MinTextUnits = 36;

    /// <summary>Only tab captions cap the system text scale, to preserve four reachable tabs.</summary>
    public const float TabTextScaleCap = 1.3f;

    /// <summary><see cref="MinTouchDp"/> in canvas units: the height every tappable row gets.</summary>
    public const float MinTouchUnits = 144f;

    /// <summary>Converts a dp measurement to the canvas units the code-built screens are laid out in.</summary>
    public static float Dp(float dp)
    {
        return dp * UnitsPerDp;
    }

    /// <summary>
    /// Converts a text size in sp to canvas units, floored at <see cref="MinTextSp"/>. A caller that
    /// asks for less does not get it — the floor is not advisory.
    /// </summary>
    public static int Sp(float sp)
    {
        return Mathf.RoundToInt(Mathf.Max(sp, MinTextSp) * UnitsPerDp);
    }

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

/// <summary>
/// One theme's worth of resolved roles. A screen names a role — the page, a surface, the accent —
/// and never a hue, so the two themes cannot drift apart field by field.
/// </summary>
public class OctopusSamplePalette
{
    /// <summary>The screen ground.</summary>
    public readonly Color Page;

    /// <summary>A card / section ground, one step above <see cref="Page"/>.</summary>
    public readonly Color Surface;

    /// <summary>An input's ground — a further step up, so a field reads as a field.</summary>
    public readonly Color SurfaceHigh;

    /// <summary>Frame around a block or a secondary button. Carries no meaning: exempt from the floor.</summary>
    public readonly Color Border;

    /// <summary>The app bar ground: navy in light, Signal ink in dark.</summary>
    public readonly Color Chrome;

    /// <summary>Heading ink on the app bar.</summary>
    public readonly Color OnChrome;

    /// <summary>An opaque text wash over the app bar, distinct from its background.</summary>
    public readonly Color ChromeControl;

    /// <summary>Primary text on <see cref="Page"/> and <see cref="Surface"/>.</summary>
    public readonly Color Title;

    /// <summary>Secondary text and captions.</summary>
    public readonly Color Muted;

    /// <summary>The accent role: selected nav item, primary button fill.</summary>
    public readonly Color Accent;

    /// <summary>Readable ink on an <see cref="Accent"/> fill.</summary>
    public readonly Color OnAccent;

    /// <summary>The selected nav item container, tinted from the accent.</summary>
    public readonly Color AccentIndicator;

    /// <summary>A gap to close. A permanent absence uses <see cref="Muted"/> instead.</summary>
    public readonly Color Attention;

    /// <summary>
    /// A live state that is working — the "READY" and "CONNECT OK" dots. A *state*, never a
    /// porting status: TOKENS §1 removed the Built/Partial/Pending badge from every sample, and
    /// nothing here may bring a tri-state badge back onto the Scenarios list.
    /// </summary>
    public readonly Color Positive;

    /// <summary>A live state that is not working — the "DOWN" and "CALL FAILED" dots. Same rule as
    /// <see cref="Positive"/>.</summary>
    public readonly Color Negative;

    /// <summary>This platform's identity hue for this theme — the Home chip, and nothing else.</summary>
    public readonly Color PlatformSlot;

    public readonly Color ControlBorder;
    public readonly Color WarningSurface;
    public readonly Color DangerSurface;
    public Color ElevationInk
    {
        get { return Page == OctopusSampleBranding.DarkBackground
            ? OctopusSampleBranding.DarkElevationInk : OctopusSampleBranding.LightElevationInk; }
    }
    public Color TabBar { get { return IsDark ? OctopusSampleBranding.DarkSurfaceLow : Color.white; } }
    public Color TabIndicator { get { return OctopusSampleBranding.Tint(Accent, TabBar, 0.15f); } }
    public Color DisabledSurface { get { return Surface; } }
    public Color DisabledInk { get { return Muted; } }
    private bool IsDark { get { return Page == OctopusSampleBranding.DarkBackground; } }
    public Color Body { get { return IsDark ? OctopusSampleBranding.DarkBody : Title; } }
    public Color Elevated { get { return IsDark ? OctopusSampleBranding.DarkElevated : Surface; } }
    public Color Focus { get { return IsDark ? OctopusSampleBranding.SdkDarkPrimaryHigh : Accent; } }
    // Resolve alpha in sRGB before uGUI's linear-space rendering.
    public Color ChipFill { get { return IsDark ? OctopusSampleBranding.Tint(Accent, Surface, 0.10f) : SurfaceHigh; } }
    public Color ChipBorder { get { return IsDark ? OctopusSampleBranding.Tint(Accent, Surface, 0.28f) : Border; } }
    public Color ChipInk { get { return IsDark ? Accent : Muted; } }
    public Color Placeholder { get { return Muted; } }
    /// <summary>Frame of a block that has to stand out from its neighbours; <see cref="Border"/> in light.</summary>
    public Color BorderStrong { get { return IsDark ? OctopusSampleBranding.DarkBorderStrong : Border; } }
    /// <summary>
    /// The halo drawn behind the header: <see cref="OctopusSampleBranding.DarkHalo"/> in dark,
    /// fully clear in light, where no halo is drawn.
    /// </summary>
    public Color Halo { get { return IsDark ? OctopusSampleBranding.DarkHalo : OctopusSampleBranding.Clear; } }
    /// <summary>
    /// The app bar ground and its status-bar bleed: <see cref="Chrome"/> in light, clear in dark so
    /// the halo runs under the header without a hard edge (the dark chrome is the page anyway).
    /// </summary>
    public Color Header { get { return IsDark ? OctopusSampleBranding.Clear : Chrome; } }
    /// <summary>
    /// The band behind a docked bottom action (Home, Community): <see cref="Surface"/> in light,
    /// clear in dark, where an opaque navy band would cut a hard edge across the page and the halo.
    /// Nothing scrolls under a dock (its scroll host stops above it), so clear leaves no overlap.
    /// </summary>
    public Color Dock { get { return IsDark ? OctopusSampleBranding.Clear : Surface; } }

    private OctopusSamplePalette(Color controlBorder, Color warningSurface, Color dangerSurface,
                                 Color page, Color surface, Color surfaceHigh, Color border,
                                 Color chrome, Color onChrome, Color title, Color muted,
                                 Color accent, Color onAccent, Color accentIndicator,
                                 Color attention, Color positive, Color negative,
                                 Color platformSlot)
    {
        ControlBorder = controlBorder;
        WarningSurface = warningSurface;
        DangerSurface = dangerSurface;
        Page = page;
        Surface = surface;
        SurfaceHigh = surfaceHigh;
        Border = border;
        Chrome = chrome;
        OnChrome = onChrome;
        // Derived here rather than passed in by each factory: it has to track the bar it is drawn
        // on, and a factory free to pass its own value is a factory free to pass the accent again.
        ChromeControl = OctopusSampleBranding.Tint(onChrome, chrome,
            IsDark ? 0.40f : OctopusSampleBranding.ChromeControlTint);
        Title = title;
        Muted = muted;
        Accent = accent;
        OnAccent = onAccent;
        AccentIndicator = accentIndicator;
        Attention = attention;
        Positive = positive;
        Negative = negative;
        PlatformSlot = platformSlot;
    }

    /// <summary>
    /// The light palette. The accent role is the navy (17.28:1 on the bar, 12.70:1 over its own
    /// indicator) — not <see cref="OctopusSampleBranding.Accent"/>, which is a fill-only hue.
    /// </summary>
    public static OctopusSamplePalette Light()
    {
        var page = Color.white;
        var surface = OctopusSampleBranding.TintedBackground;
        var title = OctopusSampleBranding.Navy;
        return new OctopusSamplePalette(
            OctopusSampleBranding.LightControlBorder,
            OctopusSampleBranding.LightWarningSurface,
            OctopusSampleBranding.LightDangerSurface,
            page,
            surface,
            page,
            OctopusSampleBranding.LightBorder,
            OctopusSampleBranding.Navy,
            Color.white,
            title,
            MutedInk(title, page),
            OctopusSampleBranding.Navy,
            Color.white,
            // Over the tab bar's own container, not over the page: the bar is painted with Surface,
            // and TOKENS §3 composites an indicator over the container it actually sits on. 11.34:1
            // for the navy label on it, against the 12.68:1 the page-composited value claimed.
            OctopusSampleBranding.Tint(OctopusSampleBranding.Navy, surface, 0.15f),
            OctopusSampleBranding.Amber,
            OctopusSampleBranding.Success,
            OctopusSampleBranding.Danger,
            OctopusSampleBranding.PlatformSlotLight);
    }

    /// <summary>
    /// The dark palette on the site's navy ladder: ink page, navy cards and a separate blue accent
    /// container.
    /// </summary>
    public static OctopusSamplePalette Dark()
    {
        var surface = OctopusSampleBranding.DarkSurfaceLow;
        return new OctopusSamplePalette(
            OctopusSampleBranding.DarkControlBorder,
            OctopusSampleBranding.DarkWarningSurface,
            OctopusSampleBranding.DarkDangerSurface,
            OctopusSampleBranding.DarkBackground,
            surface,
            surface,
            OctopusSampleBranding.DarkBorder,
            OctopusSampleBranding.DarkBackground,
            OctopusSampleBranding.DarkTitle,
            OctopusSampleBranding.DarkTitle,
            OctopusSampleBranding.DarkMuted,
            OctopusSampleBranding.AccentDark,
            OctopusSampleBranding.AccentInk,
            OctopusSampleBranding.SdkDarkPrimaryLow,
            OctopusSampleBranding.AmberDark,
            OctopusSampleBranding.SuccessDark,
            OctopusSampleBranding.DangerDark,
            OctopusSampleBranding.PlatformSlotDark);
    }

    /// <summary>
    /// The muted role: <paramref name="ink"/> at <see cref="OctopusSampleBranding.ContentAlphaFloor"/>
    /// over <paramref name="ground"/>, resolved to an opaque colour.
    ///
    /// Opaque, and that is a correctness fix rather than a matter of taste. The project renders in
    /// linear colour space (`ProjectSettings.asset` → `m_ActiveColorSpace: 1`), so uGUI composites a
    /// translucent label in *linear* space — not in the sRGB space every contrast figure in this
    /// file, and in TOKENS §2, is written in. Light-theme muted at 0.74 alpha measures 7.39:1 under
    /// the sRGB model and about 3.30:1 as linear blending actually draws it: under the floor, and
    /// invisible to a test that models the blend the way the token file writes it. Resolving the
    /// composite here makes the value the tests measure the value the GPU draws — 7.39:1 light
    /// (6.57:1 on a card), 9.27:1 dark (8.15:1 on a card).
    ///
    /// Composited over the page rather than over a card, so the role is one colour and not two; the
    /// card is the darker ground in light theme and the lighter one in dark theme, and the tests
    /// measure both.
    /// </summary>
    private static Color MutedInk(Color ink, Color ground)
    {
        return OctopusSampleBranding.Tint(ink, ground, OctopusSampleBranding.ContentAlphaFloor);
    }
}
