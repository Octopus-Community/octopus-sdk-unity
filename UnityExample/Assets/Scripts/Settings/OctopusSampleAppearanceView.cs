using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The Appearance route edits the sample theme; Settings itself remains an index.</summary>
public class OctopusSampleAppearanceView : MonoBehaviour
{
    public const string ScreenId = "settings-appearance-screen";
    public const string BackId = "settings-appearance-back";
    // Keep both existing QA handles: the choice group and the switch moved from the shell.
    public const string ThemeToggleId = "settings-appearance-theme-toggle";
    public const string ShellThemeToggleId = "shell-theme-toggle";
    public const string LightId = "settings-appearance-light";
    public const string DarkId = "settings-appearance-dark";
    private bool _rebuildRequested;

    public static OctopusSampleAppearanceView Open()
    {
        var existing = FindAnyObjectByType<OctopusSampleAppearanceView>();
        if (existing != null) return existing;
        var view = new GameObject("OctopusSampleAppearanceView").AddComponent<OctopusSampleAppearanceView>();
        SampleUi.OverlayCanvas(view.gameObject, SampleUi.DetailSortingOrder);
        view.Build();
        OctopusSampleBranding.ThemeChanged += view.OnThemeChanged;
        return view;
    }

    public static bool IsOpen { get { return FindAnyObjectByType<OctopusSampleAppearanceView>() != null; } }

    public void ToggleTheme()
    {
        OctopusSampleBranding.Theme = OctopusSampleBranding.Theme == OctopusSampleTheme.Dark
            ? OctopusSampleTheme.Light : OctopusSampleTheme.Dark;
    }

    public void Close()
    {
        OctopusSampleBranding.ThemeChanged -= OnThemeChanged;
        // The entry rides in this header: hand it back before the hierarchy goes away.
        SampleUiDebugEntryHost.ReleaseAll();
        if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
    }

    private void OnDestroy() { OctopusSampleBranding.ThemeChanged -= OnThemeChanged; }

    private void OnThemeChanged()
    {
        if (this == null) { OctopusSampleBranding.ThemeChanged -= OnThemeChanged; return; }
        if (Application.isPlaying) _rebuildRequested = true;
        else Rebuild();
    }

    private void LateUpdate()
    {
        if (!_rebuildRequested) return;
        _rebuildRequested = false;
        Rebuild();
    }

    private void Rebuild()
    {
        // The header being thrown away hosts the entry: hand it back before it goes.
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

    private void Build()
    {
        var ground = SampleUi.Panel("Ground", transform, SampleUi.Background);
        SampleUi.Stretch(ground, Vector2.zero, Vector2.one);
        var root = SampleUi.SafeArea(ScreenId, ground);
        var header = SampleUi.AppBar("Header", root, "Appearance", Close, BackId, "Line");
        SampleUiDebugEntryHost.Attach(header);
        var content = SampleUi.VerticalScroll(root, SampleUi.OverlayPadding());
        var card = SampleUi.Card("settings-appearance-card", content);
        SampleUi.FlexibleLabel(card, "Sample interface · " + OctopusSampleBranding.Theme,
            SampleUi.TextCaption, SampleUi.Muted);
        var choices = SampleUi.Panel(ThemeToggleId, card, OctopusSampleBranding.Clear);
        var layout = choices.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceSm);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.MiddleCenter;
        var dark = OctopusSampleBranding.Theme == OctopusSampleTheme.Dark;
        SampleUi.Button(LightId, choices, "Light",
            dark ? SampleUiButtonVariant.Tertiary : SampleUiButtonVariant.Secondary,
            () => OctopusSampleBranding.Theme = OctopusSampleTheme.Light);
        SampleUi.Switch(ShellThemeToggleId, choices, dark, true, ToggleTheme);
        SampleUi.Button(DarkId, choices, "Dark",
            dark ? SampleUiButtonVariant.Secondary : SampleUiButtonVariant.Tertiary,
            () => OctopusSampleBranding.Theme = OctopusSampleTheme.Dark);
    }
}
