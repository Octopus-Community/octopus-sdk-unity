using TMPro;
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class OctopusSampleTextScaleTests
{
    private Func<float> _provider;
    private GameObject _host;
    private GameObject _events;
    private OctopusSampleShell _shell;

    [SetUp]
    public void SetUp()
    {
        _provider = OctopusSampleTextScale.Provider;
        OctopusSampleTextScale.Provider = () => 1f;
        _host = new GameObject("Text scale test", typeof(RectTransform), typeof(Canvas));
        _host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        if (EventSystem.current == null) _events = new GameObject("Events", typeof(EventSystem));
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (_shell != null) { _shell.Shutdown(); Object.DestroyImmediate(_shell.gameObject); }
            Object.DestroyImmediate(_host);
            if (_events != null) Object.DestroyImmediate(_events);
        }
        finally
        {
            OctopusSampleTextScale.Provider = _provider;
            Assert.AreSame(_provider, OctopusSampleTextScale.Provider, "Each test restores the caller's seam.");
        }
    }

    [Test]
    public void SafeAreaRefreshNoticesLayoutChangesWithoutAScreenChange()
    {
        var root = Container(1080f, 1920f);
        var safe = SampleUi.SafeArea("existing-safe-area", root);
        var adapter = safe.GetComponent<SampleUiSafeArea>();
        adapter.Refresh();
        var header = SampleUi.Panel("Header", safe, SampleUi.Background);
        SampleUi.Stretch(header, new Vector2(0f, 1f), Vector2.one);
        header.pivot = new Vector2(0.5f, 1f);
        header.sizeDelta = new Vector2(0f, 4000f);
        adapter.Refresh();
        Assert.AreEqual("SafeAreaViewport", safe.parent.name);
        Object.DestroyImmediate(header.gameObject);
        adapter.Refresh();
        Assert.AreSame(root, safe.parent);
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(0.8f)]
    public void InvalidOrSmallerPreferencesKeepTheReadabilityFloor(float value)
    {
        OctopusSampleTextScale.Provider = () => value;
        Assert.AreEqual(1f, OctopusSampleTextScale.Current);
    }

    [TestCase(360f, 1f)]
    [TestCase(360f, 1.3f)]
    [TestCase(360f, 2f)]
    [TestCase(390f, 1.3f)]
    [TestCase(390f, 2f)]
    public void TextCardsRowsAndButtonsGrowWithoutClipping(float widthDp, float scale)
    {
        var column = Column(widthDp);
        var baseline = SampleUi.FlexibleLabel(column, "First line\nSecond line", SampleUi.TextBody, SampleUi.TitleColor);
        // Measure the unscaled label only once it has its laid-out width: a label that has never
        // been reflowed is still zero-wide, wraps on every word, and reports a height that no
        // correctly laid-out scaled label has to beat.
        Reflow(column);
        float normalHeight = baseline.preferredHeight;
        OctopusSampleTextScale.Provider = () => scale;
        var card = SampleUi.Card("existing-card", column);
        var copy = SampleUi.FlexibleLabel(card, "First line\nSecond line", SampleUi.TextBody, SampleUi.TitleColor);
        const string detail = "A long description or action that must remain readable when text is enlarged. ";
        SampleUi.ListRow("existing-row", card, detail, detail + detail, () => { });
        SampleUi.Button("existing-button", card, detail + detail, () => { });
        Reflow(column);
        Assert.AreEqual(Mathf.RoundToInt(SampleUi.TextBody * scale), copy.fontSize);
        if (scale > 1f)
        {
            Assert.Greater(copy.fontSize, SampleUi.TextBody);
            baseline.GetComponent<SampleUiScaledText>().RefreshScale();
            Assert.Greater(baseline.preferredHeight, normalHeight);
        }
        foreach (var text in card.GetComponentsInChildren<TMP_Text>()) AssertFits(text);
    }

    [TestCase(1.3f)]
    [TestCase(2f)]
    public void FieldsMeasureTheBufferAndKeepPaddingAtLargerSizes(float scale)
    {
        OctopusSampleTextScale.Provider = () => scale;
        var column = Column(360f);
        var input = SampleUi.LabeledField("existing-field", column, "Persistent label", "Search scenarios", false, "Search scenarios");
        var multiline = SampleUi.Field("existing-multiline", column,
            "First line\nSecond line\nThird line\nFourth line\nFifth line\nSixth line", true);
        Reflow(column);
        Assert.AreEqual(Mathf.RoundToInt(SampleUi.TextBody * scale), input.textComponent.fontSize);
        AssertFits(input.textComponent);
        Assert.GreaterOrEqual(((RectTransform)input.transform).rect.height,
            OctopusSampleBranding.Dp(20f) * scale + OctopusSampleBranding.Dp(24f));
        Assert.GreaterOrEqual(((RectTransform)multiline.transform).rect.height, multiline.preferredHeight - 1f);
        AssertFits(multiline.textComponent);
    }

    [Test]
    public void ExistingLabelsRefreshWithoutReplacingObjectsAndCanReturnToOne()
    {
        var column = Column(360f);
        var label = SampleUi.FlexibleLabel(column, "Live text", SampleUi.TextBody, SampleUi.TitleColor);
        OctopusSampleTextScale.Provider = () => 2f;
        label.GetComponent<SampleUiScaledText>().RefreshScale();
        Reflow(column);
        Assert.AreEqual(84, label.fontSize);
        AssertFits(label);
        OctopusSampleTextScale.Provider = () => 1f;
        label.GetComponent<SampleUiScaledText>().RefreshScale();
        Reflow(column);
        Assert.AreEqual(42, label.fontSize);
    }

    [TestCase(1f)]
    [TestCase(1.3f)]
    [TestCase(2f)]
    public void OnlyTheFourShellTabLabelsAreCapped(float scale)
    {
        OctopusSampleTextScale.Provider = () => scale;
        _shell = OctopusSampleShell.Create();
        Reflow((RectTransform)_shell.transform.Find("Root"));
        float tabWidth = ((RectTransform)Find(_shell.transform, "home-tab")).rect.width;
        foreach (OctopusSampleTab tab in Enum.GetValues(typeof(OctopusSampleTab)))
        {
            var item = Find(_shell.transform, OctopusSampleShell.TestId(tab));
            Assert.AreEqual(tabWidth, ((RectTransform)item).rect.width, 1f, "All four tabs keep equal widths.");
            var label = item.Find("Label").GetComponent<TMP_Text>();
            Assert.AreEqual(Mathf.RoundToInt(SampleUi.TextCaption * Mathf.Min(scale, 1.3f)), label.fontSize);
            AssertFits(label);
            var icon = (RectTransform)item.Find("Indicator");
            var iconBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(item, icon);
            var labelBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(item, label.transform);
            Assert.LessOrEqual(labelBounds.max.y, iconBounds.min.y + 1f, "The wrapped label must clear the icon.");
        }
        var title = Find(_shell.transform, "AppBar").Find("Title").GetComponent<TMP_Text>();
        Assert.AreEqual(Mathf.RoundToInt(SampleUi.TextTitleXl * scale), title.fontSizeMax);
        Assert.IsTrue(title.enableAutoSizing);
        Assert.AreEqual(TextWrappingModes.NoWrap, title.textWrappingMode);
        Assert.AreEqual(TextOverflowModes.Ellipsis, title.overflowMode);
        title.ForceMeshUpdate();
        Assert.LessOrEqual(title.textBounds.size.y, title.rectTransform.rect.height + 1f);
        Assert.LessOrEqual(title.renderedWidth, title.rectTransform.rect.width + 1f);
    }

    [Test]
    public void AnchoredHeadersAndDocksReserveTheirGrownHeightInTheViewport()
    {
        var root = Container(1080f, 2200f);
        var header = SampleUi.Panel("Header", root, SampleUi.Background);
        SampleUi.Stretch(header, new Vector2(0f, 1f), Vector2.one);
        header.pivot = new Vector2(0.5f, 1f);
        header.sizeDelta = new Vector2(0f, 192f);
        SampleUi.VerticalStack(header, 0f, new RectOffset(48, 48, 24, 24), false);
        var title = SampleUi.FlexibleLabel(header,
            "A longer title that wraps across several lines when enlarged", SampleUi.TextTitleXl, SampleUi.TitleColor);
        var dock = SampleUi.Button("existing-open", root, "Open community", () => { });
        dock.anchorMin = Vector2.zero;
        dock.anchorMax = new Vector2(1f, 0f);
        dock.pivot = new Vector2(0.5f, 0f);
        dock.sizeDelta = new Vector2(0f, 144f);
        dock.anchoredPosition = Vector2.zero;
        var content = SampleUi.VerticalScroll(root, new RectOffset(0, 0, 240, 168));
        var viewport = (RectTransform)content.parent;
        OctopusSampleTextScale.Provider = () => 2f;
        foreach (var scaled in root.GetComponentsInChildren<SampleUiScaledText>()) scaled.RefreshScale();
        Reflow(root);
        AssertFits(title);
        Assert.Greater(header.rect.height, 192f);
        Assert.LessOrEqual(viewport.offsetMax.y, -header.rect.height - 48f + 1f);
        Assert.GreaterOrEqual(viewport.offsetMin.y, dock.rect.height + 24f - 1f);
        OctopusSampleTextScale.Provider = () => 1f;
        foreach (var scaled in root.GetComponentsInChildren<SampleUiScaledText>()) scaled.RefreshScale();
        Reflow(root);
        // Returning to 1x can still need the original title's natural height; never shrink its text.
        AssertFits(title);
    }

    [TestCase(1080f, 1920f, 720f)]
    [TestCase(1170f, 2532f, 900f)]
    public void BottomActionRemainsAboveTheSafeAndKeyboardInsets(float width, float height, float keyboard)
    {
        var root = Container(width, height);
        var safe = SampleUi.SafeArea("existing-safe-area", root);
        var button = SampleUi.Button("existing-open", safe, "Open", () => { });
        button.anchorMin = button.anchorMax = button.pivot = Vector2.zero;
        button.anchoredPosition = Vector2.zero;
        button.sizeDelta = new Vector2(600f, 144f);
        safe.GetComponent<SampleUiSafeArea>().Apply(new Rect(0f, 60f, width, height - 120f),
            new Rect(0f, 0f, width, keyboard), new Vector2(width, height));
        Reflow(root);
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, button);
        Assert.GreaterOrEqual(bounds.min.y, root.rect.yMin + keyboard - 1f);
        Assert.LessOrEqual(bounds.max.y, root.rect.yMax - 60f + 1f);
        Assert.AreEqual("existing-open", button.name);
    }

    [Test]
    public void ShortKeyboardViewportScrollsTheWholePageInsteadOfOverlappingItsBars()
    {
        var root = Container(1080f, 600f);
        var safe = SampleUi.SafeArea("existing-safe-area", root);
        var header = SampleUi.Panel("Header", safe, SampleUi.Background);
        SampleUi.Stretch(header, new Vector2(0f, 1f), Vector2.one);
        header.pivot = new Vector2(0.5f, 1f);
        header.sizeDelta = new Vector2(0f, 192f);
        var button = SampleUi.Button("existing-open", safe, "Open", () => { });
        button.anchorMin = Vector2.zero;
        button.anchorMax = new Vector2(1f, 0f);
        button.pivot = new Vector2(0.5f, 0f);
        button.sizeDelta = new Vector2(0f, 144f);
        SampleUi.VerticalScroll(safe, new RectOffset(0, 0, 192, 144));
        safe.GetComponent<SampleUiSafeArea>().Apply(new Rect(0f, 0f, 1080f, 600f),
            new Rect(0f, 0f, 1080f, 300f), new Vector2(1080f, 600f));
        Reflow(root);
        var scroll = safe.parent.GetComponent<ScrollRect>();
        Assert.IsTrue(scroll.vertical);
        Assert.Greater(safe.rect.height, scroll.viewport.rect.height);
        scroll.verticalNormalizedPosition = 0f;
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, button);
        Assert.GreaterOrEqual(bounds.min.y, scroll.viewport.rect.yMin - 1f);
        Assert.LessOrEqual(bounds.max.y, scroll.viewport.rect.yMax + 1f);
    }

    [Test]
    public void FocusedSearchCanBeRevealedWithoutChangingItsQueryOrObject()
    {
        var root = Container(1080f, 700f);
        var content = SampleUi.VerticalScroll(root, new RectOffset());
        SampleUi.FlexibleLabel(content, new string('\n', 20), SampleUi.TextBody, SampleUi.TitleColor);
        var input = (SampleUiInputField)SampleUi.Field("scenarios-search-input", content, "locale");
        Reflow(root);
        input.RevealInScrollView();
        var viewport = (RectTransform)content.parent;
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, input.transform);
        Assert.GreaterOrEqual(bounds.min.y, viewport.rect.yMin - 1f);
        Assert.LessOrEqual(bounds.max.y, viewport.rect.yMax + 1f);
        Assert.AreEqual("locale", input.text);
        Assert.AreEqual("scenarios-search-input", input.name);
    }

    private RectTransform Column(float widthDp)
    {
        var column = Container(SampleUi.ReferenceWidthFor(widthDp), 5000f);
        SampleUi.VerticalStack(column, 24f, new RectOffset(), false);
        return column;
    }

    private RectTransform Container(float width, float height)
    {
        var rect = SampleUi.Panel("Container", _host.transform, SampleUi.Background);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static void Reflow(RectTransform root)
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    private static void AssertFits(TMP_Text text)
    {
        Assert.IsFalse(text.enableAutoSizing, text.name);
        Assert.AreSame(SampleUi.UiFont, text.font);
        Assert.GreaterOrEqual(text.rectTransform.rect.height + 1f, text.preferredHeight, text.name + ": " + text.text);
    }

    private static Transform Find(Transform root, string name)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        Assert.Fail("Missing existing id: " + name);
        return null;
    }
}
