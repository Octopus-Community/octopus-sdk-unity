using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The sample's shared uGUI builders.
///
/// Every screen this sample builds in code (the QA scenario list, the scenario screens) goes
/// through here rather than through a prefab or a hand-edited scene YAML. That is the same
/// deliberate choice <see cref="OctopusScenariosListView"/> already documented: a screen that
/// tracks a catalogue living in another repo has to be diffable and reviewable, and serialized
/// YAML is neither.
///
/// The styles below are the ones the scenario list shipped with — extracted here unchanged so the
/// two screens cannot drift apart, not re-designed. Sizes are expressed in the 1080x1920 canvas
/// reference every code-built screen in this sample uses; reference and sizes only mean anything
/// together, so a caller that picks another reference must re-read every constant here.
/// </summary>
public static class SampleUi
{
    /// <summary>The 1080x1920 canvas reference every code-built screen here is measured in.</summary>
    public static readonly Vector2 CanvasReference = new Vector2(1080f, 1920f);

    // The content opacity floor posed by pm-tools `shared/design/samples/TOKENS.md`: any text
    // that carries meaning stays at or above it, never below.
    private const float ContentAlphaFloor = 0.74f;

    /// <summary>The screen ground.</summary>
    public static readonly Color Background = new Color(0.08f, 0.08f, 0.08f, 0.97f);

    /// <summary>A card / section ground, one step above <see cref="Background"/>.</summary>
    public static readonly Color RowBackground = new Color(0.16f, 0.16f, 0.16f, 1f);

    /// <summary>An input's ground — a further step up, so a field reads as a field.</summary>
    public static readonly Color FieldBackground = new Color(0.24f, 0.24f, 0.24f, 1f);

    /// <summary>Primary text.</summary>
    public static readonly Color TitleColor = Color.white;

    /// <summary>Secondary text, at the content opacity floor and never below it.</summary>
    public static readonly Color Muted = new Color(1f, 1f, 1f, ContentAlphaFloor);

    /// <summary>
    /// The platform slot hue, dark half — the same value the other samples give this platform.
    /// TOKENS.md reserves it for identity, so it dresses screen titles and nothing else: a status
    /// is a semantic element, and tinting one with the slot hue is what makes two samples look
    /// like two different products.
    /// </summary>
    public static readonly Color PlatformSlot = new Color(0.471f, 0.780f, 0.482f);

    /// <summary>A gap to close. A permanent absence uses <see cref="Muted"/> instead.</summary>
    public static readonly Color Attention = new Color(0.98f, 0.75f, 0.35f, 1f);

    /// <summary>A button's ground.</summary>
    public static readonly Color ButtonFill = new Color(0.008f, 0.298f, 0.357f, 1f);

    private static Font _font;

    /// <summary>The one font every code-built screen here uses.</summary>
    public static Font UiFont
    {
        get { return _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")); }
    }

    /// <summary>
    /// Adds a screen-space overlay canvas to <paramref name="host"/>, scaled against
    /// <see cref="CanvasReference"/>.
    /// </summary>
    /// <param name="host">The GameObject that owns the canvas.</param>
    /// <param name="sortingOrder">Above the scene's own canvas, whatever order it chose.</param>
    public static Canvas OverlayCanvas(GameObject host, int sortingOrder)
    {
        var canvas = host.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = host.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = CanvasReference;
        scaler.matchWidthOrHeight = 0.5f;
        host.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>A rectangle with a solid fill. Transparent fills stop raycasting.</summary>
    public static RectTransform Panel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = color.a > 0f;
        return rect;
    }

    /// <summary>A wrapping, non-raycasting text.</summary>
    public static Text Label(string name, Transform parent, string text, int size, Color color,
                             TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<Text>();
        label.font = UiFont;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = anchor;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        return label;
    }

    /// <summary>
    /// A text line sized by a parent layout group. Only <c>flexibleWidth</c> is set, deliberately:
    /// leaving <c>preferredHeight</c> unset lets Text report the height its own wrapped content
    /// needs, instead of pinning every line to a guessed constant.
    /// </summary>
    public static Text FlexibleLabel(Transform parent, string text, int size, Color color)
    {
        var label = Label("Line", parent, text, size, color, TextAnchor.UpperLeft);
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        return label;
    }

    /// <summary>A filled button with a centred caption.</summary>
    public static RectTransform Button(string name, Transform parent, string text,
                                       UnityEngine.Events.UnityAction onClick)
    {
        var rect = Panel(name, parent, ButtonFill);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(onClick);

        var label = Label("Label", rect, text, 26, Color.white, TextAnchor.MiddleCenter);
        Stretch(label.rectTransform, Vector2.zero, Vector2.one);
        return rect;
    }

    /// <summary>
    /// A single-line text field, pre-filled with <paramref name="value"/>.
    ///
    /// Editable on purpose, and never the way a scenario is driven: a preset fills every field so
    /// a tap is the only interaction the QA Tester needs (SDK_STANDARDS §5.2). The field is there
    /// so a human reader can see what the preset put in, and tweak it when exploring by hand.
    /// </summary>
    public static InputField Field(string name, Transform parent, string value)
    {
        var rect = Panel(name, parent, FieldBackground);

        var text = Label("Text", rect, value, 24, TitleColor, TextAnchor.MiddleLeft);
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.raycastTarget = true;
        Stretch(text.rectTransform, Vector2.zero, Vector2.one);
        text.rectTransform.offsetMin = new Vector2(16f, 0f);
        text.rectTransform.offsetMax = new Vector2(-16f, 0f);

        var input = rect.gameObject.AddComponent<InputField>();
        input.targetGraphic = rect.GetComponent<Image>();
        input.textComponent = text;
        input.lineType = InputField.LineType.SingleLine;
        input.text = value;
        return input;
    }

    /// <summary>Turns <paramref name="rect"/> into a top-anchored, self-sizing vertical stack.</summary>
    public static VerticalLayoutGroup VerticalStack(RectTransform rect, float spacing,
                                                    RectOffset padding, bool selfSizing)
    {
        var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = padding;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        if (selfSizing)
        {
            // Only for a stack whose own height nothing else controls. Unity warns about a layout
            // group child that fits its own content, so a nested stack must pass false here.
            rect.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
        }
        return layout;
    }

    /// <summary>
    /// Fills <paramref name="parent"/> with a vertical scroll view and returns its content, ready
    /// to receive rows.
    /// </summary>
    /// <param name="insets">Left / right / top / bottom insets of the viewport, in canvas units.</param>
    public static RectTransform VerticalScroll(RectTransform parent, RectOffset insets)
    {
        var viewport = Panel("Viewport", parent, Color.clear);
        Stretch(viewport, Vector2.zero, Vector2.one);
        viewport.offsetMin = new Vector2(insets.left, insets.bottom);
        viewport.offsetMax = new Vector2(-insets.right, -insets.top);
        viewport.gameObject.AddComponent<RectMask2D>();
        // Panel() derives raycastTarget from alpha, which is right everywhere but here: uGUI walks
        // UP from the graphic it hit, so a drag starting in the gap between two rows would hit the
        // viewport's parent — not a descendant of the ScrollRect — and scroll nothing.
        viewport.GetComponent<Image>().raycastTarget = true;

        var content = Panel("Content", viewport, Color.clear);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        content.anchoredPosition = Vector2.zero;
        VerticalStack(content, 16f, new RectOffset(0, 0, 0, 0), true);

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 40f;
        return content;
    }

    /// <summary>Anchors <paramref name="rect"/> between two normalized corners, with no inset.</summary>
    public static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
