using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Updates existing controls in place when safe area, rotation or keyboard changes.</summary>
[ExecuteAlways]
public class SampleUiSafeArea : UIBehaviour
{
    public float BottomInsetUnits;
    public bool KeepBarsPinned;
    private RectTransform _viewport;
    private ScrollRect _scroll;
    private static int _keyboardFrame = -1;
    private static Rect _keyboardArea;
    private bool _applied;
    private Rect _lastSafe, _lastKeyboard;
    private Vector2 _lastScreen;
    private float _lastScale;
    private uint _layoutRevision;

    // Android system-bar insets, re-read every few frames (a JNI round trip) and on a resize.
    private const int InsetsRefreshFrames = 10;
    private static int _insetsFrame = -1;
    private static Vector2 _insetsScreen;
    private static Vector4 _systemInsets;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetKeyboardCache()
    {
        _keyboardFrame = -1;
        _keyboardArea = new Rect();
        _insetsFrame = -1;
        _systemInsets = Vector4.zero;
    }

    /// <summary>
    /// The part of the screen interactive content may use, in screen pixels (origin bottom-left):
    /// <see cref="Screen.safeArea"/> further tightened by the Android system bars.
    ///
    /// <see cref="OctopusSampleSystemBars"/> shows the status and navigation bars over a player that
    /// renders edge to edge, and Unity's safe area there reports the display cutout but not the
    /// status bar: the app bar's title then sits under the clock and its chips under the status
    /// icons. Every inset consumer (safe-area layouts, bleeds, the overlay inset) reads this instead
    /// of <see cref="Screen.safeArea"/>. In the editor and on other platforms it is
    /// <see cref="Screen.safeArea"/> unchanged.
    /// </summary>
    public static Rect ScreenSafeArea()
    {
        var safe = Screen.safeArea;
#if UNITY_ANDROID && !UNITY_EDITOR
        var screen = new Vector2(Screen.width, Screen.height);
        if (_insetsFrame < 0 || screen != _insetsScreen || Time.frameCount - _insetsFrame >= InsetsRefreshFrames)
        {
            _insetsFrame = Time.frameCount;
            _insetsScreen = screen;
            _systemInsets = QuerySystemInsets(screen);
        }
        safe = Inset(safe, _systemInsets, screen);
#endif
        return safe;
    }

    /// <summary>
    /// <paramref name="safe"/> shrunk to clear <paramref name="insets"/> (left, top, right, bottom
    /// in screen pixels, as x, y, z, w) on a screen of <paramref name="screen"/> pixels. Each edge
    /// keeps the tighter of the two, so an inset Unity already reports is never counted twice.
    /// </summary>
    public static Rect Inset(Rect safe, Vector4 insets, Vector2 screen)
    {
        float left = Mathf.Max(safe.xMin, insets.x);
        float bottom = Mathf.Max(safe.yMin, insets.w);
        float right = Mathf.Max(left, Mathf.Min(safe.xMax, screen.x - insets.z));
        float top = Mathf.Max(bottom, Mathf.Min(safe.yMax, screen.y - insets.y));
        return Rect.MinMaxRect(left, bottom, right, top);
    }

    /// <summary>
    /// How far system bars of <paramref name="bars"/> (left, top, right, bottom insets of a window
    /// of <paramref name="window"/> pixels, as x, y, z, w) reach into a view occupying
    /// <paramref name="view"/> of that window (window pixels, origin top-left), in the view's pixels.
    /// The player's view does not always span the window: on a device that keeps the navigation bar
    /// out of it, only the status bar overlaps, and counting both would inset the bottom twice.
    /// </summary>
    public static Vector4 BarsOverView(Rect view, Vector2 window, Vector4 bars)
    {
        return new Vector4(Mathf.Max(0f, bars.x - view.xMin),
                           Mathf.Max(0f, bars.y - view.yMin),
                           Mathf.Max(0f, view.xMax - (window.x - bars.z)),
                           Mathf.Max(0f, view.yMax - (window.y - bars.w)));
    }

    /// <summary>
    /// <paramref name="over"/> (insets in the surface's own pixels) in screen pixels, for a surface
    /// laid out at <paramref name="surface"/> pixels (its width and height, not its visible rect: a
    /// surface partly off-screen or clipped keeps its resolution) rendering a screen of
    /// <paramref name="screen"/> pixels. Zero when either size is unknown.
    /// </summary>
    public static Vector4 ToScreen(Vector4 over, Vector2 surface, Vector2 screen)
    {
        if (surface.x <= 0f || surface.y <= 0f || screen.x <= 0f || screen.y <= 0f) return Vector4.zero;
        float sx = screen.x / surface.x, sy = screen.y / surface.y;
        return new Vector4(over.x * sx, over.y * sy, over.z * sx, over.w * sy);
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // The player's surface and the decor view, resolved once and again only when the screen changes
    // size or orientation: the lookup walks the view tree, the insets read that follows does not.
    private static AndroidJavaObject _surface, _decor;
    private static Vector2 _resolvedScreen;
    private static ScreenOrientation _resolvedOrientation;

    private static void ReleaseViews()
    {
        if (_surface != null) _surface.Dispose();
        if (_decor != null) _decor.Dispose();
        _surface = _decor = null;
    }

    private static bool ResolveViews(Vector2 screen)
    {
        if (_surface != null && _decor != null && screen == _resolvedScreen &&
            Screen.orientation == _resolvedOrientation)
            return true;
        ReleaseViews();
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            if (activity == null) return false;
            using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                _decor = window.Call<AndroidJavaObject>("getDecorView");
            _surface = PlayerView(activity);
        }
        _resolvedScreen = screen;
        _resolvedOrientation = Screen.orientation;
        return _surface != null && _decor != null;
    }

    /// <summary>
    /// The visible system bars' insets over the player's surface, scaled to screen pixels; zero when
    /// unknown.
    /// </summary>
    private static Vector4 QuerySystemInsets(Vector2 screen)
    {
        try
        {
            if (!ResolveViews(screen)) return Vector4.zero;
            using (var insets = _decor.Call<AndroidJavaObject>("getRootWindowInsets"))
            using (var frame = new AndroidJavaObject("android.graphics.Rect"))
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                // A null insets or an invisible surface means the cached views no longer describe
                // the window (detached, being replaced): drop them so the next read re-resolves.
                if (insets == null)
                {
                    ReleaseViews();
                    return Vector4.zero;
                }
                var window = new Vector2(_decor.Call<int>("getWidth"), _decor.Call<int>("getHeight"));
                // The surface Unity draws on, not android.R.id.content: content spans the whole
                // window while the surface stops above the navigation bar. Its visible rect places
                // it in the window; its laid-out size is what the player renders at.
                if (!_surface.Call<bool>("getGlobalVisibleRect", frame))
                {
                    ReleaseViews();
                    return Vector4.zero;
                }
                var view = Rect.MinMaxRect(frame.Get<int>("left"), frame.Get<int>("top"),
                                           frame.Get<int>("right"), frame.Get<int>("bottom"));
                var size = new Vector2(_surface.Call<int>("getWidth"), _surface.Call<int>("getHeight"));
                if (window.x <= 0f || window.y <= 0f || view.width <= 0f || view.height <= 0f) return Vector4.zero;
                Vector4 bars;
                if (version.GetStatic<int>("SDK_INT") >= 30)
                {
                    using (var type = new AndroidJavaClass("android.view.WindowInsets$Type"))
                    using (var system = insets.Call<AndroidJavaObject>("getInsets", type.CallStatic<int>("systemBars")))
                    {
                        if (system == null) return Vector4.zero;
                        bars = new Vector4(system.Get<int>("left"), system.Get<int>("top"),
                                           system.Get<int>("right"), system.Get<int>("bottom"));
                    }
                }
                else
                {
                    bars = new Vector4(insets.Call<int>("getSystemWindowInsetLeft"),
                                       insets.Call<int>("getSystemWindowInsetTop"),
                                       insets.Call<int>("getSystemWindowInsetRight"),
                                       insets.Call<int>("getSystemWindowInsetBottom"));
                }
                return ToScreen(BarsOverView(view, window, bars), size, screen);
            }
        }
        // Best effort, called from LateUpdate: any failure falls back to Unity's safe area alone,
        // and the views are looked up again next time.
        catch (System.Exception)
        {
            ReleaseViews();
            return Vector4.zero;
        }
    }

    /// <summary>
    /// The view Unity renders into: the player's <c>unitySurfaceView</c> when the build declares
    /// one, else the first <c>SurfaceView</c> under <c>android.R.id.content</c>, else content itself.
    /// </summary>
    private static AndroidJavaObject PlayerView(AndroidJavaObject activity)
    {
        using (var resources = activity.Call<AndroidJavaObject>("getResources"))
        {
            var id = resources.Call<int>("getIdentifier", "unitySurfaceView", "id",
                                         activity.Call<string>("getPackageName"));
            if (id != 0)
            {
                var surface = activity.Call<AndroidJavaObject>("findViewById", id);
                if (surface != null) return surface;
            }
        }
        var content = activity.Call<AndroidJavaObject>("findViewById", 0x01020002); // android.R.id.content
        if (content == null) return null;
        using (var classType = new AndroidJavaClass("java.lang.Class"))
        using (var type = classType.CallStatic<AndroidJavaObject>("forName", "android.view.SurfaceView"))
        using (var groupType = classType.CallStatic<AndroidJavaObject>("forName", "android.view.ViewGroup"))
        {
            var found = FindSurface(content, type, groupType, 4);
            if (found == null) return content;
            content.Dispose();
            return found;
        }
    }

    private static AndroidJavaObject FindSurface(AndroidJavaObject view, AndroidJavaObject surfaceType,
                                                 AndroidJavaObject groupType, int depth)
    {
        if (depth < 0 || !groupType.Call<bool>("isInstance", view)) return null;
        var count = view.Call<int>("getChildCount");
        for (var i = 0; i < count; i++)
        {
            var child = view.Call<AndroidJavaObject>("getChildAt", i);
            if (child == null) continue;
            if (surfaceType.Call<bool>("isInstance", child)) return child;
            var deeper = FindSurface(child, surfaceType, groupType, depth - 1);
            child.Dispose();
            if (deeper != null) return deeper;
        }
        return null;
    }
#endif

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
        if (BottomInsetUnits > 0f && screen.x > 0f && screen.y > 0f)
        {
            float unitsPerPixel = SampleUi.ReferenceWidthFor(SampleUi.ScreenDpWidth()) / screen.x;
            area.yMin = Mathf.Min(area.yMax, area.yMin + BottomInsetUnits / unitsPerPixel / screen.y);
        }
        var rect = _viewport != null ? _viewport : (RectTransform)transform;
        if (rect.anchorMin != area.min || rect.anchorMax != area.max ||
            rect.offsetMin != Vector2.zero || rect.offsetMax != Vector2.zero)
            SampleUi.Stretch(rect, area.min, area.max);
        FitPage();
    }

    private void FitPage()
    {
        var page = (RectTransform)transform;
        float minimum = KeepBarsPinned ? 0f : MinimumPageHeight(page);
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
        var safe = ScreenSafeArea();
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
                            if (height > ScreenSafeArea().yMin)
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
