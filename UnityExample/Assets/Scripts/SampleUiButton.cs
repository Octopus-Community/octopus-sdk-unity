using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum SampleUiButtonVariant
{
    Primary,
    Secondary,
    Tertiary,
    Destructive
}

/// <summary>Explicit opaque state colours; keeps the standard Button click/navigation behaviour.</summary>
public class SampleUiButton : Button
{
    private Image _fill;
    private Image _stroke;
    private Graphic _label;
    private TMP_Text _detail;
    private Color _normalFill;
    private Color _normalInk;
    private Color _normalDetailInk;
    private Color _pressedFill;
    private Color _normalBorder;
    private Color _disabledFill;
    private Color _disabledInk;
    private Color _disabledBorder;
    private Color _focus;
    private float _radius;
    private float _strokeDp;
    private bool _pointerSelection;
    private bool _chrome;
    private bool _hasPageStyle;
    private bool _pageChrome;
    private Color _pageFill;
    private Color _pageInk;
    private Color _pageBorder;

    /// <summary>
    /// The page variant the button was built with by <see cref="SampleUi.Button"/>, or null for a
    /// button built with explicit colours. Leaving the app bar re-derives the page colours of this
    /// variant from the live palette, so a theme change made while on the bar is honoured.
    /// </summary>
    public SampleUiButtonVariant? PageVariant { get; set; }

    /// <summary>Whether the button currently wears the app-bar (chrome) style.</summary>
    public bool IsChromeStyled { get { return _chrome; } }

    public override void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) _pointerSelection = true;
        base.OnPointerDown(eventData);
    }

    public override void OnSelect(BaseEventData eventData)
    {
        _pointerSelection = eventData is PointerEventData;
        base.OnSelect(eventData);
    }

    public void Configure(Image fill, Image stroke, Graphic label, Color normalFill, Color normalInk,
                          Color normalBorder, float radius, bool chrome = false, TMP_Text detail = null,
                          float strokeDp = OctopusSampleBranding.Stroke)
    {
        _fill = fill;
        _stroke = stroke;
        _label = label;
        _detail = detail;
        _normalDetailInk = detail != null ? detail.color : normalInk;
        _normalFill = normalFill;
        _normalInk = normalInk;
        _normalBorder = normalBorder;
        _radius = radius;
        _strokeDp = strokeDp;
        _chrome = chrome;
        var palette = OctopusSampleBranding.Palette;
        // Signal's pressed surface preserves contrast for both card and chrome labels.
        _pressedFill = OctopusSampleBranding.Theme == OctopusSampleTheme.Dark &&
            (normalFill == palette.Surface || chrome)
            ? OctopusSampleBranding.DarkCardPressed
            : OctopusSampleBranding.Tint(normalInk,
            normalFill.a == 0f ? palette.SurfaceHigh : normalFill, OctopusSampleBranding.PressedTint);
        _disabledFill = chrome ? palette.Chrome : palette.DisabledSurface;
        _disabledInk = chrome ? palette.OnChrome : palette.DisabledInk;
        _disabledBorder = chrome ? palette.ChromeControl : palette.Border;
        _focus = chrome ? palette.OnChrome : palette.Focus;
        transition = Transition.None;
        DoStateTransition(currentSelectionState, true);
    }

    /// <summary>
    /// Moves a page button onto an app bar (<paramref name="chrome"/> true) or back. On the bar it
    /// wears <see cref="SampleUi.ChromeButtonFill"/> from the live palette — the page's accent
    /// fill equals the navy chrome in light and would vanish there. Leaving the bar restores the
    /// page style: a <see cref="PageVariant"/> button takes its variant colours from the live
    /// palette, any other button the colours it had before.
    /// </summary>
    public void SetChromeStyle(bool chrome)
    {
        if (_fill == null) return;
        if (chrome)
        {
            if (!_hasPageStyle)
            {
                _hasPageStyle = true;
                _pageChrome = _chrome;
                _pageFill = _normalFill;
                _pageInk = _normalInk;
                _pageBorder = _normalBorder;
            }
            Configure(_fill, _stroke, _label, SampleUi.ChromeButtonFill, SampleUi.ChromeButtonInk,
                SampleUi.ChromeButtonFill, _radius, true, _detail, _strokeDp);
            return;
        }
        if (!_hasPageStyle) return;
        _hasPageStyle = false;
        if (PageVariant.HasValue && !_pageChrome)
        {
            // Re-derive from the live palette: colours captured before a theme switch are stale.
            SampleUi.VariantColors(PageVariant.Value, out _pageFill, out _pageInk, out _pageBorder);
        }
        Configure(_fill, _stroke, _label, _pageFill, _pageInk, _pageBorder, _radius, _pageChrome,
            _detail, _strokeDp);
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        // Unity calls this during AddComponent, before the builder has assigned the graphics.
        if (_fill == null) return;
        bool disabled = state == SelectionState.Disabled;
        bool pressed = state == SelectionState.Pressed ||
            (state == SelectionState.Highlighted && OctopusSampleBranding.Theme == OctopusSampleTheme.Dark &&
             _normalFill == OctopusSampleBranding.DarkSurfaceLow);
        // A scroll cancels Pressed through uGUI's pointer-up, but leaves selection/hover behind.
        // Only navigation selection gets an outline; passing a finger over rows must not add one.
        bool focused = state == SelectionState.Selected && !_pointerSelection;
        _fill.color = disabled ? _disabledFill : pressed
            ? _pressedFill
            : _normalFill;
        if (_label != null) _label.color = disabled ? _disabledInk : _normalInk;
        if (_detail != null) _detail.color = disabled ? _disabledInk : _normalDetailInk;
        if (_stroke != null)
        {
            _stroke.color = disabled ? _disabledBorder : focused ? _focus : _normalBorder;
            _stroke.sprite = SampleUiShapes.Rounded(_radius,
                focused && !disabled ? Mathf.Max(_strokeDp, OctopusSampleBranding.FocusStroke) : _strokeDp);
        }
    }
}
