using TMPro;
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class SampleUiPrimitiveTests
{
    private GameObject _host;
    private GameObject _events;
    private OctopusSampleTheme _oldTheme;

    [SetUp]
    public void SetUp()
    {
        _oldTheme = OctopusSampleBranding.Theme;
        _host = new GameObject("Primitive test", typeof(RectTransform), typeof(Canvas));
        _host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        if (EventSystem.current == null) _events = new GameObject("Events", typeof(EventSystem));
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_host);
        if (_events != null) Object.DestroyImmediate(_events);
        OctopusSampleBranding.Theme = _oldTheme;
    }

    [TestCase(false, "")]
    [TestCase(false, "Sign")]
    [TestCase(true, "")]
    [TestCase(true, "Sign")]
    public void FieldPlaceholderSharesTheInsetAndMaskOfEnteredText(bool scenarios, string value)
    {
        var host = SampleUi.Panel("Field test content", _host.transform, SampleUi.Background);
        host.sizeDelta = new Vector2(1080f, 1800f);
        TMP_InputField input;
        if (scenarios)
        {
            OctopusScenariosListView.BuildInto(host, new OctopusScenariosListView.ViewState { Query = value });
            input = Array.Find(host.GetComponentsInChildren<TMP_InputField>(),
                field => field.name == OctopusScenariosListView.SearchInputId);
        }
        else
        {
            SampleUi.VerticalStack(host, 0f, new RectOffset(), false);
            input = SampleUi.LabeledField("field", host, "Label", value, placeholder: "Search scenarios");
        }

        Assert.IsNotNull(input);
        Assert.AreEqual(value, input.text);
        var hint = input.placeholder;
        Assert.IsNotNull(hint);
        Assert.AreEqual("Placeholder", hint.name);
        Assert.AreEqual(string.IsNullOrEmpty(value), hint.enabled,
            "Restored text must disable the hint before the first canvas update.");
        Assert.IsTrue(hint.transform.IsChildOf(input.textViewport));
        Assert.IsTrue(input.textViewport.GetComponent<RectMask2D>().isActiveAndEnabled);
        Assert.IsFalse(hint.GetComponent<TMP_Text>().richText);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(host);
        var fieldRect = (RectTransform)input.transform;
        var hintCorners = new Vector3[4];
        var textCorners = new Vector3[4];
        hint.rectTransform.GetWorldCorners(hintCorners);
        input.textComponent.rectTransform.GetWorldCorners(textCorners);
        for (int i = 0; i < hintCorners.Length; i++)
        {
            var hintCorner = fieldRect.InverseTransformPoint(hintCorners[i]);
            var textCorner = fieldRect.InverseTransformPoint(textCorners[i]);
            Assert.AreEqual(textCorner.x, hintCorner.x, 0.1f);
            Assert.AreEqual(textCorner.y, hintCorner.y, 0.1f);
        }
        float leftInset = fieldRect.InverseTransformPoint(hintCorners[0]).x - fieldRect.rect.xMin;
        Assert.AreEqual(OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceMd), leftInset, 0.1f);
        Assert.GreaterOrEqual(fieldRect.rect.height, OctopusSampleBranding.MinTouchUnits);
        Assert.IsTrue(fieldRect.GetComponent<Image>().raycastTarget);
        Assert.AreEqual(string.IsNullOrEmpty(value), hint.enabled);
    }

    [Test]
    public void FieldErrorSiblingIsReleasedAfterSafeEditorTeardown()
    {
        var input = (SampleUiInputField)SampleUi.LabeledField("field", _host.transform,
            "Label", "value", placeholder: "Hint");
        input.SetError("Invalid input");
        var parent = input.transform.parent;
        TMP_Text error = null;
        foreach (var text in parent.GetComponentsInChildren<TMP_Text>())
            if (text.text == "Invalid input") error = text;
        Assert.IsNotNull(error);
        Object.DestroyImmediate(input.gameObject);
        Assert.IsTrue(error != null, "The orphan outlives OnDestroy: DestroyImmediate is refused there.");
        // The editor flushes on its next tick; a batch-mode run never reaches one, so flush here.
        SampleUiInputField.FlushEditorTeardown();
        Assert.IsTrue(error == null);
    }

    [Test]
    public void ReleasingShapesDestroysBothNativeObjectsAndRecreatesTheCache()
    {
        var sprite = SampleUiShapes.Rounded(16f);
        var texture = sprite.texture;
        SampleUiShapes.Release();
        Assert.IsTrue(sprite == null);
        Assert.IsTrue(texture == null);
        var replacement = SampleUiShapes.Rounded(16f);
        Assert.IsTrue(replacement != null);
        Assert.AreSame(replacement, SampleUiShapes.Rounded(16f));
    }

    [Test]
    public void ListDetailRestoresItsConfiguredInkAfterBeingDisabled()
    {
        var row = SampleUi.ListRow("row", _host.transform, "Title", "Detail", () => { });
        var button = row.GetComponent<SampleUiButton>();
        var texts = row.GetComponentsInChildren<TMP_Text>();
        TMP_Text detail = null;
        foreach (var text in texts) if (text.text == "Detail") detail = text;
        detail.color = OctopusSampleBranding.Palette.Positive;
        button.Configure(row.GetComponent<Image>(), row.Find(SampleUi.StrokeName).GetComponent<Image>(),
            row.Find("Copy/Label").GetComponent<TMP_Text>(), SampleUi.RowBackground, SampleUi.TitleColor,
            OctopusSampleBranding.Palette.Border, OctopusSampleBranding.CardRadius, detail: detail);
        Assert.AreEqual(OctopusSampleBranding.Palette.Positive, detail.color);
        button.interactable = false;
        Assert.AreEqual(OctopusSampleBranding.Palette.DisabledInk, detail.color);
        button.interactable = true;
        Assert.AreEqual(OctopusSampleBranding.Palette.Positive, detail.color);
    }

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void ScrollDragCancelsRowPressWithoutLeavingAFocusOutline(OctopusSampleTheme theme)
    {
        OctopusSampleBranding.Theme = theme;
        var events = new GameObject("Drag events", typeof(EventSystem), typeof(StandaloneInputModule));
        try
        {
            var system = events.GetComponent<EventSystem>();
            var module = events.GetComponent<StandaloneInputModule>();
            // EditMode does not run the input module lifecycle or its frame loop.
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic;
            typeof(BaseInputModule).GetMethod("OnEnable", flags).Invoke(module, null);
            var content = SampleUi.VerticalScroll((RectTransform)_host.transform, new RectOffset());
            int clicks = 0;
            var row = SampleUi.ListRow("row", content, "Title", "Detail", () => clicks++);
            Canvas.ForceUpdateCanvases();
            var button = row.GetComponent<SampleUiButton>();
            var fill = row.GetComponent<Image>();
            var stroke = row.Find(SampleUi.StrokeName).GetComponent<Image>();
            var normal = fill.color;
            var border = stroke.color;
            var sprite = stroke.sprite;
            var pointer = new PointerEventData(system)
            {
                pointerId = 0,
                button = PointerEventData.InputButton.Left,
                eligibleForClick = true,
                pointerDrag = content.parent.gameObject,
                pressPosition = Vector2.zero
            };
            button.OnPointerEnter(pointer);
            Assert.AreEqual(border, stroke.color, "Pointer hover must not draw navigation focus.");
            button.OnPointerDown(pointer);
            button.OnSelect(pointer);
            pointer.pointerPress = row.gameObject;
            Assert.AreNotEqual(normal, fill.color, "Touch-down retains tap feedback before a drag is recognized.");
            ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.initializePotentialDrag);
            pointer.position = new Vector2(0f, system.pixelDragThreshold + 20f);
            pointer.delta = pointer.position;
            typeof(PointerInputModule).GetMethod("ProcessDrag", flags).Invoke(module, new object[] { pointer });
            Assert.IsTrue(pointer.dragging);
            Assert.IsFalse(pointer.eligibleForClick);
            Assert.IsNull(pointer.pointerPress);
            Assert.AreEqual(normal, fill.color, "The input module must cancel row press as scrolling begins.");
            Assert.AreEqual(border, stroke.color);
            Assert.AreSame(sprite, stroke.sprite);
            button.OnPointerExit(pointer);
            button.OnPointerEnter(pointer);
            Assert.AreEqual(normal, fill.color);
            Assert.AreEqual(border, stroke.color, "Crossing a row during a drag must not highlight it.");
            ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.endDragHandler);
            Assert.AreEqual(0, clicks);

            pointer.dragging = false;
            button.OnPointerDown(pointer);
            Assert.AreNotEqual(normal, fill.color);
            button.OnPointerUp(pointer);
            button.OnPointerClick(pointer);
            Assert.AreEqual(1, clicks, "A subsequent tap still activates the row.");
            Assert.AreEqual(normal, fill.color);
            Assert.AreEqual(border, stroke.color);
            button.OnDeselect(new BaseEventData(system));
            button.OnSelect(new BaseEventData(system));
            Assert.AreEqual(OctopusSampleBranding.Palette.Focus, stroke.color,
                "Keyboard navigation keeps its visible focus outline.");
        }
        finally { Object.DestroyImmediate(events); }
    }

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void EveryButtonVariantHasPressedAndDisabledStatesAndKeepsItsClick(OctopusSampleTheme theme)
    {
        OctopusSampleBranding.Theme = theme;
        foreach (SampleUiButtonVariant variant in Enum.GetValues(typeof(SampleUiButtonVariant)))
        {
            int clicks = 0;
            var rect = SampleUi.Button("existing-id", _host.transform, "Action", variant, () => clicks++);
            var button = rect.GetComponent<Button>();
            var image = rect.GetComponent<Image>();
            var label = rect.GetComponentInChildren<TMP_Text>();
            Color normal = image.color;
            if (variant == SampleUiButtonVariant.Tertiary)
                Assert.AreEqual(OctopusSampleBranding.Palette.ControlBorder,
                    rect.Find(SampleUi.StrokeName).GetComponent<Image>().color);
            Assert.AreEqual("existing-id", rect.name);
            Assert.IsTrue(image.raycastTarget, "The tertiary button must also receive a click over empty padding.");
            AssertTouchFloor(rect);
            Assert.IsFalse(label.enableAutoSizing);
            Assert.AreSame(SampleUi.UiFont, label.font);
            Assert.AreEqual(FontStyles.Bold, label.fontStyle);

            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            button.OnPointerEnter(pointer);
            button.OnPointerDown(pointer);
            Assert.AreNotEqual(normal, image.color, variant + " pressed");
            Assert.AreEqual(1f, image.color.a, "Pressed composites are opaque, including tertiary.");
            button.OnPointerUp(pointer);
            button.OnPointerClick(pointer);
            Assert.AreEqual(1, clicks);
            button.interactable = false;
            Assert.AreEqual(OctopusSampleBranding.Palette.DisabledSurface, image.color);
            Assert.AreEqual(OctopusSampleBranding.Palette.DisabledInk, label.color);
            button.OnPointerClick(pointer);
            Assert.AreEqual(1, clicks, "Disabled must not invoke the action.");
            button.interactable = true;
            button.OnPointerExit(pointer);
            EventSystem.current?.SetSelectedGameObject(null); // No EventSystem in EditMode; pressing never selects then.
            Assert.AreEqual(normal, image.color);
            Object.DestroyImmediate(rect.gameObject);
        }
    }

    [TestCase(OctopusSampleTheme.Light)]
    [TestCase(OctopusSampleTheme.Dark)]
    public void FieldsExposeFocusErrorDisabledAndPersistentLabel(OctopusSampleTheme theme)
    {
        OctopusSampleBranding.Theme = theme;
        var p = OctopusSampleBranding.Palette;
        var input = SampleUi.LabeledField("existing-field", _host.transform, "Persistent label", "", false, "Hint");
        var border = input.transform.Find("Stroke").GetComponent<Image>();
        Assert.AreEqual("existing-field", input.name);
        Assert.AreEqual(p.ControlBorder, border.color);
        Assert.IsTrue(input.GetComponent<Image>().raycastTarget);
        Assert.AreEqual(p.Placeholder, input.placeholder.color);
        AssertTouchFloor((RectTransform)input.transform);
        input.OnSelect(new BaseEventData(EventSystem.current));
        Assert.AreEqual(p.Focus, border.color);
        Assert.AreSame(SampleUiShapes.Rounded(12f, 2f), border.sprite);
        SampleUi.SetFieldError(input, "A value is required.");
        Assert.AreEqual(p.Negative, border.color);
        var texts = input.transform.parent.GetComponentsInChildren<TMP_Text>();
        Assert.IsTrue(Array.Exists(texts, text => text.text == "Persistent label"));
        Assert.IsTrue(Array.Exists(texts, text => text.text == "A value is required." && text.color == p.Negative));
        input.interactable = false;
        Assert.AreEqual(p.DisabledSurface, input.GetComponent<Image>().color);
        Assert.AreEqual(p.DisabledInk, input.textComponent.color);
        Assert.AreEqual(p.Border, border.color);
        input.interactable = true;
        SampleUi.SetFieldError(input, "");
        input.OnDeselect(new BaseEventData(EventSystem.current));
        Assert.AreEqual(p.ControlBorder, border.color);
        Assert.AreEqual("", ((SampleUiInputField)input).Error);
        Assert.IsFalse(Array.Exists(input.transform.parent.GetComponentsInChildren<TMP_Text>(),
            text => text.text == "A value is required."));
    }

    [TestCase(OctopusSampleTheme.Light, false)]
    [TestCase(OctopusSampleTheme.Light, true)]
    [TestCase(OctopusSampleTheme.Dark, false)]
    [TestCase(OctopusSampleTheme.Dark, true)]
    public void SwitchHasCompactTrackFullHitRegionAndLockedState(OctopusSampleTheme theme, bool on)
    {
        OctopusSampleBranding.Theme = theme;
        int clicks = 0;
        var rect = SampleUi.Switch("existing-switch", _host.transform, on, true, () => clicks++);
        var track = rect.Find("Track").GetComponent<Image>();
        var knob = (RectTransform)rect.Find("Knob");
        Assert.AreEqual("existing-switch", rect.name);
        AssertTouchFloor(rect);
        Assert.AreEqual(new Vector2(156f, 96f), track.rectTransform.sizeDelta);
        Assert.AreEqual(new Vector2(66f, 66f), knob.sizeDelta);
        Assert.AreEqual(on ? 30f : -30f, knob.anchoredPosition.x);
        Assert.AreEqual(on ? SampleUi.Accent : SampleUi.FieldBackground, track.color);
        var button = rect.GetComponent<Button>();
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        button.OnPointerClick(pointer);
        Assert.AreEqual(1, clicks);
        button.interactable = false;
        Assert.AreEqual(OctopusSampleBranding.Palette.DisabledSurface, track.color);
        button.OnPointerClick(pointer);
        Assert.AreEqual(1, clicks);
        Assert.AreEqual(on ? 30f : -30f, knob.anchoredPosition.x, "Locking preserves the value.");
    }

    [TestCase(360f)]
    [TestCase(390f)]
    public void CardsRowsAndWrappedButtonsGrowWithContent(float widthDp)
    {
        var root = (RectTransform)_host.transform;
        var column = SampleUi.Panel("Column", root, SampleUi.Background);
        column.sizeDelta = new Vector2(SampleUi.ReferenceWidthFor(widthDp), 2000f);
        SampleUi.VerticalStack(column, 24f, new RectOffset(), false);
        var card = SampleUi.Card("existing-card", column);
        var stack = card.GetComponent<VerticalLayoutGroup>();
        Assert.AreEqual(48, stack.padding.left);
        Assert.AreEqual(48, stack.padding.top);
        Assert.AreEqual(24f, stack.spacing);
        Assert.IsNull(card.GetComponent<ContentSizeFitter>());
        const string longText = "A long action or description that must wrap without shrinking the font. ";
        var row = SampleUi.ListRow("existing-row", card, "Title", longText + longText, () => { });
        var button = SampleUi.Button("existing-button", card, longText + longText, () => { });
        LayoutRebuilder.ForceRebuildLayoutImmediate(column);
        Assert.GreaterOrEqual(row.rect.height, 192f);
        AssertTouchFloor(button);
        Assert.Greater(button.rect.height, OctopusSampleBranding.MinTouchUnits);
        foreach (var label in card.GetComponentsInChildren<TMP_Text>())
        {
            Assert.IsFalse(label.enableAutoSizing);
            Assert.GreaterOrEqual(label.fontSize, 36);
            Assert.AreSame(SampleUi.UiFont, label.font);
            Assert.GreaterOrEqual(label.rectTransform.rect.height + 1f, label.preferredHeight);
        }
    }

    [Test]
    public void TouchFloorAppliesToActualManuallySizedRectangles()
    {
        var rect = SampleUi.Button("existing-button", _host.transform, "Go", () => { });
        rect.sizeDelta = Vector2.one;
        typeof(SampleUiTouchTarget).GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).Invoke(rect.GetComponent<SampleUiTouchTarget>(), null);
        AssertTouchFloor(rect);
    }

    [Test]
    public void ShapesAreSharedAcrossInstancesThemesAndStateRebuilds()
    {
        var fill = SampleUiShapes.Rounded(16f);
        var stroke = SampleUiShapes.Rounded(12f, 2f);
        Assert.AreNotSame(fill, stroke);
        foreach (var theme in new[] { OctopusSampleTheme.Light, OctopusSampleTheme.Dark })
        {
            OctopusSampleBranding.Theme = theme;
            for (int i = 0; i < 10; i++)
            {
                var card = SampleUi.Card("existing-card", _host.transform);
                Assert.AreSame(fill, card.GetComponent<Image>().sprite);
                Assert.AreSame(stroke, SampleUiShapes.Rounded(12f, 2f));
            }
        }
    }

    private static void AssertTouchFloor(RectTransform rect)
    {
        Assert.GreaterOrEqual(rect.rect.width, 144f);
        Assert.GreaterOrEqual(rect.rect.height, 144f);
        Assert.GreaterOrEqual(rect.GetComponent<LayoutElement>().minWidth, 144f);
        Assert.GreaterOrEqual(rect.GetComponent<LayoutElement>().minHeight, 144f);
    }
}
