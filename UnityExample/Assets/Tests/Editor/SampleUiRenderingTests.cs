using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SampleUiRenderingTests
{
    [TestCase(1f)]
    [TestCase(0.875f)]
    [TestCase(1.25f)]
    public void ScrollingKeepsGroupOutlinesOnPhysicalPixels(float scale)
    {
        var host = new GameObject("Scrolling border test", typeof(RectTransform));
        try
        {
            var canvas = SampleUi.OverlayCanvas(host, 0);
            Assert.IsTrue(canvas.pixelPerfect);
            host.GetComponent<CanvasScaler>().enabled = false;
            canvas.scaleFactor = scale;
            var content = SampleUi.VerticalScroll((RectTransform)host.transform, new RectOffset());
            var card = SampleUi.Card("group", content);
            SampleUi.FlexibleLabel(card, "Connection", SampleUi.TextBody, SampleUi.TitleColor);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Assert.IsNull(card.GetComponent<Selectable>(), "Group borders have no interaction state.");
            var stroke = card.Find(SampleUi.StrokeName).GetComponent<Image>();
            var sprite = stroke.sprite;
            var color = stroke.color;
            for (int step = 0; step < 32; step++)
            {
                content.anchoredPosition = new Vector2(0f, step / 8f);
                Rect adjusted = stroke.GetPixelAdjustedRect();
                foreach (var corner in new[] { adjusted.min, adjusted.max })
                {
                    var screen = RectTransformUtility.WorldToScreenPoint(null,
                        stroke.transform.TransformPoint(corner));
                    Assert.That(screen.x, Is.EqualTo(Mathf.Round(screen.x)).Within(0.002f));
                    Assert.That(screen.y, Is.EqualTo(Mathf.Round(screen.y)).Within(0.002f));
                }
                Assert.AreSame(sprite, stroke.sprite);
                Assert.AreEqual(color, stroke.color);
            }
        }
        finally { Object.DestroyImmediate(host); }
    }

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void ElevationHasAVisibleCompositeDeltaOnBothCardSurfaces(OctopusSampleTheme theme)
    {
        var palette = theme == OctopusSampleTheme.Dark ? OctopusSamplePalette.Dark() : OctopusSamplePalette.Light();
        Assert.AreEqual(theme == OctopusSampleTheme.Dark ? OctopusSampleBranding.DarkElevationInk
            : OctopusSampleBranding.LightElevationInk, palette.ElevationInk);
        foreach (var surface in new[] { palette.Page, palette.Surface, palette.SurfaceHigh })
        {
            var composite = OctopusSampleBranding.Tint(palette.ElevationInk, surface,
                OctopusSampleBranding.ElevationOpacity);
            Assert.Greater(Mathf.Abs(composite.grayscale - surface.grayscale), 0.08f);
        }
    }

    [Test]
    public void SmallSlicedShapesKeepEveryVertexInsideTheirRectangle()
    {
        var host = new GameObject("Small shape", typeof(RectTransform), typeof(Canvas));
        try
        {
            var rect = SampleUi.Panel("shape", host.transform, SampleUi.RowBackground, 16f, SampleUi.Muted, 0f);
            rect.sizeDelta = Vector2.one * OctopusSampleBranding.Dp(16f);
            var image = rect.GetComponent<Image>();
            using (var mesh = new VertexHelper())
            {
                typeof(Image).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null)
                    .Invoke(image, new object[] { mesh });
                Assert.Greater(mesh.currentVertCount, 0);
                var vertex = new UIVertex();
                for (int i = 0; i < mesh.currentVertCount; i++)
                {
                    mesh.PopulateUIVertex(ref vertex, i);
                    Assert.That(vertex.position.x, Is.InRange(rect.rect.xMin, rect.rect.xMax));
                    Assert.That(vertex.position.y, Is.InRange(rect.rect.yMin, rect.rect.yMax));
                }
            }
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void InterUsesSharedDynamicAtlasesRealBoldAndDefaultGlyphFallback()
    {
        var font = SampleUi.UiFont;
        Assert.IsNotNull(font);
        Assert.AreNotSame(TMP_Settings.defaultFontAsset, font, "Bundled Inter must load, not silently fall back.");
        Assert.AreSame(font, SampleUi.UiFont);
        Assert.AreEqual("Inter-Regular", font.sourceFontFile.name);
        Assert.AreEqual(AtlasPopulationMode.Dynamic, font.atlasPopulationMode);
        Assert.AreEqual(1024, font.atlasWidth);
        Assert.AreEqual(1024, font.atlasHeight);
        var bold = font.fontWeightTable[OctopusSampleBranding.StrongFontWeight / 100].regularTypeface;
        Assert.IsNotNull(bold);
        Assert.AreEqual("Inter-Bold", bold.sourceFontFile.name);
        Assert.Contains(TMP_Settings.defaultFontAsset, font.fallbackFontAssetTable);
        Assert.Contains(TMP_Settings.defaultFontAsset, bold.fallbackFontAssetTable);
        Assert.IsTrue(font.HasCharacter('é', false, true));
        Assert.IsTrue(bold.HasCharacter('é', false, true));
    }

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void FieldsKeepPaddedClippingPlainTextAndVisibleFocusAndError(OctopusSampleTheme theme)
    {
        var previous = OctopusSampleBranding.Theme;
        var host = new GameObject("Rendering test", typeof(RectTransform), typeof(Canvas));
        GameObject events = null;
        try
        {
            OctopusSampleBranding.Theme = theme;
            if (EventSystem.current == null) events = new GameObject("Events", typeof(EventSystem));
            var input = SampleUi.LabeledField("existing-field", host.transform, "Label", "<b>literal</b>", false, "Hint");
            Assert.IsInstanceOf<SampleUiInputField>(input);
            Assert.IsInstanceOf<TextMeshProUGUI>(input.textComponent);
            Assert.AreEqual("<b>literal</b>", input.text);
            Assert.IsFalse(input.richText);
            Assert.IsFalse(input.textComponent.richText);
            Assert.IsFalse(input.placeholder.enabled);
            input.text = string.Empty;
            input.ForceLabelUpdate();
            Assert.IsTrue(input.placeholder.enabled);
            input.text = "<b>literal</b>";
            input.ForceLabelUpdate();
            Assert.IsFalse(input.placeholder.enabled);
            Assert.IsNotNull(input.textViewport.GetComponent<RectMask2D>());
            Assert.AreEqual(Vector2.one * OctopusSampleBranding.Dp(12f), input.textViewport.offsetMin);
            Assert.AreSame(input.textViewport, input.textComponent.transform.parent);
            Assert.AreSame(input.textViewport, input.placeholder.transform.parent);
            var styled = (SampleUiInputField)input;
            var border = input.transform.Find("Stroke").GetComponent<Image>();
            input.OnSelect(new BaseEventData(EventSystem.current));
            styled.RefreshVisuals();
            Assert.AreEqual(OctopusSampleBranding.Palette.Focus, border.color);
            styled.SetError("Invalid value");
            Assert.AreEqual(OctopusSampleBranding.Palette.Negative, border.color);
            input.interactable = false;
            Assert.AreEqual(OctopusSampleBranding.Palette.DisabledInk, input.textComponent.color);
            Assert.AreEqual("<b>literal</b>", input.text);
        }
        finally
        {
            Object.DestroyImmediate(host);
            if (events != null) Object.DestroyImmediate(events);
            OctopusSampleBranding.Theme = previous;
        }
    }

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void RoundedSurfacesShareMasksAndElevationKeepsTheSourceGeometry(OctopusSampleTheme theme)
    {
        var previous = OctopusSampleBranding.Theme;
        var host = new GameObject("Rendering test", typeof(RectTransform), typeof(Canvas));
        try
        {
            OctopusSampleBranding.Theme = theme;
            var first = SampleUi.Card("first", host.transform);
            var second = SampleUi.Card("second", host.transform);
            Assert.AreSame(first.GetComponent<Image>().sprite, second.GetComponent<Image>().sprite);
            Assert.AreEqual(Image.Type.Sliced, first.GetComponent<Image>().type);
            var effect = first.GetComponent<SampleUiElevation>();
            Assert.IsNotNull(effect);
            Assert.IsNotNull(SampleUi.Field("field", host.transform, "value").GetComponent<SampleUiElevation>());
            Assert.IsNotNull(SampleUi.Button("button", host.transform, "Action", () => { }).GetComponent<SampleUiElevation>());
            using (var vertices = new VertexHelper())
            {
                var quad = new UIVertex[4];
                for (int i = 0; i < quad.Length; i++)
                {
                    quad[i] = UIVertex.simpleVert;
                    quad[i].position = new Vector3(i % 2, i / 2, 0f);
                }
                vertices.AddUIVertexQuad(quad);
                effect.ModifyMesh(vertices);
                var shadow = new UIVertex();
                vertices.PopulateUIVertex(ref shadow, 0);
                Color shadowInk = shadow.color;
                var surface = OctopusSampleBranding.Palette.Surface;
                var composite = OctopusSampleBranding.Tint(shadowInk, surface,
                    1f - Mathf.Pow(1f - shadowInk.a, 8f));
                Assert.Greater(Mathf.Abs(composite.grayscale - surface.grayscale), 0.06f,
                    "The emitted elevation vertices must remain visible in this theme.");
                var last = new UIVertex();
                vertices.PopulateUIVertex(ref last, vertices.currentVertCount - 1);
                Assert.AreEqual(quad[0].color, last.color, "Original opaque geometry is rendered last.");
                Assert.Greater(vertices.currentVertCount, 6);
            }
        }
        finally
        {
            Object.DestroyImmediate(host);
            OctopusSampleBranding.Theme = previous;
        }
    }

    [TestCase("home")]
    [TestCase("science")]
    [TestCase("forum")]
    [TestCase("settings")]
    [TestCase("palette")]
    [TestCase("language")]
    [TestCase("info")]
    [TestCase("code")]
    [TestCase("description")]
    [TestCase("design_services")]
    [TestCase("bug_report")]
    [TestCase("feedback")]
    public void MaterialSymbolsHaveBothDensitiesAndAreCached(string symbol)
    {
        var small = Resources.Load<Sprite>("SampleIcons/" + symbol + "@2x");
        var large = Resources.Load<Sprite>("SampleIcons/" + symbol + "@3x");
        Assert.IsNotNull(small);
        Assert.IsNotNull(large);
        Assert.AreEqual(48, small.rect.width);
        Assert.AreEqual(72, large.rect.width);
        Assert.AreSame(SampleUiIcons.Get(symbol), SampleUiIcons.Get(symbol));
    }
}
