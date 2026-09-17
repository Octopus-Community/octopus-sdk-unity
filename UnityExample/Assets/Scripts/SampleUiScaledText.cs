using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Keeps the authored size and line height while the system preference changes.</summary>
[ExecuteAlways]
[RequireComponent(typeof(TextMeshProUGUI))]
public class SampleUiScaledText : MonoBehaviour
{
    private TMP_Text _text;
    private int _baseSize;
    private float _lineHeight;
    private float _cap = float.PositiveInfinity;
    private float _applied = -1f;
    private bool _singleLine;

    public int BaseSize { get { return _baseSize; } }
    public float BaseLineHeight { get { return _lineHeight; } }

    public void Configure(int size, float lineHeight, bool tabLabel = false, bool singleLine = false)
    {
        _singleLine = singleLine;
        _baseSize = Mathf.Max(size, OctopusSampleBranding.MinTextUnits);
        _lineHeight = lineHeight;
        _cap = tabLabel ? OctopusSampleBranding.TabTextScaleCap : float.PositiveInfinity;
        _applied = -1f;
        RefreshScale();
    }

    public void RefreshScale()
    {
        if (_baseSize == 0) return;
        float scale = Mathf.Min(OctopusSampleTextScale.Current, _cap);
        if (Mathf.Approximately(scale, _applied)) return;
        _applied = scale;
        if (_text == null) _text = GetComponent<TMP_Text>();
        _text.fontSize = Mathf.RoundToInt(_baseSize * scale);
        _text.enableAutoSizing = _singleLine;
        if (_singleLine)
        {
            // Fixed-height chrome must keep a visible title at larger accessibility text sizes.
            // Keep app-bar titles at least title-sized, then ellipsize. The fixed floor still
            // lets enlarged text fit the bar without dropping to the caption size.
            _text.fontSizeMin = Mathf.Min(_baseSize, SampleUi.TextTitle);
            _text.fontSizeMax = _text.fontSize;
        }
        var font = _text.font;
        if (font != null && font.faceInfo.pointSize > 0)
        {
            // TMP lineSpacing is a percentage of em, unlike legacy Text's multiplier.
            var face = font.faceInfo;
            float naturalEm = face.lineHeight * face.scale / face.pointSize;
            _text.lineSpacing = (_lineHeight / _baseSize - naturalEm) * 100f;
        }
    }

    private void Update() { RefreshScale(); }
}
