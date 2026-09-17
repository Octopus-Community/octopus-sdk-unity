using UnityEngine;

/// <summary>Which half of the token palette a screen draws with.</summary>
public enum OctopusSampleTheme
{
    Light,
    Dark
}

/// <summary>
/// The sample's own design tokens — the Unity half of the cross-platform sample contract
/// (`pm-tools/shared/design/samples/TOKENS.md`).
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
    /// The sample's chrome colour: app bar in both themes, primary button fill and the whole accent
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

    /// <summary>
    /// The accent role on dark surfaces — text, icons and fills alike. A lighter step of
    /// <see cref="Accent"/>, for a measured reason: the dark accent is almost never drawn on the
    /// bare page but on a raised container or its own tint, and <see cref="Accent"/> falls under the
    /// floor on all of those. This one holds 7.21:1 on the dark page, 6.35:1 on
    /// <see cref="DarkSurfaceLow"/>, and 4.74:1 over its own 15% indicator — the tightest path in
    /// the shell.
    /// </summary>
    public static readonly Color AccentDark = Hex(0x6FB2FF);

    /// <summary>
    /// The readable ink on an accent fill in both themes: 7.21:1 on <see cref="AccentDark"/>, where
    /// white would be 2.21:1. Same value as the dark page, so a filled control and the shell agree.
    /// </summary>
    public static readonly Color AccentInk = Hex(0x142238);

    /// <summary>Tinted ground for light-theme cards and framed blocks.</summary>
    public static readonly Color TintedBackground = Hex(0xEDF2FA);

    /// <summary>Border for framed blocks and secondary buttons, light theme.</summary>
    public static readonly Color LightBorder = Hex(0xB9C9E0);

    /// <summary>Dark page background and base surface.</summary>
    public static readonly Color DarkBackground = Hex(0x142238);

    /// <summary>Raised dark surfaces — one and two steps above <see cref="DarkBackground"/>.</summary>
    public static readonly Color DarkSurfaceLow = Hex(0x1B2C46);

    /// <summary>Two steps above the dark page: fields, and a card on a card.</summary>
    public static readonly Color DarkSurfaceHigh = Hex(0x223353);

    /// <summary>
    /// Border on the dark surface. A flat navy line rather than a tint of the accent: a border
    /// carries no meaning to read, and drawing it in the accent hue makes it compete with the
    /// controls that *are* the accent.
    /// </summary>
    public static readonly Color DarkBorder = Hex(0x253449);

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

    /// <summary>Dark-theme half of <see cref="Amber"/>: 9.67:1 on <see cref="DarkSurfaceLow"/>.</summary>
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
    /// Dark-theme half of <see cref="Success"/>: 7.23:1 on <see cref="DarkSurfaceLow"/>, 8.22:1 on
    /// <see cref="DarkBackground"/>. Split rather than forced through both themes, because
    /// <see cref="Success"/> measures 2.92:1 on the dark page.
    /// </summary>
    public static readonly Color SuccessDark = Hex(0x5FD07E);

    /// <summary>
    /// "DOWN" / "CALL FAILED" — a live state, or an operation, that did not work. Android's hue, unchanged: it measures
    /// 6.73:1 on the tinted card ground, so nothing forces a divergence here.
    /// </summary>
    public static readonly Color Danger = Hex(0x9E243F);

    /// <summary>Dark-theme half of <see cref="Danger"/>: 5.35:1 on <see cref="DarkSurfaceLow"/>.</summary>
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
    /// themes. 6.55:1 on <see cref="DarkSurfaceLow"/>, 4.89:1 on its own 15% tint.
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
    public static readonly Color SdkDarkPrimaryMain = Hex(0x6FB2FF);

    /// <summary>Dark `primaryLow` passed to the SDK.</summary>
    public static readonly Color SdkDarkPrimaryLow = Hex(0x142238);

    /// <summary>Dark `primaryHigh` passed to the SDK.</summary>
    public static readonly Color SdkDarkPrimaryHigh = Hex(0xDCE9FC);

    /// <summary>
    /// Dark `onPrimary`. The dark surface, not black, so SDK controls match the sample shell.
    /// </summary>
    public static readonly Color SdkDarkOnPrimary = Hex(0x142238);

    // Component roles and geometry: sample harmonisation reference, accepted 2026-09-11.
    public static readonly Color LightControlBorder = Hex(0x68768A);
    public static readonly Color DarkControlBorder = Hex(0x9AA7B8);
    public static readonly Color LightWarningSurface = Hex(0xFFF7E6);
    public static readonly Color DarkWarningSurface = Hex(0x382B18);
    public static readonly Color LightDangerSurface = Hex(0xFCEEF1);
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

    /// <summary>
    /// Raised after <see cref="Theme"/> changes, so an open screen can rebuild itself. Screens are
    /// built in code here, so "rebuild" is the whole mechanism — there is no style sheet to reload.
    /// </summary>
    public static event System.Action ThemeChanged;

    /// <summary>
    /// The theme in force. Defaults to <see cref="OctopusSampleTheme.Dark"/>: Unity exposes no
    /// runtime system-appearance API on the platforms this sample ships to, so a default is chosen
    /// rather than detected, and this is the one the sample already had.
    /// </summary>
    public static OctopusSampleTheme Theme
    {
        get { return _theme; }
        set
        {
            if (_theme == value) return;
            _theme = value;
            _palette = value == OctopusSampleTheme.Light
                ? OctopusSamplePalette.Light()
                : OctopusSamplePalette.Dark();
            var handler = ThemeChanged;
            if (handler != null) handler();
        }
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

    /// <summary>The app bar ground. Navy in both themes — the app bar is what says "Octopus sample".</summary>
    public readonly Color Chrome;

    /// <summary>Ink on <see cref="Chrome"/>: 17.28:1.</summary>
    public readonly Color OnChrome;

    /// <summary>
    /// The fill of a control drawn on <see cref="Chrome"/> — the app bar's own buttons.
    ///
    /// Deliberately not <see cref="Accent"/>. In light theme the accent role *is* the navy, and the
    /// app bar is navy in both themes, so an accent-filled pill on the bar is navy on navy: the
    /// control disappears and only its label survives. On-device QA of this shell caught exactly
    /// that — the light-theme theme toggle rendered as bare text with no pill at all, while dark
    /// theme showed a filled button. Android's sample avoids the collision the same way, by drawing
    /// its app-bar actions as ink-on-chrome icon buttons rather than as accent pills.
    ///
    /// A <see cref="OctopusSampleBranding.ChromeControlTint"/> wash of <see cref="OnChrome"/> over
    /// the bar: 3.31:1 against the bar, with <see cref="OnChrome"/> at 5.22:1 on it. Identical in
    /// both themes, because the bar is.
    /// </summary>
    public readonly Color ChromeControl;

    /// <summary>Primary text on <see cref="Page"/> and <see cref="Surface"/>.</summary>
    public readonly Color Title;

    /// <summary>Secondary text, at the content opacity floor and never below it.</summary>
    public readonly Color Muted;

    /// <summary>The accent role: selected nav item, primary button fill.</summary>
    public readonly Color Accent;

    /// <summary>Readable ink on an <see cref="Accent"/> fill.</summary>
    public readonly Color OnAccent;

    /// <summary>The selected nav item's indicator: the accent at 15% over the bar's own container.</summary>
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
    public Color DisabledSurface { get { return Surface; } }
    public Color DisabledInk { get { return Muted; } }
    public Color Focus { get { return Accent; } }
    public Color Placeholder { get { return Muted; } }

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
                                                  OctopusSampleBranding.ChromeControlTint);
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
    /// The dark palette. The indicator is composited over <see cref="Surface"/>, the bar's own
    /// container, rather than over the page — 4.74:1, the tightest contrast path in the shell.
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
            OctopusSampleBranding.DarkSurfaceHigh,
            OctopusSampleBranding.DarkBorder,
            OctopusSampleBranding.Navy,
            Color.white,
            Color.white,
            MutedInk(Color.white, OctopusSampleBranding.DarkBackground),
            OctopusSampleBranding.AccentDark,
            OctopusSampleBranding.AccentInk,
            OctopusSampleBranding.Tint(OctopusSampleBranding.AccentDark, surface, 0.15f),
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
