using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The dark-theme halo: its generated texture, its aim at the top-right screen corner, and its place
/// behind a transparent header on the shell.
/// </summary>
public class SampleUiHaloTests
{
    private OctopusSampleShell _shell;

    [TearDown]
    public void TearDown()
    {
        SampleUiDebugEntryHost.ReleaseAll();
        if (_shell != null)
        {
            _shell.Shutdown();
            Object.DestroyImmediate(_shell.gameObject);
        }
        _shell = null;
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
    }

    [Test]
    public void TheTexturePeaksAtTheTopRightCornerAndFadesIntoThePage()
    {
        var palette = OctopusSamplePalette.Dark();
        var texture = SampleUiHalo.TextureFor(palette.Page, palette.Halo);
        var last = SampleUiHalo.TextureSize - 1;
        Color32 page = palette.Page;

        Assert.AreEqual((Color32)SampleUiHalo.Composite(palette.Page, palette.Halo, 0f),
                        (Color32)texture.GetPixel(last, last), "Peak is not at the top-right corner.");
        // Left column and bottom row lie one radius away: page, so a clamped UV draws page too.
        Assert.AreEqual(page, (Color32)texture.GetPixel(0, last));
        Assert.AreEqual(page, (Color32)texture.GetPixel(last, 0));
        Assert.AreEqual(page, (Color32)texture.GetPixel(0, 0));

        // Monotonic along the diagonal towards the corner.
        var previous = 0f;
        for (var i = 0; i <= last; i += 16)
        {
            var blue = texture.GetPixel(i, i).b;
            Assert.GreaterOrEqual(blue, previous);
            previous = blue;
        }
        Assert.AreEqual(1f, texture.GetPixel(last, last).a, "Texels are stored opaque.");
    }

    [Test]
    public void ReleasingTheSharedTexturesDestroysThemAndTheNextUseRegenerates()
    {
        var palette = OctopusSamplePalette.Dark();
        var halo = SampleUiHalo.TextureFor(palette.Page, palette.Halo);
        var plain = SampleUiHalo.PlainFor(palette.Page);
        var fade = SampleUiDockFade.TextureFor(palette.Page);

        SampleUiHalo.ReleaseTextures();
        SampleUiDockFade.ReleaseTexture();

        // Unity's null: the native objects are gone, not merely dereferenced (the reload leak).
        Assert.IsTrue(halo == null, "The halo texture outlived its release.");
        Assert.IsTrue(plain == null, "The plain page texture outlived its release.");
        Assert.IsTrue(fade == null, "The dock fade texture outlived its release.");
        Assert.IsTrue(SampleUiHalo.TextureFor(palette.Page, palette.Halo) != null);
        Assert.IsTrue(SampleUiHalo.PlainFor(palette.Page) != null);
        Assert.IsTrue(SampleUiDockFade.TextureFor(palette.Page) != null);
    }

    [Test]
    public void TheFalloffIsLinearOverOneScreenWidth()
    {
        Assert.AreEqual(1f, SampleUiHalo.Falloff(0f));
        Assert.AreEqual(0.5f, SampleUiHalo.Falloff(0.5f), 1e-6f);
        Assert.AreEqual(0f, SampleUiHalo.Falloff(1f));
        Assert.AreEqual(0f, SampleUiHalo.Falloff(1.4f));
    }

    [Test]
    public void TheUvRectAnchorsTheQuadrantOnTheScreenCornerInBothOrientations()
    {
        // Portrait: the square of side W hangs from the top edge.
        var portrait = new Vector2(1080f, 2400f);
        var uv = SampleUiHalo.UvRect(new Rect(0f, 0f, 1080f, 2400f), portrait);
        Assert.AreEqual(1f, uv.xMax, 1e-5f);
        Assert.AreEqual(1f, uv.yMax, 1e-5f);
        Assert.AreEqual(0f, uv.xMin, 1e-5f);
        Assert.AreEqual(-(2400f - 1080f) / 1080f, uv.yMin, 1e-5f);

        // Landscape: the radius is still the width, so the square overhangs the bottom edge.
        var landscape = new Vector2(2400f, 1080f);
        uv = SampleUiHalo.UvRect(new Rect(0f, 0f, 2400f, 1080f), landscape);
        Assert.AreEqual(1f, uv.xMax, 1e-5f);
        Assert.AreEqual(1f, uv.yMax, 1e-5f);
        Assert.AreEqual(1f - 1080f / 2400f, uv.yMin, 1e-5f);

        // An inset ground keeps the halo on the screen corner, not on its own corner.
        uv = SampleUiHalo.UvRect(new Rect(0f, 200f, 1080f, 2000f), portrait);
        Assert.AreEqual(1f - 200f / 1080f, uv.yMax, 1e-5f);
    }

    [TestCase(OctopusSampleTheme.Dark)]
    [TestCase(OctopusSampleTheme.Light)]
    public void TheShellDrawsTheHaloBehindAClearHeaderInDarkOnly(OctopusSampleTheme theme)
    {
        OctopusSampleBranding.Theme = theme;
        _shell = OctopusSampleShell.Create();
        var root = _shell.transform.Find("Root");
        Assert.IsNotNull(root);
        var halo = root.GetChild(0);
        Assert.AreEqual(SampleUiHalo.ObjectName, halo.name, "The halo must be the ground's first child.");
        var image = halo.GetComponent<RawImage>();
        Assert.IsFalse(image.raycastTarget);

        var appBar = FindDeep(_shell.transform, "AppBar").GetComponent<Image>();
        var topBleed = FindDeep(_shell.transform, "TopBleed").GetComponent<Image>();
        if (theme == OctopusSampleTheme.Dark)
        {
            Assert.IsTrue(image.enabled);
            Assert.IsNotNull(image.texture);
            Assert.AreEqual(0f, appBar.color.a, "A painted header draws a hard edge across the halo.");
            Assert.AreEqual(0f, topBleed.color.a);
        }
        else
        {
            Assert.IsFalse(image.enabled, "Light theme draws no halo.");
            Assert.AreEqual(OctopusSampleBranding.Palette.Chrome, appBar.color);
            Assert.AreEqual(OctopusSampleBranding.Palette.Chrome, topBleed.color);
        }
    }

    [Test]
    public void SwitchingTheThemeTogglesTheHalo()
    {
        _shell = OctopusSampleShell.Create();
        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        var halo = _shell.transform.Find("Root").GetChild(0).GetComponent<RawImage>();
        Assert.IsFalse(halo.enabled);
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
        halo = _shell.transform.Find("Root").GetChild(0).GetComponent<RawImage>();
        Assert.IsTrue(halo.enabled);
    }

    [Test]
    public void TheCommunityTabDrawsNoHaloAndTheOtherTabsGetItBack()
    {
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
        _shell = OctopusSampleShell.Create();
        _shell.Select(OctopusSampleTab.Community);
        var halo = _shell.transform.Find("Root").GetChild(0);
        Assert.AreEqual(SampleUiHalo.ObjectName, halo.name);
        Assert.IsTrue(halo.GetComponent<SampleUiHalo>().Hidden);
        AssertPlainPage(halo.GetComponent<RawImage>());

        // A theme switch rebuilds the tab: still no glow there.
        OctopusSampleBranding.Theme = OctopusSampleTheme.Light;
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
        AssertPlainPage(_shell.transform.Find("Root").GetChild(0).GetComponent<RawImage>());

        foreach (var tab in new[] { OctopusSampleTab.Home, OctopusSampleTab.Scenarios, OctopusSampleTab.Settings })
        {
            _shell.Select(tab);
            halo = _shell.transform.Find("Root").GetChild(0);
            Assert.IsFalse(halo.GetComponent<SampleUiHalo>().Hidden, tab.ToString());
            var image = halo.GetComponent<RawImage>();
            Assert.IsTrue(image.enabled, tab + " keeps its halo.");
            Assert.AreEqual(SampleUiHalo.TextureFor(OctopusSampleBranding.Palette.Page, OctopusSampleBranding.Palette.Halo),
                            image.texture, tab + " draws the glow.");
        }
    }

    // No glow, but the ground still painted from an sRGB texture: a vertex colour would draw the
    // ink page #0D0D16 after 8-bit linear, off the docks' fade and the other tabs.
    private static void AssertPlainPage(RawImage image)
    {
        Assert.IsTrue(image.enabled, "The plain page still paints the ground in dark.");
        var page = OctopusSampleBranding.Palette.Page;
        Assert.AreEqual(SampleUiHalo.PlainFor(page), image.texture, "The Community tab draws no glow.");
        var corner = ((Texture2D)image.texture).GetPixel(3, 3);
        Assert.AreEqual(page.r, corner.r, 0.01f);
        Assert.AreEqual(page.g, corner.g, 0.01f);
        Assert.AreEqual(page.b, corner.b, 0.01f);
        Assert.AreEqual(1f, corner.a, 0.01f);
    }

    [Test]
    public void ADetailPageGlowsUnderItsHeaderAndUnderTheStatusBar()
    {
        var about = OctopusSampleAboutView.Open();
        try
        {
            var page = FindDeep(about.transform, OctopusSampleAboutView.ScreenId);
            Assert.AreEqual(SampleUiHalo.ObjectName, page.GetChild(0).name);
            Assert.IsTrue(page.GetChild(0).GetComponent<RawImage>().enabled);
            // The status-bar bleed is a sibling of the page and clear in dark: it carries a halo of
            // its own, or the strip would show whatever lies under the detail.
            var bleed = FindDeep(about.transform, "TopBleed");
            Assert.AreEqual(0f, bleed.GetComponent<Image>().color.a);
            Assert.IsTrue(bleed.Find(SampleUiHalo.ObjectName).GetComponent<RawImage>().enabled);
        }
        finally
        {
            Object.DestroyImmediate(about.gameObject);
        }
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (var child in parent.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        Assert.Fail("No " + name + " under " + parent.name);
        return null;
    }
}
