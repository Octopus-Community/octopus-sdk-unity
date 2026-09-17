using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Enforces the touch floor on the actual rectangle, also for manually anchored controls.
///
/// The contract is one frame, deliberately: a resize is recorded in
/// <c>OnRectTransformDimensionsChange</c> and applied in the next <c>LateUpdate</c>, never inside
/// the callback, because resizing from there re-enters the layout pass Unity is running. A control
/// can therefore be under the floor for the frame it is built in — a test that shrinks a rect reads
/// the floor after a frame, or after invoking <c>LateUpdate</c> itself. Behaviour, not a bug.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class SampleUiTouchTarget : UIBehaviour
{
    private bool _resizing;
    private bool _minimumRequested;

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureMinimum();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        _minimumRequested = true;
    }

    private void LateUpdate()
    {
        if (!_minimumRequested) return;
        _minimumRequested = false;
        EnsureMinimum();
    }

    private void EnsureMinimum()
    {
        if (_resizing || !isActiveAndEnabled) return;
        _resizing = true;
        var rect = (RectTransform)transform;
        float min = OctopusSampleBranding.MinTouchUnits;
        if (rect.rect.width < min) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, min);
        if (rect.rect.height < min) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, min);
        _resizing = false;
    }
}
