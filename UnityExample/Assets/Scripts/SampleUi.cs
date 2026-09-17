using TMPro;
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
/// The styles below are no longer this file's own: every colour resolves through
/// <see cref="OctopusSampleBranding"/>, the sample's implementation of the cross-platform token
/// contract, and every size is expressed at three canvas units per dp through that class's
/// dp/sp helpers. Reference and sizes only mean anything together, so a caller that picks another
/// reference must re-read every constant here.
///
/// The colours are properties rather than fields on purpose: the palette changes with the theme,
/// and a `static readonly Color` captured at class-init time would still be showing the dark values
/// after a switch to light.
/// </summary>
public static class SampleUi
{
    /// <summary>Fills one inset with the colour of the bar that ends there.</summary>
    public static RectTransform BuildBleed(RectTransform root, string name, Color color, bool top)
    {
        var bleed = SampleUi.Panel(name, root, color);
        bleed.anchorMin = top ? new Vector2(0f, 1f) : Vector2.zero;
        bleed.anchorMax = top ? Vector2.one : new Vector2(1f, 0f);
        bleed.pivot = new Vector2(0.5f, top ? 1f : 0f);
        bleed.anchoredPosition = Vector2.zero;
        ResizeBleed(bleed, top);
        return bleed;
    }

    public static void ResizeBleed(RectTransform bleed, bool top)
    {
        if (bleed == null) return;
        var area = Screen.safeArea;
        var pixels = top ? Screen.height - area.yMax : area.yMin;
        if (Screen.width <= 0 || Screen.height <= 0 || pixels <= 0f)
        {
            if (bleed.sizeDelta != Vector2.zero) bleed.sizeDelta = Vector2.zero;
            return;
        }

        var unitsPerPixel = SampleUi.ReferenceWidthFor(SampleUi.ScreenDpWidth()) / Screen.width;
        var size = new Vector2(0f, pixels * unitsPerPixel);
        if (bleed.sizeDelta != size) bleed.sizeDelta = size;
    }

    public const string StrokeName = "Stroke";
    // Settings details, scenario details and Developer tools share the detail layer over the shell.
    // The Debug entry and console stay above the Client profile page (1200) too; feedback opens over all.
    public const int DetailSortingOrder = 1100;
    public const int DebugEntrySortingOrder = 1250;
    public const int DebugConsoleSortingOrder = 1275;
    public const int FeedbackSortingOrder = 1300;

    // Injection seam: the publicly mirrored sample never references the private Debug assembly.
    public static RectTransform DebugEntry { get; private set; }
    public static event System.Action DebugEntryChanged;

    public static void RegisterDebugEntry(RectTransform entry)
    {
        // Keep the control above detail overlays even when a header adopts it into a lower canvas.
        if (entry != null)
        {
            var canvas = entry.GetComponent<Canvas>();
            if (canvas == null) canvas = entry.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = DebugEntrySortingOrder;
            if (entry.GetComponent<GraphicRaycaster>() == null)
                entry.gameObject.AddComponent<GraphicRaycaster>();
        }
        DebugEntry = entry;
        if (DebugEntryChanged != null) DebugEntryChanged();
    }

    /// <summary>
    /// Raised when a header claims the entry; every other holder gives it back to its own owner.
    /// The argument is the claimant, so a holder never releases on its own claim.
    /// </summary>
    public static event System.Action<object> DebugEntryClaimed;

    /// <summary>Announces that <paramref name="claimant"/> is taking the entry over.</summary>
    public static void ClaimDebugEntry(object claimant)
    {
        if (DebugEntryClaimed != null) DebugEntryClaimed(claimant);
    }

    /// <summary>The 1080x1920 canvas reference every code-built screen here is measured in.</summary>
    public static readonly Vector2 CanvasReference = new Vector2(1080f, 1920f);

    /// <summary>
    /// The device width, in dp, that <see cref="CanvasReference"/> assumes: 1080 units over 360dp is
    /// the 3 units per dp <see cref="OctopusSampleBranding.UnitsPerDp"/> states.
    /// </summary>
    public const float ReferenceDpWidth = 360f;

    /// <summary>A screen title, 22sp.</summary>
    public const int TextTitleXl = 66;

    /// <summary>A section or card title, 16sp.</summary>
    public const int TextTitle = 48;

    /// <summary>Body copy, 14sp.</summary>
    public const int TextBody = 42;

    /// <summary>Secondary copy, 12sp — the TOKENS §2 floor itself, never anything below it.</summary>
    public const int TextCaption = 36;

    /// <summary>The screen ground.</summary>
    public static Color Background { get { return OctopusSampleBranding.Palette.Page; } }

    /// <summary>A card / section ground, one step above <see cref="Background"/>.</summary>
    public static Color RowBackground { get { return OctopusSampleBranding.Palette.Surface; } }

    /// <summary>An input's ground — a further step up, so a field reads as a field.</summary>
    public static Color FieldBackground { get { return OctopusSampleBranding.Palette.SurfaceHigh; } }

    /// <summary>Primary text.</summary>
    public static Color TitleColor { get { return OctopusSampleBranding.Palette.Title; } }

    /// <summary>Secondary text, at the content opacity floor and never below it.</summary>
    public static Color Muted { get { return OctopusSampleBranding.Palette.Muted; } }

    /// <summary>
    /// This platform's identity hue. TOKENS §7 reserves it for the Home chip and the launcher badge
    /// — never for a semantic element, and never for a screen title, which is what it used to dress
    /// here. Titles take <see cref="TitleColor"/>; the accent role is <see cref="Accent"/>.
    /// </summary>
    public static Color PlatformSlot { get { return OctopusSampleBranding.Palette.PlatformSlot; } }

    /// <summary>The accent role for the theme in force — selected nav item, primary button fill.</summary>
    public static Color Accent { get { return OctopusSampleBranding.Palette.Accent; } }

    /// <summary>A gap to close. A permanent absence uses <see cref="Muted"/> instead.</summary>
    public static Color Attention { get { return OctopusSampleBranding.Palette.Attention; } }

    /// <summary>A button's ground: the accent, with <see cref="ButtonInk"/> on top.</summary>
    public static Color ButtonFill { get { return OctopusSampleBranding.Palette.Accent; } }

    /// <summary>The readable ink on <see cref="ButtonFill"/> — white would be 2.21:1 in dark theme.</summary>
    public static Color ButtonInk { get { return OctopusSampleBranding.Palette.OnAccent; } }

    /// <summary>
    /// A button's ground when it is drawn on the app bar rather than on the page. Never
    /// <see cref="ButtonFill"/> there: see <see cref="OctopusSamplePalette.ChromeControl"/>.
    /// </summary>
    public static Color ChromeButtonFill { get { return OctopusSampleBranding.Palette.ChromeControl; } }

    /// <summary>The ink on <see cref="ChromeButtonFill"/>: 5.22:1.</summary>
    public static Color ChromeButtonInk { get { return OctopusSampleBranding.Palette.OnChrome; } }

    /// <summary>The shared Inter font asset, with a real Bold face and TMP glyph fallback.</summary>
    public static TMP_FontAsset UiFont { get { return SampleUiFonts.Regular; } }

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
        // ScrollRect inertia uses fractional offsets. Snap rendered UI geometry so the sliced
        // outlines keep the same pixel coverage while the content moves.
        canvas.pixelPerfect = true;
        canvas.sortingOrder = sortingOrder;
        var scaler = host.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidthFor(ScreenDpWidth()),
                                                 CanvasReference.y);
        // Width-driven, not the 0.5 mix: with a mix, the units-per-dp a screen actually gets also
        // depends on its height, and the floor below stops being a floor on a short screen.
        scaler.matchWidthOrHeight = 0f;
        host.AddComponent<GraphicRaycaster>();
        host.AddComponent<SampleUiCanvas>();
        return canvas;
    }

    /// <summary>Three canvas units per dp at every width; unknown density uses the 360dp fallback.</summary>
    public static float ReferenceWidthFor(float dpWidth)
    {
        if (dpWidth <= 0f || float.IsNaN(dpWidth) || float.IsInfinity(dpWidth)) return CanvasReference.x;
        return dpWidth * OctopusSampleBranding.UnitsPerDp;
    }

    /// <summary>
    /// This screen's width in dp, or <see cref="ReferenceDpWidth"/> when the platform reports no
    /// usable density (<c>Screen.dpi</c> is 0 on more targets than it is documented to be, and a
    /// division by it is how a sample ends up with a canvas of nothing).
    /// </summary>
    public static float ScreenDpWidth()
    {
        var dpi = Screen.dpi;
        if (dpi <= 0f || float.IsNaN(dpi) || float.IsInfinity(dpi) || Screen.width <= 0) return ReferenceDpWidth;
        return Screen.width / (dpi / 160f);
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
        go.AddComponent<SampleUiAdaptivePanel>();
        return rect;
    }

    /// <summary>A rounded, optionally outlined panel. Geometry is shared through the sprite cache.</summary>
    public static RectTransform Panel(string name, Transform parent, Color color, float radiusDp,
                                      Color border, float strokeDp = OctopusSampleBranding.Stroke)
    {
        var rect = Panel(name, parent, color);
        var fill = rect.GetComponent<Image>();
        fill.sprite = SampleUiShapes.Rounded(radiusDp);
        fill.type = Image.Type.Sliced;
        if (radiusDp >= OctopusSampleBranding.FieldRadius && color.a > 0f)
            rect.gameObject.AddComponent<SampleUiElevation>();
        if (strokeDp > 0f) Stroke(rect, radiusDp, border, strokeDp);
        return rect;
    }

    private static Image Stroke(RectTransform parent, float radiusDp, Color color, float strokeDp)
    {
        var rect = Panel(StrokeName, parent, color);
        Stretch(rect, Vector2.zero, Vector2.one);
        var image = rect.GetComponent<Image>();
        image.sprite = SampleUiShapes.Rounded(radiusDp, strokeDp);
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        return image;
    }

    /// <summary>
    /// A wrapping, non-raycasting text.
    ///
    /// <paramref name="size"/> is clamped to <see cref="OctopusSampleBranding.MinTextUnits"/> — 12sp
    /// at this canvas reference. The floor is enforced here, once, rather than trusted to every call
    /// site: TOKENS §2 admits exactly one exception to it (a numeric badge counter) and this sample
    /// has none.
    /// </summary>
    public static TMP_Text Label(string name, Transform parent, string text, int size, Color color,
                             TextAnchor anchor, bool tabLabel = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TMP_Text>();
        label.font = UiFont;
        label.fontWeight = (FontWeight)OctopusSampleBranding.BodyFontWeight;
        label.text = text;
        label.fontSize = Mathf.Max(size, OctopusSampleBranding.MinTextUnits);
        label.enableAutoSizing = false;
        label.parseCtrlCharacters = false;
        label.fontStyle = size >= TextTitle ? FontStyles.Bold : FontStyles.Normal;
        // Authored line heights are preserved by the TMP scale adapter.
        float lineDp = size >= TextTitleXl ? 28f : size >= TextTitle ? 22f
            : size > TextCaption ? 20f : 18f;
        go.AddComponent<SampleUiScaledText>().Configure(size, OctopusSampleBranding.Dp(lineDp), tabLabel);
        label.color = color;
        label.alignment = Alignment(anchor);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }

    /// <summary>
    /// A text line sized by a parent layout group. Only <c>flexibleWidth</c> is set, deliberately:
    /// leaving <c>preferredHeight</c> unset lets Text report the height its own wrapped content
    /// needs, instead of pinning every line to a guessed constant.
    /// </summary>
    public static TMP_Text FlexibleLabel(Transform parent, string text, int size, Color color)
    {
        var label = Label("Line", parent, text, size, color, TextAnchor.UpperLeft);
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        return label;
    }

    /// <summary>A primary action. Existing callers keep their Button and Label objects.</summary>
    public static RectTransform Button(string name, Transform parent, string text,
                                       UnityEngine.Events.UnityAction onClick)
    {
        return Button(name, parent, text, SampleUiButtonVariant.Primary, onClick);
    }

    public static RectTransform Button(string name, Transform parent, string text,
                                       SampleUiButtonVariant variant, UnityEngine.Events.UnityAction onClick,
                                       float radiusDp = OctopusSampleBranding.ButtonRadius)
    {
        var p = OctopusSampleBranding.Palette;
        Color fill = variant == SampleUiButtonVariant.Primary ? p.Accent
            : variant == SampleUiButtonVariant.Destructive ? p.DangerSurface : p.SurfaceHigh;
        Color ink = variant == SampleUiButtonVariant.Primary ? p.OnAccent
            : variant == SampleUiButtonVariant.Destructive ? p.Negative : p.Accent;
        Color border = variant == SampleUiButtonVariant.Secondary ? p.ControlBorder
            : variant == SampleUiButtonVariant.Destructive ? p.Negative : fill;
        if (variant == SampleUiButtonVariant.Tertiary)
        {
            fill = OctopusSampleBranding.Clear;
            border = p.ControlBorder;
        }
        return BuildButton(name, parent, text, fill, ink, border, onClick, radiusDp, false);
    }

    /// <summary>Shared chrome; callers retain their existing header, title and back QA names.</summary>
    public static RectTransform AppBar(string name, RectTransform parent, string title,
        UnityEngine.Events.UnityAction onBack = null, string backName = "Back", string titleName = "Title")
    {
        var bar = Panel(name, parent, OctopusSampleBranding.Palette.Chrome);
        Stretch(bar, new Vector2(0f, 1f), Vector2.one);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.sizeDelta = new Vector2(0f, OctopusSampleBranding.AppBarUnits);
        var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = Padding(OctopusSampleBranding.SpaceLg, OctopusSampleBranding.SpaceSm);
        layout.spacing = OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceSm);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        if (onBack != null)
        {
            var back = Panel(backName, bar, OctopusSampleBranding.Clear);
            back.GetComponent<Image>().raycastTarget = true;
            var button = back.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = back.GetComponent<Image>();
            button.onClick.AddListener(onBack);
            var size = TouchLayout(back);
            size.preferredWidth = size.minWidth;
            AppBarItem(back);
            SampleUiIcons.Back(back, OctopusSampleBranding.Palette.OnChrome);
        }
        var heading = Label(titleName, bar, title, TextTitleXl,
            OctopusSampleBranding.Palette.OnChrome, TextAnchor.MiddleLeft);
        heading.GetComponent<SampleUiScaledText>().Configure(TextTitleXl,
            OctopusSampleBranding.Dp(28f), singleLine: true);
        heading.textWrappingMode = TextWrappingModes.NoWrap;
        heading.overflowMode = TextOverflowModes.Ellipsis;
        var headingSize = heading.gameObject.AddComponent<LayoutElement>();
        headingSize.minWidth = headingSize.preferredWidth = 0f;
        headingSize.flexibleWidth = 1f;
        AppBarItem(heading.rectTransform);
        return bar;
    }

    /// <summary>
    /// One height for title, identity and actions, including injected Debug controls.
    /// Zeroes the vertical padding of the item's own layout group.
    /// </summary>
    public static void AppBarItem(RectTransform item)
    {
        var size = item.GetComponent<LayoutElement>();
        if (size == null) size = item.gameObject.AddComponent<LayoutElement>();
        // Override SampleUiAdaptivePanel (priority 1), whose preferred height grows with text
        // scale. Otherwise its ILayoutElement competes with this fixed-height chrome slot.
        size.layoutPriority = 2;
        size.minHeight = size.preferredHeight = OctopusSampleBranding.MinTouchUnits;
        size.flexibleHeight = 0f;
        var group = item.GetComponent<HorizontalOrVerticalLayoutGroup>();
        if (group != null)
            group.padding = new RectOffset(group.padding.left, group.padding.right, 0, 0);
    }

    /// <summary>Shared page gutters on all four sides, in canvas units.</summary>
    public static RectOffset ContentPadding()
    {
        return Padding(OctopusSampleBranding.SpaceLg, OctopusSampleBranding.SpaceLg);
    }

    public static RectTransform ChromeButton(string name, Transform parent, string text,
                                             UnityEngine.Events.UnityAction onClick)
    {
        return BuildButton(name, parent, text, ChromeButtonFill, ChromeButtonInk, ChromeButtonFill,
            onClick, OctopusSampleBranding.ButtonRadius, true);
    }

    private static RectTransform BuildButton(string name, Transform parent, string text, Color fill,
        Color ink, Color border, UnityEngine.Events.UnityAction onClick, float radiusDp, bool chrome)
    {
        var rect = Panel(name, parent, fill, radiusDp, border, 0f);
        rect.GetComponent<Image>().raycastTarget = true;
        var stroke = Stroke(rect, radiusDp, border, OctopusSampleBranding.Stroke);
        var button = rect.gameObject.AddComponent<SampleUiButton>();
        button.targetGraphic = rect.GetComponent<Image>();
        if (onClick != null) button.onClick.AddListener(onClick);
        TouchLayout(rect);
        var stack = VerticalStack(rect, 0f, Padding(OctopusSampleBranding.SpaceLg,
            OctopusSampleBranding.SpaceMd), false);
        stack.childAlignment = TextAnchor.MiddleCenter;
        var label = Label("Label", rect, text, TextBody, ink, TextAnchor.MiddleCenter);
        label.fontStyle = FontStyles.Bold;
        // A chrome pill carries one short word and is sized on it: wrapping is how a 64dp "Back"
        // pill rendered "Bac / k" once the font became Inter, wider than the legacy face. Content
        // buttons keep the normal wrapping — a full-width action may legitimately take two lines.
        if (chrome) label.textWrappingMode = TextWrappingModes.NoWrap;
        button.Configure(rect.GetComponent<Image>(), stroke, label, fill, ink, border, radiusDp, chrome);
        return rect;
    }

    /// <summary>
    /// Measures a button's label and horizontal padding without changing its rect, with a floor
    /// of <see cref="OctopusSampleBranding.MinTouchDp"/> dp unless overridden.
    /// </summary>
    /// <param name="rect">A button built by <see cref="Button(string, Transform, string, UnityEngine.Events.UnityAction)"/>
    /// or <see cref="ChromeButton"/>.</param>
    /// <param name="minDp">The floor, in dp; defaults to the touch target.</param>
    public static float PreferredButtonWidth(RectTransform rect,
                                             float minDp = OctopusSampleBranding.MinTouchDp)
    {
        float floor = OctopusSampleBranding.Dp(minDp);
        if (rect == null) return floor;
        var label = rect.Find("Label") as RectTransform;
        var text = label != null ? label.GetComponent<TMP_Text>() : null;
        if (text == null) return floor;
        var group = rect.GetComponent<HorizontalOrVerticalLayoutGroup>();
        float padding = group != null && group.padding != null ? group.padding.horizontal : 0f;
        return Mathf.Max(floor, text.preferredWidth + padding);
    }

    private static LayoutElement TouchLayout(RectTransform rect, float heightDp = OctopusSampleBranding.MinTouchDp)
    {
        var layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = OctopusSampleBranding.MinTouchUnits;
        layout.minHeight = OctopusSampleBranding.Dp(heightDp);
        rect.sizeDelta = new Vector2(layout.minWidth, layout.minHeight);
        rect.gameObject.AddComponent<SampleUiTouchTarget>();
        return layout;
    }

    public static RectOffset OverlayPadding()
    {
        var padding = Padding(OctopusSampleBranding.SpaceLg, OctopusSampleBranding.SpaceLg);
        padding.top = OctopusSampleBranding.OverlayContentTopUnits;
        return padding;
    }

    public static RectOffset Padding(float horizontalDp, float verticalDp)
    {
        int h = Mathf.RoundToInt(OctopusSampleBranding.Dp(horizontalDp));
        int v = Mathf.RoundToInt(OctopusSampleBranding.Dp(verticalDp));
        return new RectOffset(h, h, v, v);
    }

    /// <summary>A full-width action row; text determines height above the 48/64dp floor.</summary>
    public static RectTransform ListRow(string name, Transform parent, string title, string subtitle,
                                        UnityEngine.Events.UnityAction onClick)
    {
        var p = OctopusSampleBranding.Palette;
        var rect = Panel(name, parent, p.Surface, OctopusSampleBranding.CardRadius, p.Border, 0f);
        var stroke = Stroke(rect, OctopusSampleBranding.CardRadius, p.Border, OctopusSampleBranding.Stroke);
        TouchLayout(rect, string.IsNullOrEmpty(subtitle) ? OctopusSampleBranding.MinTouchDp
            : OctopusSampleBranding.RowWithSubtitleHeight);
        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = ContentPadding();
        layout.spacing = OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceMd);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.MiddleLeft;
        if (name.StartsWith("settings-", System.StringComparison.Ordinal) ||
            name.StartsWith("about-", System.StringComparison.Ordinal))
            SampleUiIcons.Row(rect, SampleUiIcons.ForRow(name));
        var copy = Panel("Copy", rect, OctopusSampleBranding.Clear);
        copy.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        VerticalStack(copy, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceXs), new RectOffset(), false);
        var label = Label("Label", copy, title, TextBody, p.Title, TextAnchor.MiddleLeft);
        label.fontStyle = FontStyles.Bold;
        TMP_Text detail = null;
        if (!string.IsNullOrEmpty(subtitle)) detail = FlexibleLabel(copy, subtitle, TextCaption, p.Muted);
        var chevron = Label("Chevron", rect, "›", TextTitle, p.Muted, TextAnchor.MiddleCenter);
        var chevronSize = chevron.gameObject.AddComponent<LayoutElement>();
        chevronSize.minWidth = chevronSize.preferredWidth = OctopusSampleBranding.Dp(24f);
        var button = rect.gameObject.AddComponent<SampleUiButton>();
        button.targetGraphic = rect.GetComponent<Image>();
        if (onClick != null) button.onClick.AddListener(onClick);
        button.Configure(rect.GetComponent<Image>(), stroke, label, p.Surface, p.Title, p.Border,
            OctopusSampleBranding.CardRadius, false, detail);
        return rect;
    }

    /// <summary>
    /// A text field, single-line by default, pre-filled with <paramref name="value"/>.
    ///
    /// Editable by default. Scenario screens set readOnly until Customize is opened; presets
    /// still fill every field in one tap for QA (SDK_STANDARDS §5.2).
    /// </summary>
    public static TMP_InputField Field(string name, Transform parent, string value, bool multiline = false,
                                       string placeholder = "")
    {
        var rect = Panel(name, parent, FieldBackground, OctopusSampleBranding.FieldRadius,
            OctopusSampleBranding.Palette.ControlBorder, 0f);
        rect.GetComponent<Image>().raycastTarget = true;
        var border = Stroke(rect, OctopusSampleBranding.FieldRadius,
            OctopusSampleBranding.Palette.ControlBorder, OctopusSampleBranding.Stroke);
        var layout = TouchLayout(rect);
        layout.minHeight = OctopusSampleBranding.MinTouchUnits * (multiline ? 3f : 1f);
        layout.preferredHeight = layout.minHeight;

        // Assign TMP's viewport and text before OnEnable registers geometry/keyboard callbacks.
        rect.gameObject.SetActive(false);
        var viewport = Panel("Text Area", rect, OctopusSampleBranding.Clear);
        Stretch(viewport, Vector2.zero, Vector2.one);
        viewport.offsetMin = Vector2.one * OctopusSampleBranding.Dp(12f);
        viewport.offsetMax = -viewport.offsetMin;
        viewport.gameObject.AddComponent<RectMask2D>();
        var text = Label("Text", viewport, value, TextBody, TitleColor,
                         multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
        text.richText = false;
        text.raycastTarget = true;
        text.overflowMode = TextOverflowModes.Masking;
        Stretch(text.rectTransform, Vector2.zero, Vector2.one);

        var input = rect.gameObject.AddComponent<SampleUiInputField>();
        input.targetGraphic = rect.GetComponent<Image>();
        input.textViewport = viewport;
        input.textComponent = text;
        input.richText = false;
        input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
        input.text = value;
        input.Configure(border);
        if (!string.IsNullOrEmpty(placeholder))
        {
            var hint = Label("Placeholder", input.textViewport, placeholder, TextBody,
                OctopusSampleBranding.Palette.Placeholder, TextAnchor.MiddleLeft);
            hint.richText = false;
            Stretch(hint.rectTransform, Vector2.zero, Vector2.one);
            hint.rectTransform.offsetMin = input.textComponent.rectTransform.offsetMin;
            hint.rectTransform.offsetMax = input.textComponent.rectTransform.offsetMax;
            // InputField's placeholder setter does not refresh its enabled state. With a restored
            // query, the first font/layout callback would otherwise disable this Graphic from inside
            // CanvasUpdateRegistry's rebuild loop. Set the state before the first canvas update.
            hint.enabled = string.IsNullOrEmpty(input.text);
            input.placeholder = hint;
        }
        rect.gameObject.SetActive(true);
        if (input.placeholder != null) input.ForceLabelUpdate();
        return input;
    }

    /// <summary>A persistent label and field in a self-contained layout, ready for an error message.</summary>
    public static TMP_InputField LabeledField(string name, Transform parent, string label, string value,
                                          bool multiline = false, string placeholder = "")
    {
        var group = Panel("FieldGroup", parent, OctopusSampleBranding.Clear);
        VerticalStack(group, OctopusSampleBranding.Dp(6f), new RectOffset(), false);
        FlexibleLabel(group, label, TextCaption, TitleColor);
        return Field(name, group, value, multiline, placeholder);
    }

    public static void SetFieldError(TMP_InputField input, string message)
    {
        var styled = input as SampleUiInputField;
        if (styled != null) styled.SetError(message);
    }

    /// <summary>
    /// A card: the <see cref="RowBackground"/> ground every block on a screen sits on, already a
    /// vertical stack with the shared padding. Sized by the caller's layout group, so a card inside
    /// a scroll's content stack needs nothing else.
    ///
    /// In particular it gets no <see cref="ContentSizeFitter"/> of its own, and that is the whole
    /// point of the last argument below: every caller puts a card inside a height-controlling
    /// stack, which reads the card's preferred height off the card's own layout group. A fitter on
    /// top of that is the combination Unity warns about — the same rule `OctopusScenariosListView`
    /// states on its rows ("the content stack already controls its height").
    /// </summary>
    public static RectTransform Card(string name, Transform parent)
    {
        var card = Panel(name, parent, RowBackground, OctopusSampleBranding.CardRadius,
            OctopusSampleBranding.Palette.Border);
        VerticalStack(card, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceSm),
            Padding(OctopusSampleBranding.SpaceLg, OctopusSampleBranding.SpaceLg), false);
        return card;
    }

    /// <summary>A live-state marker and label, never a porting-status badge.</summary>
    public static RectTransform StatusDot(string name, Transform parent, Color color, string label)
    {
        var row = Panel(name, parent, OctopusSampleBranding.Clear);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceXs);
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        row.gameObject.AddComponent<LayoutElement>().minHeight = OctopusSampleBranding.Dp(16f);

        var marker = Panel("Marker", row, color, 4f, color, 0f);
        var markerLayout = marker.gameObject.AddComponent<LayoutElement>();
        markerLayout.preferredWidth = OctopusSampleBranding.Dp(8f);
        markerLayout.preferredHeight = OctopusSampleBranding.Dp(8f);

        var text = Label("Label", row, label, TextCaption, color, TextAnchor.MiddleLeft);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.fontStyle = FontStyles.Bold;
        text.gameObject.AddComponent<LayoutElement>();
        return row;
    }

    /// <summary>A 52×32dp track inside a 52×48dp hit region. The caller owns the value and rebuilds on flip.</summary>
    public static RectTransform Switch(string name, Transform parent, bool on, bool interactable,
                                       UnityEngine.Events.UnityAction onClick)
    {
        var p = OctopusSampleBranding.Palette;
        var target = Panel(name, parent, OctopusSampleBranding.Clear);
        target.GetComponent<Image>().raycastTarget = true;
        var size = TouchLayout(target);
        size.minWidth = size.preferredWidth = OctopusSampleBranding.Dp(OctopusSampleBranding.SwitchWidth);
        size.preferredHeight = OctopusSampleBranding.MinTouchUnits;
        target.sizeDelta = new Vector2(size.minWidth, size.minHeight);
        Color fill = on ? p.Accent : p.SurfaceHigh;
        Color ink = on ? p.OnAccent : p.Muted;
        Color edge = on ? p.Accent : p.ControlBorder;
        var track = Panel("Track", target, fill, OctopusSampleBranding.SwitchHeight / 2f, edge, 0f);
        track.anchorMin = track.anchorMax = new Vector2(0.5f, 0.5f);
        track.sizeDelta = new Vector2(OctopusSampleBranding.Dp(OctopusSampleBranding.SwitchWidth),
            OctopusSampleBranding.Dp(OctopusSampleBranding.SwitchHeight));
        track.GetComponent<Image>().raycastTarget = false;
        var stroke = Stroke(track, OctopusSampleBranding.SwitchHeight / 2f, edge, OctopusSampleBranding.FocusStroke);
        // Keep the existing Knob child directly under the QA-named root.
        var knob = Panel("Knob", target, ink, OctopusSampleBranding.SwitchKnob / 2f, ink, 0f);
        knob.anchorMin = knob.anchorMax = new Vector2(0.5f, 0.5f);
        knob.sizeDelta = Vector2.one * OctopusSampleBranding.Dp(OctopusSampleBranding.SwitchKnob);
        knob.anchoredPosition = new Vector2(OctopusSampleBranding.Dp(on ? 10f : -10f), 0f);
        knob.GetComponent<Image>().raycastTarget = false;
        var button = target.gameObject.AddComponent<SampleUiButton>();
        button.targetGraphic = target.GetComponent<Image>();
        if (onClick != null) button.onClick.AddListener(onClick);
        button.Configure(track.GetComponent<Image>(), stroke, knob.GetComponent<Image>(),
            fill, ink, edge, OctopusSampleBranding.SwitchHeight / 2f,
            strokeDp: OctopusSampleBranding.FocusStroke);
        button.interactable = interactable;
        return target;
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
        var viewport = Panel("Viewport", parent, OctopusSampleBranding.Clear);
        Stretch(viewport, Vector2.zero, Vector2.one);
        viewport.offsetMin = new Vector2(insets.left, insets.bottom);
        viewport.offsetMax = new Vector2(-insets.right, -insets.top);
        viewport.gameObject.AddComponent<RectMask2D>();
        // Panel() derives raycastTarget from alpha, which is right everywhere but here: uGUI walks
        // UP from the graphic it hit, so a drag starting in the gap between two rows would hit the
        // viewport's parent — not a descendant of the ScrollRect — and scroll nothing.
        viewport.GetComponent<Image>().raycastTarget = true;

        var content = Panel("Content", viewport, OctopusSampleBranding.Clear);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        content.anchoredPosition = Vector2.zero;
        VerticalStack(content, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceLg), new RectOffset(), true);

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 40f;
        return content;
    }

    /// <summary>A live safe-area and keyboard inset; controls retain focus as it changes.</summary>
    public static RectTransform SafeArea(string name, Transform parent)
    {
        var safe = Panel(name, parent, OctopusSampleBranding.Clear);
        safe.gameObject.AddComponent<SampleUiSafeArea>().Refresh();
        return safe;
    }

    private static TextAlignmentOptions Alignment(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            default: return TextAlignmentOptions.BottomRight;
        }
    }

    public static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
