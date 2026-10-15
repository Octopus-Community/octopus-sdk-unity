using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Compose's <c>maxLines</c> + <c>TextOverflow.Ellipsis</c> for a layout-driven TMP label. TMP
/// reports the height of its whole wrapped text to the layout, so a long subtitle would grow its
/// row however far it runs; this caps the reported height at <see cref="MaxLines"/> lines, at the
/// live text scale, and lets TMP put the ellipsis on the last visible line.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class SampleUiLineClamp : UIBehaviour, ILayoutElement
{
    public int MaxLines = 1;

    private TMP_Text _text;
    private SampleUiScaledText _scaled;

    /// <summary>Clamps <paramref name="label"/> to <paramref name="lines"/> lines, ellipsized.</summary>
    public static SampleUiLineClamp Apply(TMP_Text label, int lines)
    {
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.maxVisibleLines = Mathf.Max(1, lines);
        var clamp = label.GetComponent<SampleUiLineClamp>();
        if (clamp == null) clamp = label.gameObject.AddComponent<SampleUiLineClamp>();
        clamp.MaxLines = Mathf.Max(1, lines);
        return clamp;
    }

    /// <summary>The height <see cref="MaxLines"/> lines take at the label's current size.</summary>
    public float CapHeight
    {
        get
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            if (_scaled == null) _scaled = GetComponent<SampleUiScaledText>();
            float line = _scaled != null && _scaled.BaseSize > 0
                ? _scaled.BaseLineHeight * _text.fontSize / _scaled.BaseSize
                : _text.fontSize * 1.2f;
            return MaxLines * line;
        }
    }

    public float preferredHeight
    {
        get
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            return Mathf.Min(_text.preferredHeight, CapHeight);
        }
    }

    public float minWidth { get { return -1f; } }
    public float preferredWidth { get { return -1f; } }
    public float flexibleWidth { get { return -1f; } }
    public float minHeight { get { return -1f; } }
    public float flexibleHeight { get { return -1f; } }
    // Unity 6.6 added these to ILayoutElement; -1 = no constraint, as in SampleUiAdaptivePanel.
    public float maxWidth { get { return -1f; } }
    public float maxHeight { get { return -1f; } }
    // Above LayoutElement (1) and TMP (0), so the capped height is the one the layout reads.
    public int layoutPriority { get { return 2; } }

    public void CalculateLayoutInputHorizontal() { }
    public void CalculateLayoutInputVertical() { }

    protected override void OnEnable() { base.OnEnable(); SetDirty(); }
    protected override void OnDisable() { SetDirty(); base.OnDisable(); }

    private void SetDirty()
    {
        if (!IsActive()) return;
        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
    }
}
