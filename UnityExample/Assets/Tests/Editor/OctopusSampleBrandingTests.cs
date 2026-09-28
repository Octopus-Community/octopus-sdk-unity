using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Turns the readability floors of `shared/design/TOKENS.md` §2 into a gate.
///
/// The contrast figures written in <see cref="OctopusSampleBranding"/>'s comments are the kind of
/// claim that is true when written and quietly false three PRs later, and neither the compile gate
/// nor a screenshot catches a ratio that has drifted from 4.6 to 4.4. So every role is measured
/// here, against the surface it is *actually drawn on* — a label's contrast against the nominal page
/// is meaningless when it sits on a card.
///
/// Measured with the WCAG 2.x relative-luminance formula on sRGB values, which is what the token
/// file's own figures are. Unity's <see cref="Color"/> literals are gamma-space, so they feed it
/// directly.
/// </summary>
public class OctopusSampleBrandingTests
{
    /// <summary>WCAG AA for body text. TOKENS §2 takes it as the floor for every role here.</summary>
    private const float TextFloor = 4.5f;

    /// <summary>
    /// WCAG AA for a non-text boundary: what tells the eye a control is a control. TOKENS §2 asks it
    /// of a button's own fill against the surface behind it.
    /// </summary>
    private const float UiFloor = 3f;

    [TearDown]
    public void TearDown()
    {
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
    }

    [Test]
    public void TheLightPaletteClearsTheTextContrastFloor()
    {
        AssertPaletteIsReadable(OctopusSamplePalette.Light(), "light");
    }

    [Test]
    public void TheDarkPaletteClearsTheTextContrastFloor()
    {
        AssertPaletteIsReadable(OctopusSamplePalette.Dark(), "dark");
    }

    [TestCase("Copy log")]
    [TestCase("Copy")]
    public void DarkChromeButtonsStayReadableWhilePressed(string caption)
    {
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
        var owner = new GameObject("Copy button test", typeof(RectTransform));
        var events = new GameObject("Copy button events", typeof(EventSystem));
        try
        {
            var rect = SampleUi.ChromeButton("copy", owner.transform, caption, () => { });
            var button = rect.GetComponent<SampleUiButton>();
            var fill = rect.GetComponent<Image>();
            var label = rect.GetComponentInChildren<TMP_Text>();
            var normalFill = fill.color;
            AssertReadable("dark", caption + " at rest", label.color, fill.color);

            var pointer = new PointerEventData(events.GetComponent<EventSystem>());
            button.OnPointerEnter(pointer);
            button.OnPointerDown(pointer);
            Assert.AreEqual(OctopusSampleBranding.DarkCardPressed, fill.color);
            AssertReadable("dark", caption + " pressed", label.color, fill.color);

            button.OnPointerUp(pointer);
            Assert.AreEqual(normalFill, fill.color);
            AssertReadable("dark", caption + " released", label.color, fill.color);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(events);
        }
    }

    /// <summary>
    /// Every text role, on every ground it is drawn on. The pairs are the ones the shell and the
    /// two catalogue screens actually paint: `SampleUi.RowBackground` is the palette's Surface, so
    /// a label on a card is measured there and not on the page.
    /// </summary>
    private static void AssertPaletteIsReadable(OctopusSamplePalette palette, string theme)
    {
        AssertReadable(theme, "Title on the page", palette.Title, palette.Page);
        AssertReadable(theme, "Title on a card", palette.Title, palette.Surface);
        AssertReadable(theme, "Body on a card", palette.Body, palette.Surface);
        AssertReadable(theme, "Body on a sheet", palette.Body, palette.Elevated);
        AssertReadable(theme, "Caption on a sheet", palette.Muted, palette.Elevated);
        AssertReadable(theme, "Chip label", palette.ChipInk, palette.ChipFill);
        AssertReadable(theme, "Title on a field", palette.Title, palette.SurfaceHigh);
        AssertReadable(theme, "Muted text on the page", palette.Muted, palette.Page);
        AssertReadable(theme, "Muted text on a card", palette.Muted, palette.Surface);
        AssertReadable(theme, "App-bar title on the chrome", palette.OnChrome, palette.Chrome);
        AssertReadable(theme, "App-bar button label on its fill", palette.OnChrome,
                       palette.ChromeControl);
        AssertReadable(theme, "Button label on the accent", palette.OnAccent, palette.Accent);
        AssertReadable(theme, "Selected tab label on its indicator", palette.Accent,
                       palette.AccentIndicator);
        AssertReadable(theme, "Attention text on a card", palette.Attention, palette.Surface);
        AssertReadable(theme, "Attention text on the page", palette.Attention, palette.Page);
        AssertReadable(theme, "Disabled label", palette.DisabledInk, palette.DisabledSurface);
        AssertReadable(theme, "Placeholder", palette.Placeholder, palette.SurfaceHigh);
        AssertReadable(theme, "Warning text", palette.Attention, palette.WarningSurface);
        AssertReadable(theme, "Destructive label", palette.Negative, palette.DangerSurface);
        AssertReadable(theme, "Pressed primary label", palette.OnAccent,
            OctopusSampleBranding.Tint(palette.OnAccent, palette.Accent, OctopusSampleBranding.PressedTint));
        AssertReadable(theme, "Pressed secondary label", palette.Accent,
            OctopusSampleBranding.Tint(palette.Accent, palette.SurfaceHigh, OctopusSampleBranding.PressedTint));
        AssertReadable(theme, "Pressed destructive label", palette.Negative,
            OctopusSampleBranding.Tint(palette.Negative, palette.DangerSurface, OctopusSampleBranding.PressedTint));
        Assert.GreaterOrEqual(Contrast(palette.ControlBorder, palette.SurfaceHigh), UiFloor,
            "The field boundary must be identifiable on its actual fill.");
        AssertReadable(theme, "The platform slot on a card", palette.PlatformSlot, palette.Surface);
        AssertReadable(theme, "The platform slot on the page", palette.PlatformSlot, palette.Page);
    }

    [Test]
    public void TheDarkControlOutlineHoldsOnEveryStepOfTheLadder()
    {
        // The navy ladder has four grounds a control can sit on; the outline has to read on each,
        // not only on the field fill the palette check measures.
        var dark = OctopusSamplePalette.Dark();
        foreach (var ground in new[]
                 {
                     dark.Page, dark.Surface, dark.Elevated, OctopusSampleBranding.DarkCardPressed
                 })
        {
            Assert.GreaterOrEqual(Contrast(dark.ControlBorder, ground), UiFloor,
                "The dark control outline measures under 3:1 on one step of the ladder.");
        }
    }

    [Test]
    public void OnlyTheDarkThemeStrengthensTheFramedBlockHairline()
    {
        var light = OctopusSamplePalette.Light();
        var dark = OctopusSamplePalette.Dark();

        Assert.AreEqual(light.Border, light.BorderStrong, "The light theme must stay untouched.");
        Assert.AreEqual(OctopusSampleBranding.DarkBorderStrong, dark.BorderStrong);
        Assert.Greater(Contrast(dark.BorderStrong, dark.Surface), Contrast(dark.Border, dark.Surface),
            "The strong hairline must read above the default one on a card.");
    }

    [Test]
    public void TheAccentRoleIsNeverThePlatformSlotHue()
    {
        // TOKENS §7 separates the two layers: the slot hue identifies the platform (the Home chip,
        // the launcher badge) and says nothing about what is interactive. Painting a control with it
        // is how the identity layer and the semantic layer collapse into one.
        foreach (var palette in new[] { OctopusSamplePalette.Light(), OctopusSamplePalette.Dark() })
        {
            Assert.AreNotEqual(palette.PlatformSlot, palette.Accent,
                "The platform slot hue is being used as the accent.");
        }
    }

    [Test]
    public void AnAppBarControlStaysVisibleAgainstTheBar()
    {
        // The defect this pins down was found on device, not here: the theme toggle was filled with
        // the accent role, which in light theme IS the navy the bar is painted with, so the button
        // vanished and left its label floating. A pill that measures 1.00:1 against its own bar is
        // the kind of thing every contrast test in this file was green through.
        foreach (var palette in new[] { OctopusSamplePalette.Light(), OctopusSamplePalette.Dark() })
        {
            var ratio = Contrast(palette.ChromeControl, palette.Chrome);
            Assert.GreaterOrEqual(ratio, UiFloor,
                "An app-bar control measures " + ratio.ToString("0.00") + ":1 against the bar.");
            Assert.AreNotEqual(palette.Accent, palette.ChromeControl,
                "The app-bar control is back on the accent role, which is the bar's own hue in " +
                "light theme.");
        }
    }

    [Test]
    public void NoRoleIsDrawnTranslucent()
    {
        // Every ratio in this file — and in TOKENS §2 — is computed in sRGB, but the project renders
        // in linear colour space, where uGUI composites a translucent fill in linear space instead.
        // Muted text at 0.74 alpha measures 7.39:1 sRGB and about 3.30:1 as it is actually drawn, so
        // a translucent role is a role whose real contrast no test here can see. Roles resolve their
        // own composite to an opaque value; this is what keeps the next one from not bothering.
        var fields = typeof(OctopusSamplePalette).GetFields();
        foreach (var palette in new[] { OctopusSamplePalette.Light(), OctopusSamplePalette.Dark() })
        {
            foreach (var field in fields)
            {
                if (field.FieldType != typeof(Color)) continue;
                var color = (Color)field.GetValue(palette);
                Assert.AreEqual(1f, color.a, 0.0001f,
                    "Palette role " + field.Name + " is translucent: its rendered contrast is not " +
                    "the one measured here.");
            }
        }
    }

    [Test]
    public void TheTextFloorSurvivesAScreenNarrowerThanTheReference()
    {
        // A fixed 1080-unit reference is not a device-independent floor: width-driven scaling makes
        // a canvas unit worth dpWidth/reference dp, so on a 320dp phone the 36 units this sample
        // calls 12sp are drawn at 10.7dp while every clamp still reports green.
        foreach (var dpWidth in new[] { 320f, 360f, 390f, 411f, 600f })
        {
            var reference = SampleUi.ReferenceWidthFor(dpWidth);
            var textDp = SampleUi.TextCaption * dpWidth / reference;
            var touchDp = OctopusSampleBranding.MinTouchUnits * dpWidth / reference;

            Assert.GreaterOrEqual(textDp, OctopusSampleBranding.MinTextSp - 0.01f,
                "At " + dpWidth + "dp wide the smallest type renders at " + textDp.ToString("0.0") +
                "sp.");
            Assert.GreaterOrEqual(touchDp, OctopusSampleBranding.MinTouchDp - 0.01f,
                "At " + dpWidth + "dp wide a minimum target renders at " + touchDp.ToString("0.0") +
                "dp.");
        }
    }

    [Test]
    public void TheReferenceTracksDpWithoutAWidthCap()
    {
        foreach (float width in new[] { 320f, 360f, 390f, 600f, 3000f })
            Assert.AreEqual(width * 3f, SampleUi.ReferenceWidthFor(width));
        Assert.AreEqual(SampleUi.CanvasReference.x, SampleUi.ReferenceWidthFor(float.NaN));
        Assert.AreEqual(SampleUi.CanvasReference.x, SampleUi.ReferenceWidthFor(float.PositiveInfinity));
        Assert.AreEqual(SampleUi.CanvasReference.x, SampleUi.ReferenceWidthFor(0f),
            "An unknown density must fall back to the design reference, not to zero.");
    }

    [Test]
    public void TheTwoThemesAreActuallyDifferent()
    {
        var light = OctopusSamplePalette.Light();
        var dark = OctopusSamplePalette.Dark();

        Assert.AreNotEqual(light.Page, dark.Page);
        Assert.AreNotEqual(light.Accent, dark.Accent);
        Assert.AreNotEqual(light.Title, dark.Title);
    }

    [Test]
    public void TheDpScaleMatchesTheCanvasReference()
    {
        // 1080-wide canvas reference over a 360dp reference phone: one canvas unit is a third of a
        // dp. Every floor below is derived from that, so it is the figure to change if the reference
        // canvas ever does.
        Assert.AreEqual(3f, OctopusSampleBranding.UnitsPerDp);
        Assert.AreEqual(OctopusSampleBranding.Sp(OctopusSampleBranding.MinTextSp),
                        OctopusSampleBranding.MinTextUnits);
        Assert.AreEqual(OctopusSampleBranding.Dp(OctopusSampleBranding.MinTouchDp),
                        OctopusSampleBranding.MinTouchUnits);
    }

    [Test]
    public void NoTypeSizeSitsUnderTheTextFloor()
    {
        foreach (var size in new[]
                 {
                     SampleUi.TextTitleXl, SampleUi.TextTitle, SampleUi.TextBody, SampleUi.TextCaption
                 })
        {
            Assert.GreaterOrEqual(size, OctopusSampleBranding.MinTextUnits,
                "A type-scale step is under the 12sp floor (" +
                OctopusSampleBranding.MinTextUnits + " canvas units).");
        }
    }

    [Test]
    public void TheLauncherNameIsTheOneTheTokensFix()
    {
        // ProjectSettings.productName is what the launcher shows; the constant is what the About
        // screen and the Home header read. They are two files, so they drift unless something says
        // so out loud.
        Assert.AreEqual(OctopusSampleBranding.AppName, Application.productName,
            "ProjectSettings.productName and OctopusSampleBranding.AppName disagree.");
    }

    [Test]
    public void TheDarkHaloIsTheBrandBlueAtTwelvePercent()
    {
        var halo = OctopusSampleBranding.DarkHalo;
        Assert.AreEqual(0.12f, OctopusSampleBranding.HaloAlpha);
        Assert.AreEqual(OctopusSampleBranding.Accent.r, halo.r);
        Assert.AreEqual(OctopusSampleBranding.Accent.g, halo.g);
        Assert.AreEqual(OctopusSampleBranding.Accent.b, halo.b);
        Assert.AreEqual(OctopusSampleBranding.HaloAlpha, halo.a);
        Assert.AreEqual((Color32)new Color(0x1D / 255f, 0x88 / 255f, 0xFE / 255f), (Color32)new Color(halo.r, halo.g, halo.b));

        Assert.AreEqual(halo, OctopusSamplePalette.Dark().Halo);
        Assert.AreEqual(0f, OctopusSamplePalette.Light().Halo.a, "Light theme draws no halo.");
        Assert.AreEqual(0f, OctopusSamplePalette.Dark().Header.a, "The dark header must let the halo through.");
        Assert.AreEqual(OctopusSamplePalette.Light().Chrome, OctopusSamplePalette.Light().Header);
    }

    [Test]
    public void TextStaysReadableOnTheHaloPeak()
    {
        var palette = OctopusSamplePalette.Dark();
        var peak = SampleUiHalo.Composite(palette.Page, palette.Halo, 0f);
        Assert.AreEqual(new Color32(0x0A, 0x1C, 0x33, 0xFF), (Color32)peak);
        AssertReadable("dark", "muted text on the halo peak", palette.Muted, peak);
        AssertReadable("dark", "app-bar title on the halo peak", palette.OnChrome, peak);
    }

    [Test]
    public void SwitchingTheThemeSwapsThePaletteAndAnnouncesIt()
    {
        var announced = 0;
        System.Action listener = () => announced++;
        OctopusSampleBranding.ThemeChanged += listener;
        try
        {
            OctopusSampleBranding.Theme = OctopusSampleTheme.Light;

            Assert.AreEqual(1, announced, "The theme change was not announced exactly once.");
            Assert.AreEqual(OctopusSamplePalette.Light().Page, OctopusSampleBranding.Palette.Page);
            Assert.AreEqual(OctopusSampleBranding.Palette.Accent, OctopusSampleBranding.NavAccent);
        }
        finally
        {
            OctopusSampleBranding.ThemeChanged -= listener;
        }
    }

    private static void AssertReadable(string theme, string what, Color foreground, Color ground)
    {
        var ratio = Contrast(Over(foreground, ground), ground);
        Assert.GreaterOrEqual(ratio, TextFloor,
            theme + " theme — " + what + " measures " + ratio.ToString("0.00") +
            ":1, under the " + TextFloor + ":1 floor of TOKENS §2.");
    }

    /// <summary>Composites <paramref name="foreground"/> onto an opaque ground by its own alpha.</summary>
    private static Color Over(Color foreground, Color ground)
    {
        var a = foreground.a;
        return new Color(ground.r * (1f - a) + foreground.r * a,
                         ground.g * (1f - a) + foreground.g * a,
                         ground.b * (1f - a) + foreground.b * a, 1f);
    }

    private static float Contrast(Color a, Color b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        var high = Mathf.Max(la, lb);
        var low = Mathf.Min(la, lb);
        return (high + 0.05f) / (low + 0.05f);
    }

    private static float Luminance(Color c)
    {
        return 0.2126f * Channel(c.r) + 0.7152f * Channel(c.g) + 0.0722f * Channel(c.b);
    }

    private static float Channel(float c)
    {
        return c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
    }
}
