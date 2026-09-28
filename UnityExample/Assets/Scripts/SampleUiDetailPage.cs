using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Constrains a detail to the shell viewport; system and tab insets are consumed once.</summary>
public class SampleUiDetailPage : MonoBehaviour
{
    private static readonly List<SampleUiDetailPage> Pages = new List<SampleUiDetailPage>();
    private Action _close;
    private RectTransform _topBleed, _bottomBleed;

    public void Initialize(Action close, bool retainTabs)
    {
        _close = close;
        bool hasTabs = retainTabs && FindAnyObjectByType<OctopusSampleShell>() != null;
        if (hasTabs) Pages.Add(this);
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

    public static void CloseAll()
    {
        foreach (var page in Pages.ToArray())
            if (page != null && page.gameObject.activeInHierarchy) page._close();
        Pages.RemoveAll(page => page == null || !page.gameObject.activeInHierarchy);
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
