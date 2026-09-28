using UnityEngine;

/// <summary>Shared lifecycle for the configuration and account overlays.</summary>
public abstract class OctopusSampleSettingsDetailView : MonoBehaviour
{
    private bool _rebuildRequested;

    protected void InitializeView()
    {
        SampleUi.OverlayCanvas(gameObject, SampleUi.DetailSortingOrder);
        Build();
        OctopusSampleBranding.ThemeChanged += Refresh;
    }

    protected abstract void Build();

    protected RectTransform Content(string screenId, string title, string backId, bool canGoBack)
    {
        var root = SampleUi.DetailPage(screenId, transform, Dismiss, canGoBack);
        var header = SampleUi.AppBar("Header", root, title,
            canGoBack ? new UnityEngine.Events.UnityAction(Close) : null, backId, "Line");
        // Keep first-launch onboarding self-contained; no debug route can initialize the SDK
        // behind a configuration screen that has not yet been submitted.
        if (canGoBack) SampleUiDebugEntryHost.Attach(header);
        return SampleUi.VerticalScroll(root, SampleUi.OverlayPadding());
    }

    protected void Refresh()
    {
        if (this == null) { Unsubscribe(); return; }
        if (Application.isPlaying) _rebuildRequested = true;
        else Rebuild();
    }

    protected virtual void LateUpdate()
    {
        if (!_rebuildRequested) return;
        _rebuildRequested = false;
        Rebuild();
    }

    private void Rebuild()
    {
        SampleUiDebugEntryHost.ReleaseAll();
        for (var i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            child.SetActive(false);
            child.transform.SetParent(null, false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        Build();
    }

    protected virtual void Unsubscribe()
    {
        OctopusSampleBranding.ThemeChanged -= Refresh;
    }

    /// <summary>
    /// What a tab switch runs to take this page away (<see cref="SampleUiDetailPage.CloseAll"/>).
    /// <see cref="Close"/> by default; a view whose Close navigates somewhere overrides it, since
    /// the tab the reader just picked is where they are going.
    /// </summary>
    protected virtual void Dismiss() { Close(); }

    public virtual void Close()
    {
        Unsubscribe();
        SampleUiDebugEntryHost.ReleaseAll();
        gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
    }

    protected virtual void OnDestroy() { Unsubscribe(); }
}
