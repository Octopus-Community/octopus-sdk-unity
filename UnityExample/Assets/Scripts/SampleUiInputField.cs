using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>InputField with focus, error and disabled roles, including its text and placeholder.</summary>
public class SampleUiInputField : TMP_InputField
{
    private Image _border;
    private OctopusSamplePalette _palette;
    private string _error = string.Empty;
    private TMP_Text _errorLabel;
#if UNITY_EDITOR
    private static readonly System.Collections.Generic.List<GameObject> PendingTeardown =
        new System.Collections.Generic.List<GameObject>();
#endif
    private bool _wasFocused;
    private string _lastInput;
    private int _lastCaret;
    private Vector2 _lastViewportSize, _lastFieldSize;
    private Rect _lastKeyboard;
    private ScrollRect _parentScroll;
    private bool _scrollResolved;
    private uint _scrollRevision;

    protected override void OnTransformParentChanged()
    {
        base.OnTransformParentChanged();
        _scrollResolved = false;
    }

    private ScrollRect ParentScroll()
    {
        if (!_scrollResolved || !_wasFocused || _scrollRevision != SampleUiAdaptivePanel.LayoutRevision)
        {
            _parentScroll = GetComponentInParent<ScrollRect>();
            _scrollResolved = true;
            _scrollRevision = SampleUiAdaptivePanel.LayoutRevision;
        }
        return _parentScroll;
    }

    public string Error { get { return _error; } }

    public override float preferredHeight
    {
        get
        {
            if (textComponent == null) return base.preferredHeight;
            float padding = OctopusSampleBranding.Dp(24f);
            float line = OctopusSampleBranding.Dp(20f) * OctopusSampleTextScale.Current;
            float floor = OctopusSampleBranding.MinTouchUnits * (multiLine ? 3f : 1f);
            // Measure the full edit buffer against the padded width. TMP moves the text for
            // caret visibility; its displayed geometry must not become the layout's source.
            float width = Mathf.Max(1f, textViewport != null ? textViewport.rect.width
                : ((RectTransform)transform).rect.width - padding);
            float bufferHeight = textComponent.GetPreferredValues(text, width, Mathf.Infinity).y;
            return Mathf.Max(floor, Mathf.Max(line, bufferHeight) + padding);
        }
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (!isFocused) { _wasFocused = false; return; }
        var scroll = ParentScroll();
        Vector2 viewportSize = scroll != null && scroll.viewport != null ? scroll.viewport.rect.size : Vector2.zero;
        Vector2 fieldSize = ((RectTransform)transform).rect.size;
        Rect keyboard = SampleUiSafeArea.KeyboardArea();
        // Reveal after selection, typing or an inset/layout change. Do not fight a deliberate drag
        // towards Run/Open while the field still has keyboard focus.
        if (!_wasFocused || _lastInput != text || _lastCaret != caretPosition ||
            _lastViewportSize != viewportSize || _lastFieldSize != fieldSize || _lastKeyboard != keyboard)
            RevealInScrollView();
        _wasFocused = true;
        _lastInput = text;
        _lastCaret = caretPosition;
        _lastViewportSize = viewportSize;
        _lastFieldSize = fieldSize;
        _lastKeyboard = keyboard;
    }

    // Scroll the selected field into the resized viewport without changing selection or the buffer.
    public void RevealInScrollView()
    {
        var scroll = ParentScroll();
        while (scroll != null)
        {
            RevealIn(scroll);
            scroll = scroll.transform.parent != null ? scroll.transform.parent.GetComponentInParent<ScrollRect>() : null;
        }
    }

    private void RevealIn(ScrollRect scroll)
    {
        if (scroll.content == null || scroll.viewport == null) return;
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, transform);
        var viewport = scroll.viewport.rect;
        float bottom = bounds.min.y;
        float top = bounds.max.y;
        if (bounds.size.y > viewport.height && textComponent != null)
        {
            // A long multiline buffer cannot fit as a whole: reveal the caret's line instead.
            float prefixHeight = textComponent.GetPreferredValues(
                text.Substring(0, Mathf.Clamp(stringPosition, 0, text.Length)) + "M",
                textComponent.rectTransform.rect.width, Mathf.Infinity).y;
            bottom = top - OctopusSampleBranding.Dp(12f) - prefixHeight;
            top = bottom + OctopusSampleBranding.Dp(20f) * OctopusSampleTextScale.Current;
        }
        float movement = bottom < viewport.yMin ? viewport.yMin - bottom : 0f;
        if (top > viewport.yMax) movement = viewport.yMax - top;
        if (Mathf.Abs(movement) < 0.1f) return;
        scroll.StopMovement();
        scroll.content.anchoredPosition += new Vector2(0f, movement);
    }

    public void Configure(Image border)
    {
        _border = border;
        _palette = OctopusSampleBranding.Palette;
        transition = Transition.None;
        customCaretColor = true;
        caretColor = _palette.Title;
        selectionColor = _palette.AccentIndicator;
        RefreshVisuals();
    }

    public void SetError(string message)
    {
        _error = message ?? string.Empty;
        if (_errorLabel == null && _error.Length > 0)
        {
            // A sibling participates in the caller's vertical layout; it cannot cover the next field.
            _errorLabel = SampleUi.FlexibleLabel(transform.parent, _error,
                SampleUi.TextCaption, _palette.Negative);
            _errorLabel.richText = false;
            _errorLabel.transform.SetSiblingIndex(transform.GetSiblingIndex() + 1);
        }
        if (_errorLabel != null)
        {
            _errorLabel.text = _error;
            _errorLabel.gameObject.SetActive(_error.Length > 0);
        }
        RefreshVisuals();
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        // InputField also tracks its focus transition here, even with Transition.None.
        base.DoStateTransition(state, instant);
        RefreshVisuals(isFocused || state == SelectionState.Selected || state == SelectionState.Pressed);
    }

    public void RefreshVisuals()
    {
        RefreshVisuals(isFocused || currentSelectionState == SelectionState.Selected);
    }

    private void RefreshVisuals(bool focused)
    {
        if (_border == null || _palette == null || textComponent == null) return;
        bool disabled = !IsInteractable();
        var fill = targetGraphic as Image;
        if (fill != null) fill.color = disabled ? _palette.DisabledSurface : _palette.SurfaceHigh;
        textComponent.color = disabled ? _palette.DisabledInk : _palette.Title;
        if (placeholder != null) placeholder.color = _palette.Placeholder;
        _border.color = disabled ? _palette.Border : _error.Length > 0 ? _palette.Negative
            : focused ? _palette.Focus : _palette.ControlBorder;
        _border.sprite = SampleUiShapes.Rounded(OctopusSampleBranding.FieldRadius,
            focused && !disabled ? OctopusSampleBranding.FocusStroke : OctopusSampleBranding.Stroke);
    }

    protected override void OnDestroy()
    {
        if (_errorLabel != null)
        {
            if (Application.isPlaying) Destroy(_errorLabel.gameObject);
#if UNITY_EDITOR
            else
            {
                // DestroyImmediate is refused from inside OnDestroy, so the orphan waits for the
                // next editor tick. A batch-mode test runner never reaches that tick, hence the
                // explicit flush below.
                PendingTeardown.Add(_errorLabel.gameObject);
                UnityEditor.EditorApplication.delayCall += FlushEditorTeardown;
            }
#endif
        }
        base.OnDestroy();
    }

#if UNITY_EDITOR
    /// <summary>
    /// Destroys the error labels orphaned by an edit-mode teardown. The editor calls this on its
    /// next tick; a test that cannot wait for a tick calls it itself.
    /// </summary>
    public static void FlushEditorTeardown()
    {
        for (int i = 0; i < PendingTeardown.Count; i++)
        {
            if (PendingTeardown[i] != null) DestroyImmediate(PendingTeardown[i]);
        }
        PendingTeardown.Clear();
    }
#endif
}
