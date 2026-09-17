using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class OctopusSampleAppBarTests
{
    private readonly List<GameObject> _objects = new List<GameObject>();
    private OctopusSampleShell _shell;
    private Func<float> _textScale;

    [SetUp]
    public void SetUp()
    {
        _textScale = OctopusSampleTextScale.Provider;
        OctopusSampleTextScale.Provider = () => 1f;
    }

    [TearDown]
    public void TearDown()
    {
        SampleUiDebugEntryHost.ReleaseAll();
        if (_shell != null) _shell.Shutdown();
        foreach (var item in _objects) if (item != null) Object.DestroyImmediate(item);
        _objects.Clear();
        OctopusReefRunView.ResetRouting();
        OctopusSampleTextScale.Provider = _textScale;
    }

    [Test]
    public void AllShellAndPushedHeadersUseTheSharedGeometry()
    {
        _shell = OctopusSampleShell.Create();
        _objects.Add(_shell.gameObject);
        foreach (OctopusSampleTab tab in Enum.GetValues(typeof(OctopusSampleTab)))
        {
            _shell.Select(tab);
            AssertHeader(_shell.transform, "AppBar", "Title", null);
            var viewport = _shell.GetComponentsInChildren<ScrollRect>().First().viewport;
            Assert.AreEqual(OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceLg), viewport.offsetMin.x);
            Assert.AreEqual(-OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceLg), viewport.offsetMax.y);
        }
        var scenario = OctopusScenarioScreenView.Open(new ConnectionScenario());
        _objects.Add(scenario.gameObject);
        AssertHeader(scenario.transform, "Header", "Title", "Back");
        scenario.Dismiss();
        var about = OctopusSampleAboutView.Open();
        _objects.Add(about.gameObject);
        AssertHeader(about.transform, "Header", "Title", OctopusSampleAboutView.BackId);
        about.Close();
        var appearance = OctopusSampleAppearanceView.Open();
        _objects.Add(appearance.gameObject);
        AssertHeader(appearance.transform, "Header", "Line", OctopusSampleAppearanceView.BackId);
        appearance.Close();
        var developer = OctopusSampleDeveloperToolsView.Open(() => 0,
            () => new List<OctopusSampleDeveloperToolsView.LogLine>(),
            () => new List<OctopusSampleDeveloperToolsView.InfoFact>(), () => { });
        _objects.Add(developer.gameObject);
        AssertHeader(developer.transform, "Header", "Title", "devtools-back");
        developer.ShowEvents();
        AssertHeader(developer.transform, "Header", "Title", "devtools-back");
        developer.ShowInfo();
        AssertHeader(developer.transform, "Header", "Title", "devtools-back");
        developer.Back();
        developer.Back();
        var reef = OctopusReefRunView.Open();
        _objects.Add(reef.gameObject);
        AssertHeader(reef.transform, "Reef Run header", "Line", OctopusReefRunView.BackId);
        reef.Close();
    }

    [Test]
    public void IdentityAndInjectedDebugShareHeightAndCenterAndRestoreTheOwnerLayout()
    {
        var owner = Container();
        var entry = SampleUi.Button("debug-open-button", owner, "Debug", () => { });
        var original = entry.GetComponent<LayoutElement>();
        var height = original.preferredHeight;
        var priority = original.layoutPriority;
        var padding = entry.GetComponent<VerticalLayoutGroup>().padding;
        var paddingValues = new[] { padding.left, padding.right, padding.top, padding.bottom };
        SampleUi.RegisterDebugEntry(entry);
        try
        {
            _shell = OctopusSampleShell.Create();
            _objects.Add(_shell.gameObject);
            var bar = (RectTransform)entry.parent;
            LayoutRebuilder.ForceRebuildLayoutImmediate(bar);
            var chip = _shell.GetComponentsInChildren<RectTransform>().Single(
                item => item.name == OctopusSampleHomeView.PlatformChipId);
            var chipBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(bar, chip);
            var entryBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(bar, entry);
            Assert.AreEqual(OctopusSampleBranding.MinTouchUnits, chip.rect.height);
            Assert.AreEqual(chip.rect.height, entry.rect.height);
            Assert.AreEqual(chipBounds.center.y, entryBounds.center.y, 0.1f);
            var detail = SampleUi.AppBar("Header", owner, "Detail", () => { });
            SampleUiDebugEntryHost.Attach(detail);
            LayoutRebuilder.ForceRebuildLayoutImmediate(detail);
            Assert.AreSame(detail, entry.parent);
            Assert.AreEqual(OctopusSampleBranding.MinTouchUnits, entry.rect.height);
            SampleUiDebugEntryHost.ReleaseAll();
            _shell.Shutdown();
            Assert.AreSame(owner, entry.parent);
            Assert.AreEqual(height, original.preferredHeight);
            Assert.AreEqual(priority, original.layoutPriority);
            var restoredPadding = entry.GetComponent<VerticalLayoutGroup>().padding;
            CollectionAssert.AreEqual(paddingValues, new[]
                { restoredPadding.left, restoredPadding.right, restoredPadding.top, restoredPadding.bottom });
        }
        finally
        {
            SampleUiDebugEntryHost.ReleaseAll();
            if (_shell != null) _shell.Shutdown();
            SampleUi.RegisterDebugEntry(null);
        }
    }

    [TestCase(4, 66f, false)]
    [TestCase(100, 48f, true)]
    public void TitleLengthPinsRenderedSizeWithoutOverlappingControls(int length, float size, bool truncated)
    {
        var root = Container();
        var bar = SampleUi.AppBar("Header", root, new string('W', length), () => { });
        var action = SampleUi.ChromeButton("Action", bar, "Debug", () => { });
        SampleUi.AppBarItem(action);
        LayoutRebuilder.ForceRebuildLayoutImmediate(bar);
        var title = bar.Find("Title").GetComponent<TMP_Text>();
        title.ForceMeshUpdate();
        Assert.AreEqual(TextWrappingModes.NoWrap, title.textWrappingMode);
        Assert.AreEqual(TextOverflowModes.Ellipsis, title.overflowMode);
        Assert.AreEqual(truncated, title.isTextTruncated);
        Assert.AreEqual(size, title.fontSize, 0.1f);
        Assert.Greater(title.textInfo.characterCount, 0);
        Assert.AreEqual(size, title.textInfo.characterInfo[0].pointSize, 0.1f,
            "Pin the generated glyph size, not only the configured autosizing bounds.");
        if (truncated) Assert.Less(title.rectTransform.rect.width, title.preferredWidth);
        var titleBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(bar, title.transform);
        var actionBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(bar, action);
        var backBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(bar, bar.Find("Back"));
        Assert.GreaterOrEqual(titleBounds.min.x, backBounds.max.x);
        Assert.LessOrEqual(titleBounds.max.x, actionBounds.min.x);
    }

    [Test]
    public void AppBarItemOverridesAdaptiveHeightAtLargeTextScale()
    {
        OctopusSampleTextScale.Provider = () => 2f;
        var bar = SampleUi.AppBar("Header", Container(), "Title");
        var action = SampleUi.ChromeButton("Action", bar, "One\nTwo\nThree", () => { });
        var padding = action.GetComponent<VerticalLayoutGroup>().padding;
        int left = padding.left, right = padding.right;
        SampleUi.AppBarItem(action);
        LayoutRebuilder.ForceRebuildLayoutImmediate(bar);

        Assert.Greater(action.GetComponent<SampleUiAdaptivePanel>().preferredHeight, 144f,
            "The adaptive ILayoutElement competes with the fixed app-bar height at large text sizes.");
        Assert.AreEqual(144f, LayoutUtility.GetPreferredHeight(action));
        Assert.AreEqual(144f, action.rect.height);
        var actualPadding = action.GetComponent<VerticalLayoutGroup>().padding;
        CollectionAssert.AreEqual(new[] { left, right, 0, 0 }, new[]
            { actualPadding.left, actualPadding.right, actualPadding.top, actualPadding.bottom });
    }

    [TestCase(0f, 0f)] // Edge-to-edge emulator.
    [TestCase(120f, 90f)] // Notch and home indicator (physical pixels).
    public void HomeBarsAndScrolledContentClearTheRealDockInsideTheSafeArea(float top, float bottom)
    {
        _shell = OctopusSampleShell.Create();
        _objects.Add(_shell.gameObject);
        _shell.Select(OctopusSampleTab.Home);
        _shell.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var root = (RectTransform)_shell.transform;
        root.sizeDelta = new Vector2(1080f, 2400f);
        var safe = (RectTransform)_shell.GetComponentInChildren<SampleUiSafeArea>().transform;
        var header = (RectTransform)safe.Find("AppBar");
        var tabs = (RectTransform)safe.Find("TabBar");
        var home = _shell.GetComponentInChildren<OctopusSampleHomeView>();
        var dock = (RectTransform)home.transform.Find("HomeDock");
        safe.GetComponent<SampleUiSafeArea>().Apply(new Rect(0f, bottom, 1080f, 2400f - top - bottom),
            new Rect(), new Vector2(1080f, 2400f));
        LayoutRebuilder.ForceRebuildLayoutImmediate(safe);
        var scroll = home.GetComponentInChildren<ScrollRect>();
        scroll.verticalNormalizedPosition = 0f;
        var headerBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, header);
        var tabsBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, tabs);
        var dockBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, dock);
        // Recursive bounds include the scrolled content outside the mask. Measure only the
        // viewport's own rectangle, in the same root-local coordinates as the bars.
        var viewportCorners = new Vector3[4];
        scroll.viewport.GetWorldCorners(viewportCorners);
        float viewportMinY = float.PositiveInfinity;
        float viewportMaxY = float.NegativeInfinity;
        foreach (var corner in viewportCorners)
        {
            float y = root.InverseTransformPoint(corner).y;
            viewportMinY = Mathf.Min(viewportMinY, y);
            viewportMaxY = Mathf.Max(viewportMaxY, y);
        }
        Assert.LessOrEqual(headerBounds.max.y, root.rect.yMax - top + 0.1f);
        Assert.GreaterOrEqual(tabsBounds.min.y, root.rect.yMin + bottom - 0.1f);
        Assert.Less(viewportMaxY, headerBounds.min.y);
        Assert.Greater(viewportMinY, tabsBounds.max.y);
        Assert.Greater(viewportMinY, dockBounds.max.y,
            "The real Home scroll host must reserve the dock's height as well as the tab bar.");
        Assert.GreaterOrEqual(dockBounds.min.y, tabsBounds.max.y - 0.1f);
        Assert.IsNotNull(scroll.viewport.GetComponent<RectMask2D>());
    }

    private RectTransform Container()
    {
        var host = new GameObject("App bar test", typeof(RectTransform), typeof(Canvas));
        host.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        _objects.Add(host);
        var root = (RectTransform)host.transform;
        root.sizeDelta = new Vector2(1080f, 2400f);
        return root;
    }

    private static void AssertHeader(Transform root, string name, string titleName, string backName)
    {
        var bar = root.GetComponentsInChildren<RectTransform>().Single(item => item.name == name);
        Assert.IsFalse(bar.GetComponentsInChildren<Transform>(true).Any(item =>
            item.name == OctopusSampleAppearanceView.ShellThemeToggleId ||
            item.name == OctopusSampleAppearanceView.ThemeToggleId),
            "Theme controls belong in Settings > Appearance, never in an app bar.");
        var layout = bar.GetComponent<HorizontalLayoutGroup>();
        Assert.IsNotNull(layout, name);
        Assert.AreEqual(192f, bar.sizeDelta.y);
        Assert.AreEqual(48, layout.padding.left);
        Assert.AreEqual(OctopusSampleBranding.AppBarUnits, bar.sizeDelta.y);
        Assert.AreEqual(SampleUi.ContentPadding().left, layout.padding.left);
        Assert.AreEqual(SampleUi.ContentPadding().right, layout.padding.right);
        Assert.AreEqual(TextAnchor.MiddleLeft, layout.childAlignment);
        var title = bar.Find(titleName).GetComponent<TMP_Text>();
        Assert.AreEqual(TextOverflowModes.Ellipsis, title.overflowMode);
        Assert.AreEqual(TextWrappingModes.NoWrap, title.textWrappingMode);
        Assert.AreEqual(OctopusSampleBranding.MinTouchUnits, title.GetComponent<LayoutElement>().preferredHeight);
        if (backName != null)
        {
            Assert.IsNotNull(bar.Find(backName).Find("Icon"));
            var scroll = root.GetComponentInChildren<ScrollRect>();
            if (scroll != null)
            {
                Assert.AreEqual(SampleUi.ContentPadding().left, scroll.viewport.offsetMin.x);
                Assert.AreEqual(-OctopusSampleBranding.OverlayContentTopUnits, scroll.viewport.offsetMax.y);
                Assert.AreEqual(OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceLg),
                    scroll.content.GetComponent<VerticalLayoutGroup>().spacing);
            }
        }
    }
}
