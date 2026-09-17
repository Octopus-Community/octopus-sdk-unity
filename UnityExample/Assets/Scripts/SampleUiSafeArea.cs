using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Updates existing controls in place when safe area, rotation or keyboard changes.</summary>
[ExecuteAlways]
public class SampleUiSafeArea : UIBehaviour
{
    private RectTransform _viewport;
    private ScrollRect _scroll;
    private static int _keyboardFrame = -1;
    private static Rect _keyboardArea;
    private bool _applied;
    private Rect _lastSafe, _lastKeyboard;
    private Vector2 _lastScreen;
    private float _lastScale;
    private uint _layoutRevision;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetKeyboardCache()
    {
        _keyboardFrame = -1;
        _keyboardArea = new Rect();
    }

    public static Rect UsableArea(Rect safe, Rect keyboard, Vector2 screen)
    {
        if (screen.x <= 0f || screen.y <= 0f) return new Rect(0f, 0f, 1f, 1f);
        float left = Mathf.Clamp(safe.xMin, 0f, screen.x);
        float right = Mathf.Clamp(safe.xMax, left, screen.x);
        float top = Mathf.Clamp(safe.yMax, 0f, screen.y);
        float bottom = Mathf.Clamp(safe.yMin, 0f, top);
        // Conservatively keep targets above floating as well as docked keyboards.
        if (keyboard.width > 0f && keyboard.height > 0f && keyboard.xMax > left && keyboard.xMin < right)
            bottom = Mathf.Clamp(Mathf.Max(bottom, keyboard.yMax), bottom, top);
        return Rect.MinMaxRect(left / screen.x, bottom / screen.y, right / screen.x, top / screen.y);
    }

    public void Apply(Rect safe, Rect keyboard, Vector2 screen)
    {
        _applied = false;
        var area = UsableArea(safe, keyboard, screen);
        var rect = _viewport != null ? _viewport : (RectTransform)transform;
        if (rect.anchorMin != area.min || rect.anchorMax != area.max ||
            rect.offsetMin != Vector2.zero || rect.offsetMax != Vector2.zero)
            SampleUi.Stretch(rect, area.min, area.max);
        FitPage();
    }

    private void FitPage()
    {
        var page = (RectTransform)transform;
        float minimum = MinimumPageHeight(page);
        if (_viewport == null)
        {
            if (minimum <= page.rect.height + 0.1f) return;
            // Leave the normal hierarchy/anchors intact. Only a page that cannot fit acquires
            // an outer scroll viewport, so existing scroll state still belongs to its content.
            var min = page.anchorMin;
            var max = page.anchorMax;
            _viewport = SampleUi.Panel("SafeAreaViewport", page.parent, Color.clear);
            _viewport.SetSiblingIndex(page.GetSiblingIndex());
            SampleUi.Stretch(_viewport, min, max);
            _viewport.GetComponent<Image>().raycastTarget = true;
            _viewport.gameObject.AddComponent<RectMask2D>();
            _scroll = _viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = _viewport;
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            page.SetParent(_viewport, false);
            page.anchorMin = new Vector2(0f, 1f);
            page.anchorMax = Vector2.one;
            page.pivot = new Vector2(0.5f, 1f);
            page.sizeDelta = Vector2.zero;
            page.anchoredPosition = Vector2.zero;
            _scroll.content = page;
        }
        else if (minimum <= _viewport.rect.height + 0.1f)
        {
            var obsolete = _viewport;
            page.SetParent(obsolete.parent, false);
            page.SetSiblingIndex(obsolete.GetSiblingIndex());
            SampleUi.Stretch(page, obsolete.anchorMin, obsolete.anchorMax);
            _viewport = null;
            _scroll = null;
            obsolete.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(obsolete.gameObject);
            else DestroyImmediate(obsolete.gameObject);
            return;
        }
        // Keep at least a full touch target and a drag region below the pinned bars on short
        // landscape/keyboard viewports; the whole page remains reachable by scrolling.
        float height = Mathf.Max(_viewport.rect.height, minimum);
        if (!Mathf.Approximately(page.rect.height, height))
            page.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        float extra = Mathf.Max(0f, height - _viewport.rect.height);
        _scroll.vertical = extra > 0.1f;
        float y = Mathf.Clamp(page.anchoredPosition.y, 0f, extra);
        if (!Mathf.Approximately(page.anchoredPosition.y, y)) page.anchoredPosition = new Vector2(0f, y);
    }

    private static float MinimumPageHeight(RectTransform parent)
    {
        if (parent.GetComponent<ScrollRect>() != null) return 2f * OctopusSampleBranding.MinTouchUnits;
        float top = 0f, bottom = 0f, height = 0f;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeInHierarchy || child.GetComponent<TMP_Text>() != null) continue;
            if (child.anchorMin.y < child.anchorMax.y)
                height = Mathf.Max(height, MinimumPageHeight(child) - child.sizeDelta.y);
            else if (Mathf.Approximately(child.anchorMin.y, 1f))
                top = Mathf.Max(top, -child.anchoredPosition.y + child.rect.height * child.pivot.y);
            else if (Mathf.Approximately(child.anchorMin.y, 0f))
                bottom = Mathf.Max(bottom, child.anchoredPosition.y + child.rect.height * (1f - child.pivot.y));
        }
        return Mathf.Max(height, top + bottom);
    }

    public void Refresh()
    {
        var safe = Screen.safeArea;
        var keyboard = KeyboardArea();
        var screen = new Vector2(Screen.width, Screen.height);
        var scale = OctopusSampleTextScale.Current;
        var revision = SampleUiAdaptivePanel.LayoutRevision;
        if (_applied && _lastSafe == safe && _lastKeyboard == keyboard && _lastScreen == screen &&
            _lastScale == scale && _layoutRevision == revision) return;
        _lastSafe = safe;
        _lastKeyboard = keyboard;
        _lastScreen = screen;
        _lastScale = scale;
        _layoutRevision = revision;
        Apply(safe, keyboard, screen);
        _applied = true;
    }

    public static Rect KeyboardArea()
    {
        if (!Application.isPlaying) return new Rect();
        if (_keyboardFrame == Time.frameCount) return _keyboardArea;
        _keyboardFrame = Time.frameCount;
        Rect keyboard = TouchScreenKeyboard.visible ? TouchScreenKeyboard.area : new Rect();
#if UNITY_ANDROID && !UNITY_EDITOR
        // Unity's Android keyboard area can be empty. The visible display frame is in physical
        // screen pixels; ignore reductions limited to the system bottom inset.
        if (TouchScreenKeyboard.visible && keyboard.height <= 0f)
        {
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (activity != null)
                    {
                        using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                        using (var decor = window.Call<AndroidJavaObject>("getDecorView"))
                        using (var frame = new AndroidJavaObject("android.graphics.Rect"))
                        {
                            decor.Call("getWindowVisibleDisplayFrame", frame);
                            float height = Screen.height - frame.Get<int>("bottom");
                            if (height > Screen.safeArea.yMin)
                                keyboard = new Rect(0f, 0f, Screen.width, height);
                        }
                    }
                }
            }
            // Best effort, called from LateUpdate: any failure leaves the keyboard rect empty.
            catch (System.Exception) { }
        }
#endif
        _keyboardArea = keyboard;
        return keyboard;
    }

    protected override void OnEnable() { base.OnEnable(); _applied = false; Refresh(); }
    private void LateUpdate() { Refresh(); }
}
