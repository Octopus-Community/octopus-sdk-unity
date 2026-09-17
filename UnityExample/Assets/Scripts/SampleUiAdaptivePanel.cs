using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Lets authored panel heights act as floors at larger text sizes. Layout groups retain ownership
/// of their children; anchored headers and docks grow and reserve the extra space in their sibling
/// viewports. No screen ids or copies of a screen's layout constants are needed here.
/// </summary>
[ExecuteAlways]
public class SampleUiAdaptivePanel : UIBehaviour, ILayoutElement, ILayoutSelfController, ILayoutGroup
{
    public static uint LayoutRevision { get; private set; }

    protected override void OnEnable() { base.OnEnable(); LayoutRevision++; }
    protected override void OnDisable() { LayoutRevision++; base.OnDisable(); }

    private float _authoredHeight = -1f;
    private bool _resizing;
    private bool _dirty;

    public float minWidth { get { return -1f; } }
    public float preferredWidth { get { return -1f; } }
    public float flexibleWidth { get { return -1f; } }
    public float minHeight { get { return -1f; } }
    public float flexibleHeight { get { return -1f; } }
    public int layoutPriority { get { return 1; } }

    public float preferredHeight
    {
        get
        {
            if (OctopusSampleTextScale.Current <= 1f) return -1f;
            var group = GetComponent<LayoutGroup>();
            if (group != null && group.isActiveAndEnabled) return group.preferredHeight;
            // InputField measures the full buffer, rather than its visible substring.
            var field = GetComponent<SampleUiInputField>();
            if (field != null) return field.preferredHeight;
            float height = -1f;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                var element = child.GetComponent<LayoutElement>();
                if (element != null && element.ignoreLayout) continue;
                float preferred = LayoutUtility.GetPreferredHeight(child);
                if (preferred <= 0f) continue; // Decorative shapes do not reserve text space.
                float span = child.anchorMax.y - child.anchorMin.y;
                if (span > 0f)
                    preferred = (preferred - child.sizeDelta.y) / span;
                else if (Mathf.Approximately(child.anchorMin.y, 1f))
                    preferred = -child.anchoredPosition.y + preferred * child.pivot.y;
                else if (Mathf.Approximately(child.anchorMin.y, 0f))
                    preferred = child.anchoredPosition.y + preferred * (1f - child.pivot.y);
                else
                    preferred += 2f * Mathf.Abs(child.anchoredPosition.y);
                height = Mathf.Max(height, preferred);
            }
            return height;
        }
    }

    public void CalculateLayoutInputHorizontal() { }
    public void CalculateLayoutInputVertical() { }
    public void SetLayoutHorizontal() { }

    public void SetLayoutVertical()
    {
        if (_resizing) return;
        var rect = (RectTransform)transform;
        var parent = rect.parent as RectTransform;
        if (parent == null || !Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y)) return;
        var parentGroup = parent.GetComponent<LayoutGroup>();
        var element = GetComponent<LayoutElement>();
        if (parentGroup != null && parentGroup.isActiveAndEnabled &&
            (element == null || !element.ignoreLayout)) return;
        // Scroll content owns its height through its existing ContentSizeFitter.
        if (GetComponent<ContentSizeFitter>() != null || GetComponent<SampleUiSafeArea>() != null) return;
        float preferred = LayoutUtility.GetPreferredHeight(rect);
        if (_authoredHeight < 0f)
        {
            if (OctopusSampleTextScale.Current <= 1f || preferred <= rect.rect.height) return;
            _authoredHeight = rect.rect.height;
        }
        float height = Mathf.Max(_authoredHeight, preferred);
        float extra = height - rect.rect.height;
        if (Mathf.Abs(extra) < 0.1f) return;
        _resizing = true;
        bool top = Mathf.Approximately(rect.anchorMin.y, 1f);
        bool bottom = Mathf.Approximately(rect.anchorMin.y, 0f);
        float oldEdge = top ? rect.anchoredPosition.y - rect.rect.height * rect.pivot.y
            : rect.anchoredPosition.y + rect.rect.height * (1f - rect.pivot.y);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        // Preserve the outer edge even when an older header uses a centred pivot.
        if (top) rect.anchoredPosition -= new Vector2(0f, extra * (1f - rect.pivot.y));
        if (bottom) rect.anchoredPosition += new Vector2(0f, extra * rect.pivot.y);
        if (top || bottom)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var sibling = parent.GetChild(i) as RectTransform;
                if (sibling == null || sibling == rect || sibling.anchorMax.y <= sibling.anchorMin.y) continue;
                // Only siblings already reserving this bar's original space follow its growth.
                if (top && sibling.offsetMax.y <= oldEdge + 0.1f)
                    sibling.offsetMax -= new Vector2(0f, extra);
                if (bottom && sibling.offsetMin.y >= oldEdge - 0.1f)
                    sibling.offsetMin += new Vector2(0f, extra);
            }
        }
        _resizing = false;
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        LayoutRevision++;
        if (!_resizing && isActiveAndEnabled) SetDirty();
    }

    private void OnTransformChildrenChanged()
    {
        LayoutRevision++;
        SetDirty();
    }

    private void SetDirty()
    {
        if (CanvasUpdateRegistry.IsRebuildingLayout()) _dirty = true;
        else LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
    }

    private void LateUpdate()
    {
        if (!_dirty) return;
        _dirty = false;
        SetDirty();
    }
}
