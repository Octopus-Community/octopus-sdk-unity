using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lets the app bar of a detail route host the internal Debug entry, the way the shell's own app bar
/// hosts it.
///
/// The entry used to live on a canvas of its own at 1150, above everything; hosting it in the shell
/// app bar (900) put it under every route opened at <see cref="SampleUi.DetailSortingOrder"/> —
/// scenario screens, About, Appearance, Dev tools. The shared design keeps the same control in the
/// app bar of those routes too, beside the back arrow, so the header of each one claims it on open
/// and hands it back when it closes.
///
/// Nothing here references the private Debug assembly: the entry arrives through the
/// <see cref="SampleUi.DebugEntry"/> seam, and a build without the Debug folder simply has none.
/// </summary>
[ExecuteAlways]
public class SampleUiDebugEntryHost : MonoBehaviour
{
    private static readonly List<SampleUiDebugEntryHost> Hosts = new List<SampleUiDebugEntryHost>();

    private RectTransform _entry;
    private Transform _parent;
    private Vector2 _anchorMin, _anchorMax, _pivot, _size, _position;
    private RectOffset _entryPadding;
    private float _minWidth, _preferredWidth, _flexibleWidth;
    private float _minHeight, _preferredHeight, _flexibleHeight;
    private int _layoutPriority;
    private int _baseSize;
    private float _trailingInset;
    private float _baseLineHeight;

    /// <summary>
    /// Claims the entry for <paramref name="header"/>. Returns null when no entry is installed,
    /// which is the normal case for a public build.
    /// </summary>
    /// <param name="header">The route's app bar.</param>
    /// <param name="trailingInsetUnits">How far from the header's trailing edge to sit, in canvas
    /// units, for a header posed by hand; 0 takes the standard gutter. Shared app bars use their layout group instead.</param>
    public static SampleUiDebugEntryHost Attach(RectTransform header, float trailingInsetUnits = 0f)
    {
        if (header == null || SampleUi.DebugEntry == null) return null;
        var host = header.gameObject.AddComponent<SampleUiDebugEntryHost>();
        host._trailingInset = trailingInsetUnits > 0f
            ? trailingInsetUnits : OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceLg);
        host.Adopt();
        return host;
    }

    private void Adopt()
    {
        // Whoever holds the entry — the shell, or a route opened before this one — gives it back
        // to its own owner first, so the state captured below is the entry's resting state.
        SampleUi.ClaimDebugEntry(this);
        var entry = SampleUi.DebugEntry;
        if (entry == null) return;
        SampleUi.DebugEntryClaimed += OnClaimed;
        Hosts.Add(this);
        _entry = entry;
        _parent = entry.parent;
        _anchorMin = entry.anchorMin;
        _anchorMax = entry.anchorMax;
        _pivot = entry.pivot;
        _size = entry.sizeDelta;
        _position = entry.anchoredPosition;
        var element = entry.GetComponent<LayoutElement>();
        if (element == null) element = entry.gameObject.AddComponent<LayoutElement>();
        _minHeight = element.minHeight;
        _preferredHeight = element.preferredHeight;
        _flexibleHeight = element.flexibleHeight;
        _layoutPriority = element.layoutPriority;
        var group = entry.GetComponent<HorizontalOrVerticalLayoutGroup>();
        _entryPadding = group != null
            ? new RectOffset(group.padding.left, group.padding.right, group.padding.top, group.padding.bottom)
            : null;
        _minWidth = element.minWidth;
        _preferredWidth = element.preferredWidth;
        _flexibleWidth = element.flexibleWidth;
        var label = entry.GetComponentInChildren<TMP_Text>();
        var scaled = label != null ? label.GetComponent<SampleUiScaledText>() : null;
        if (scaled != null)
        {
            _baseSize = scaled.BaseSize;
            _baseLineHeight = scaled.BaseLineHeight;
            scaled.Configure(SampleUi.TextBody, OctopusSampleBranding.Dp(20f));
        }

        var rect = (RectTransform)transform;
        entry.SetParent(rect, false);
        float width = Mathf.Max(OctopusSampleBranding.Dp(80f), SampleUi.PreferredButtonWidth(entry));
        element.minWidth = element.preferredWidth = width;
        element.flexibleWidth = 0f;
        SampleUi.AppBarItem(entry);
        if (rect.GetComponent<HorizontalOrVerticalLayoutGroup>() != null) return;
        // A header posed by hand rather than by a layout group: sit at its trailing edge, the place
        // the layout group would have given the last child anyway.
        entry.anchorMin = entry.anchorMax = entry.pivot = new Vector2(1f, 0.5f);
        entry.sizeDelta = new Vector2(width, OctopusSampleBranding.MinTouchUnits);
        entry.anchoredPosition = new Vector2(-_trailingInset, 0f);
    }

    /// <summary>
    /// Hands the entry back to the shell before a route tears its hierarchy down. Unity refuses a
    /// reparent from OnDisable ("cannot set the parent while deactivating"), and by OnDestroy the
    /// entry — a child of the header — is already destroyed with it: a route therefore calls this
    /// immediately before it destroys its root, whether it is closing or rebuilding itself.
    /// </summary>
    public static void ReleaseAll()
    {
        for (int i = Hosts.Count - 1; i >= 0; i--)
        {
            var host = Hosts[i];
            if (host != null) host.Release(true);
        }
    }

    /// <summary>
    /// Puts the entry back where it was found. <paramref name="announce"/> is false when another
    /// host is taking over — it already knows it has the entry — and true when this route closes,
    /// which re-announces the entry so the shell app bar adopts it again.
    /// </summary>
    internal void Release(bool announce)
    {
        Hosts.Remove(this);
        if (_entry == null) return;
        SampleUi.DebugEntryClaimed -= OnClaimed;
        var entry = _entry;
        _entry = null;
        var label = entry.GetComponentInChildren<TMP_Text>();
        var scaled = label != null ? label.GetComponent<SampleUiScaledText>() : null;
        if (scaled != null) scaled.Configure(_baseSize, _baseLineHeight);
        var element = entry.GetComponent<LayoutElement>();
        if (element != null)
        {
            element.minHeight = _minHeight;
            element.preferredHeight = _preferredHeight;
            element.flexibleHeight = _flexibleHeight;
            element.layoutPriority = _layoutPriority;
            element.minWidth = _minWidth;
            element.preferredWidth = _preferredWidth;
            element.flexibleWidth = _flexibleWidth;
        }
        // The owner can already be gone during an unload or an application shutdown.
        var group = entry.GetComponent<HorizontalOrVerticalLayoutGroup>();
        if (group != null && _entryPadding != null) group.padding = _entryPadding;
        if (_parent == null) return;
        entry.SetParent(_parent, false);
        entry.anchorMin = _anchorMin;
        entry.anchorMax = _anchorMax;
        entry.pivot = _pivot;
        entry.sizeDelta = _size;
        entry.anchoredPosition = _position;
        if (announce) SampleUi.RegisterDebugEntry(entry);
    }

    private void OnClaimed(object claimant)
    {
        if (!ReferenceEquals(claimant, this)) Release(false);
    }

    // A last-resort unregister: by this point a header destroyed with the entry still under it has
    // already taken the entry down, and Release finds it null. ReleaseAll above is what actually
    // saves it, called by the route before it destroys anything.
    private void OnDestroy() { Release(true); }
}
