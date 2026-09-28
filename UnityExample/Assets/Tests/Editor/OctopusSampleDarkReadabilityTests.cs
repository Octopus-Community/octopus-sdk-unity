using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Every text the sample draws in dark theme reads against what is actually behind it — no card
/// title, label or value left dark on navy — and no docked action sits on an opaque band that would
/// cut a hard edge across the page and its halo. Walks every sample-owned screen, as built.
/// </summary>
public class OctopusSampleDarkReadabilityTests
{
    private const float TextFloor = 4.5f;

    private readonly List<GameObject> _objects = new List<GameObject>();
    private OctopusSampleShell _shell;
    private Func<float> _textScale;

    [SetUp]
    public void SetUp()
    {
        _textScale = OctopusSampleTextScale.Provider;
        OctopusSampleTextScale.Provider = () => 1f;
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
    }

    [TearDown]
    public void TearDown()
    {
        SampleUiDebugEntryHost.ReleaseAll();
        if (_shell != null) _shell.Shutdown();
        foreach (var item in _objects) if (item != null) Object.DestroyImmediate(item);
        _objects.Clear();
        _shell = null;
        OctopusReefRunView.ResetRouting();
        OctopusSampleTextScale.Provider = _textScale;
        OctopusSampleBranding.Theme = OctopusSampleTheme.Dark;
    }

    [Test]
    public void EveryDarkThemeTextReadsOnItsGround()
    {
        var failures = new StringBuilder();

        _shell = OctopusSampleShell.Create();
        _objects.Add(_shell.gameObject);
        foreach (OctopusSampleTab tab in Enum.GetValues(typeof(OctopusSampleTab)))
        {
            _shell.Select(tab);
            Scan("tab " + tab, _shell.transform, failures);
        }

        foreach (var id in OctopusScenarioPilots.Ids)
        {
            var scenario = OctopusScenarioScreenView.Open(OctopusScenarioPilots.Create(id));
            _objects.Add(scenario.gameObject);
            Scan("scenario " + id, scenario.transform, failures);
            scenario.Dismiss();
        }

        var about = OctopusSampleAboutView.Open();
        _objects.Add(about.gameObject);
        Scan("About", about.transform, failures);
        about.Close();

        var appearance = OctopusSampleAppearanceView.Open();
        _objects.Add(appearance.gameObject);
        Scan("Appearance", appearance.transform, failures);
        appearance.Close();

        var config = OctopusSampleConfigView.Open();
        _objects.Add(config.gameObject);
        Scan("Config", config.transform, failures);
        Object.DestroyImmediate(config.gameObject);

        var account = OctopusSampleAccountView.Open();
        _objects.Add(account.gameObject);
        Scan("Account", account.transform, failures);
        Object.DestroyImmediate(account.gameObject);

        var developer = OctopusSampleDeveloperToolsView.Open(() => 0,
            () => new List<OctopusSampleDeveloperToolsView.LogLine>(),
            () => new List<OctopusSampleDeveloperToolsView.InfoFact>(), () => { });
        _objects.Add(developer.gameObject);
        Scan("Developer tools", developer.transform, failures);
        developer.ShowEvents();
        Scan("Developer tools / events", developer.transform, failures);
        developer.Back();
        developer.ShowInfo();
        Scan("Developer tools / info", developer.transform, failures);
        developer.Back();
        developer.Back();

        var profile = OctopusSampleClientProfileView.Open(new OctopusScenarioHostProfile("member-a"));
        _objects.Add(profile.gameObject);
        Scan("Client profile", profile.transform, failures);
        profile.Dismiss();

        var reef = OctopusReefRunView.Open();
        _objects.Add(reef.gameObject);
        Scan("Reef Run", reef.transform, failures);
        reef.Close();

        Assert.IsEmpty(failures.ToString(), "Dark-theme text under " + TextFloor + ":1:\n" + failures);
    }

    [Test]
    public void DockedActionsSitOnTheGroundInDarkAndOnTheSurfaceInLight()
    {
        _shell = OctopusSampleShell.Create();
        _objects.Add(_shell.gameObject);
        AssertDock(OctopusSampleTab.Home, "HomeDock");
        AssertDock(OctopusSampleTab.Community, "Dock");
    }

    [Test]
    public void TheListFadesIntoTheDarkDockAndNotIntoTheLightOne()
    {
        _shell = OctopusSampleShell.Create();
        _objects.Add(_shell.gameObject);
        foreach (var tab in new[] { OctopusSampleTab.Home, OctopusSampleTab.Community })
        {
            foreach (var theme in new[] { OctopusSampleTheme.Dark, OctopusSampleTheme.Light })
            {
                OctopusSampleBranding.Theme = theme;
                _shell.Select(tab);
                var fade = (RectTransform)Find(_shell.transform, SampleUiDockFade.ObjectName);
                var viewport = fade.parent;
                Assert.IsNotNull(viewport.GetComponent<RectMask2D>(), tab + ": the fade must sit in the masked viewport.");
                Assert.AreEqual(viewport.childCount - 1, fade.GetSiblingIndex(), tab + ": the fade must draw over the cards.");
                Assert.AreEqual(Vector2.zero, fade.anchorMin, tab + ": the fade must meet the viewport's bottom edge.");
                Assert.AreEqual(new Vector2(1f, 0f), fade.anchorMax);
                Assert.AreEqual(SampleUiDockFade.Height, fade.rect.height, 0.01f);
                var image = fade.GetComponent<RawImage>();
                Assert.IsFalse(image.raycastTarget, tab + ": the fade must never eat a tap or a drag.");
                if (theme == OctopusSampleTheme.Dark)
                {
                    Assert.IsTrue(image.enabled, tab + ": the last card is cut on a hard line above the dark dock.");
                    var texture = (Texture2D)image.texture;
                    var page = OctopusSampleBranding.Palette.Page;
                    var bottom = texture.GetPixel(0, 0);
                    var top = texture.GetPixel(0, texture.height - 1);
                    Assert.AreEqual(1f, bottom.a, 0.01f, "Opaque page at the dock's edge.");
                    Assert.AreEqual(0f, top.a, 0.01f, "Transparent at the top of the fade.");
                    Assert.AreEqual(page.r, bottom.r, 0.01f);
                    Assert.AreEqual(page.g, bottom.g, 0.01f);
                    Assert.AreEqual(page.b, bottom.b, 0.01f);
                }
                else
                {
                    Assert.IsFalse(image.enabled, tab + ": the light dock is an opaque band, no fade over it.");
                }
            }
        }
    }

    [Test]
    public void AFullyScrolledListStopsWithItsLastCardClearOfTheFade()
    {
        _shell = OctopusSampleShell.Create();
        _objects.Add(_shell.gameObject);
        foreach (var tab in new[] { OctopusSampleTab.Home, OctopusSampleTab.Community })
        {
            _shell.Select(tab);
            var fade = (RectTransform)Find(_shell.transform, SampleUiDockFade.ObjectName);
            var viewport = (RectTransform)fade.parent;
            var content = viewport.GetComponent<ScrollRect>().content;
            var padding = content.GetComponent<VerticalLayoutGroup>().padding;
            // At full scroll the content's bottom meets the viewport's, where the fade starts.
            Assert.GreaterOrEqual(padding.bottom, SampleUiDockFade.Height - 0.01f,
                tab + ": the last card ends under the fade.");
            // Viewport inset + list padding: the whole gap between the last card and the dock.
            Assert.GreaterOrEqual(viewport.offsetMin.y + padding.bottom,
                SampleUiDockFade.Height + OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceLg) - 0.5f,
                tab + ": less than the fade plus the usual spacing above the dock.");
        }
    }

    private void AssertDock(OctopusSampleTab tab, string name)
    {
        foreach (var theme in new[] { OctopusSampleTheme.Dark, OctopusSampleTheme.Light })
        {
            OctopusSampleBranding.Theme = theme;
            _shell.Select(tab);
            var dock = Find(_shell.transform, name).GetComponent<Image>();
            if (theme == OctopusSampleTheme.Dark)
                Assert.AreEqual(0f, dock.color.a, tab + ": an opaque dock cuts a band across the dark page.");
            else
                Assert.AreEqual(OctopusSampleBranding.Palette.Surface, dock.color, tab + ": light dock changed.");
        }
    }

    private static void Scan(string screen, Transform root, StringBuilder failures)
    {
        var palette = OctopusSampleBranding.Palette;
        var scanned = 0;
        var peak = SampleUiHalo.Composite(palette.Page, palette.Halo, 0f);
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(false))
        {
            if (!text.enabled || string.IsNullOrWhiteSpace(text.text) || text.color.a <= 0f) continue;
            scanned++;
            // Behind a clear chain the page shows, or the halo up to its peak: check both ends.
            var worst = float.PositiveInfinity;
            foreach (var floor in new[] { palette.Page, peak })
            {
                var ground = Ground(text.transform.parent, floor);
                worst = Mathf.Min(worst, Contrast(Over(text.color, ground), ground));
            }
            if (worst < TextFloor)
            {
                failures.AppendLine(screen + " — " + Path(text.transform, root) + " \"" + Clip(text.text) +
                                    "\" #" + ColorUtility.ToHtmlStringRGBA(text.color) + " measures " +
                                    worst.ToString("0.00") + ":1");
            }
        }
        // A screen that built no text would pass vacuously.
        if (scanned == 0) failures.AppendLine(screen + " — no text found to check");
    }

    /// <summary>
    /// What a text actually sits on: its ancestors' fills, innermost last, composited over the
    /// nearest opaque one (or <paramref name="floor"/> when none is opaque).
    /// </summary>
    private static Color Ground(Transform from, Color floor)
    {
        var fills = new List<Color>();
        for (var node = from; node != null; node = node.parent)
        {
            var graphic = node.GetComponent<Graphic>();
            if (graphic == null || !graphic.enabled || graphic is TMP_Text || graphic.color.a <= 0f) continue;
            var mask = node.GetComponent<Mask>();
            if (mask != null && mask.enabled && !mask.showMaskGraphic) continue;
            fills.Add(graphic.color);
            if (graphic.color.a >= 0.999f) break;
        }
        var ground = floor;
        for (var i = fills.Count - 1; i >= 0; i--) ground = Over(fills[i], ground);
        return ground;
    }

    private static Transform Find(Transform parent, string name)
    {
        foreach (var child in parent.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        Assert.Fail("No " + name + " under " + parent.name);
        return null;
    }

    private static string Path(Transform node, Transform root)
    {
        var path = node.name;
        for (var at = node.parent; at != null && at != root; at = at.parent) path = at.name + "/" + path;
        return path;
    }

    private static string Clip(string text)
    {
        text = text.Replace("\n", " ");
        return text.Length > 40 ? text.Substring(0, 40) + "…" : text;
    }

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
        return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
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
