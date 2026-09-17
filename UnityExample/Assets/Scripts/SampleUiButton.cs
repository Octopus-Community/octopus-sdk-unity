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
        var palette = OctopusSampleBranding.Palette;
        _pressedFill = OctopusSampleBranding.Tint(normalInk,
            normalFill.a == 0f ? palette.SurfaceHigh : normalFill, OctopusSampleBranding.PressedTint);
        _disabledFill = chrome ? palette.Chrome : palette.DisabledSurface;
        _disabledInk = chrome ? palette.OnChrome : palette.DisabledInk;
        _disabledBorder = chrome ? palette.ChromeControl : palette.Border;
        _focus = chrome ? palette.OnChrome : palette.Focus;
        transition = Transition.None;
        DoStateTransition(currentSelectionState, true);
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        // Unity calls this during AddComponent, before the builder has assigned the graphics.
        if (_fill == null) return;
        bool disabled = state == SelectionState.Disabled;
        bool pressed = state == SelectionState.Pressed;
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
