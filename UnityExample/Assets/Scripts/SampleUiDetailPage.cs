using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Constrains a detail to the shell viewport; system and tab insets are consumed once.</summary>
public class SampleUiDetailPage : MonoBehaviour
{
    private static readonly List<SampleUiDetailPage> Pages = new List<SampleUiDetailPage>();
    private static readonly Dictionary<GameObject, OctopusSampleTab> OwnerTabs = new Dictionary<GameObject, OctopusSampleTab>();
    private Action _close;
    private OctopusSampleTab _tab;
    private GameObject _owner;
    private bool _suspended;
    private RectTransform _topBleed, _bottomBleed;

    private static int _shown;

    /// <summary>
    /// Whether a detail page is on screen: its header is the shared navy chrome, so the status bar
    /// above it wants light icons whatever the tab beneath draws (see <see cref="OctopusSampleSystemBars"/>).
    /// </summary>
    public static bool AnyShown { get { return _shown > 0; } }

    private void OnEnable() { _shown++; }
    private void OnDisable() { _shown = Mathf.Max(0, _shown - 1); }

    public void Initialize(Action close, bool retainTabs)
    {
        _close = close;
        bool hasTabs = retainTabs && FindAnyObjectByType<OctopusSampleShell>() != null;
        if (hasTabs)
        {
            _owner = GetComponentInParent<Canvas>(true).rootCanvas.gameObject;
            foreach (var owner in new List<GameObject>(OwnerTabs.Keys))
                if (owner == null) OwnerTabs.Remove(owner);
            if (!OwnerTabs.TryGetValue(_owner, out _tab))
            {
                _tab = FindAnyObjectByType<OctopusSampleShell>().Selected;
                OwnerTabs.Add(_owner, _tab);
            }
            _suspended = !_owner.activeSelf;
            // A rebuild retires its previous page before constructing this one. Keep only the
            // current page per owner, while retaining other tabs' suspended canvases.
            Pages.RemoveAll(page => page == null || (page._owner == _owner && !page.gameObject.activeSelf));
            Pages.Add(this);
        }
        // Inset fills are siblings: they paint the physical edges without covering shell tabs.
        // Clear in dark (Header), so the bleed carries its own halo: the strip under the status bar
        // then continues the page's glow whether a shell lies beneath the detail or not.
        _topBleed = SampleUi.BuildBleed((RectTransform)transform.parent, "TopBleed",
            OctopusSampleBranding.Palette.Header, true);
        SampleUi.Halo(_topBleed);
        if (!hasTabs) _bottomBleed = SampleUi.BuildBleed((RectTransform)transform.parent, "BottomBleed",
            OctopusSampleBranding.Palette.TabBar, false);
        var safe = gameObject.AddComponent<SampleUiSafeArea>();
        safe.BottomInsetUnits = hasTabs ? OctopusSampleShell.TabBarHeight : 0f;
        safe.Apply(SampleUiSafeArea.ScreenSafeArea(), SampleUiSafeArea.KeyboardArea(), new Vector2(Screen.width, Screen.height));
    }

    /// <summary>Hide other tabs' detail canvases without closing their draft/result/back stack.</summary>
    public static void HideOtherTabs(OctopusSampleTab tab)
    {
        foreach (var page in Pages.ToArray())
        {
            if (page == null || page._tab == tab || page._owner == null || !page._owner.activeSelf) continue;
            foreach (var host in page._owner.GetComponentsInChildren<SampleUiDebugEntryHost>(true))
                host.Release(true);
            page._suspended = true;
            page._owner.SetActive(false);
        }
    }

    /// <summary>Restore the chosen tab's pages in their original opening order.</summary>
    public static void ShowTab(OctopusSampleTab tab)
    {
        foreach (var page in Pages.ToArray())
        {
            if (page == null || page._tab != tab || page._owner == null) continue;
            if (page._suspended)
            {
                page._suspended = false;
                page._owner.SetActive(true);
            }
            if (!page._owner.activeSelf) continue;
            var hosts = page._owner.GetComponentsInChildren<SampleUiDebugEntryHost>();
            foreach (var host in hosts) host.Resume();
        }
    }

    /// <summary>An explicit link to a section reveals its list, rather than its retained overlay.</summary>
    public static void CloseForNavigation(OctopusSampleTab destination)
    {
        foreach (var page in Pages.ToArray())
        {
            if (page == null || (page._tab != destination && !page.gameObject.activeInHierarchy)) continue;
            Pages.Remove(page);
            page._close();
        }
    }

    public static void CloseAll(bool includeHidden = false)
    {
        foreach (var page in Pages.ToArray())
        {
            if (page == null || (!page.gameObject.activeInHierarchy && !includeHidden)) continue;
            Pages.Remove(page);
            page._close();
        }
        Pages.RemoveAll(page => page == null);
    }

    private void LateUpdate()
    {
        SampleUi.ResizeBleed(_topBleed, true);
        SampleUi.ResizeBleed(_bottomBleed, false);
    }

    private void OnDestroy()
    {
        Pages.Remove(this);
        RemoveBleed(_topBleed);
        RemoveBleed(_bottomBleed);
    }

    private static void RemoveBleed(RectTransform bleed)
    {
        if (bleed == null) return;
        bleed.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(bleed.gameObject); else DestroyImmediate(bleed.gameObject);
    }
}
