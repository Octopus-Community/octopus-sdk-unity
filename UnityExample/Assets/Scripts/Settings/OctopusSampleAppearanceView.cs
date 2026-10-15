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
        SampleUiDebugEntryHost.ReleaseFor(gameObject);
        gameObject.SetActive(false);
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
        SampleUiDebugEntryHost.ReleaseFor(gameObject);
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
        var root = SampleUi.DetailPage(ScreenId, transform, Close);
        var header = SampleUi.AppBar("Header", root, "Appearance", Close, BackId, "Line");
        SampleUiDebugEntryHost.Attach(header);
        var content = SampleUi.VerticalScroll(root, SampleUi.OverlayPadding());
        var card = SampleUi.Card("settings-appearance-card", content);
        SampleUi.FlexibleLabel(card, "Appearance", SampleUi.TextTitle, SampleUi.TitleColor);
        SampleUi.FlexibleLabel(card, "Applies to the sample and to the SDK surface it hosts.", SampleUi.TextCaption, SampleUi.Muted);
        var choices = SampleUi.Panel(ThemeToggleId, card, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(choices, OctopusSampleBranding.Dp(4f), new RectOffset(), false);
        var labels = new[] { "System", "Light", "Dark" };
        var ids = new[] { "settings-appearance-system", LightId, DarkId };
        for (var i = 0; i < labels.Length; i++)
        {
            var appearance = i;
            SampleUi.Button(ids[i], choices, labels[i],
                OctopusSampleNativeTheme.CurrentAppearance == i ? SampleUiButtonVariant.Secondary : SampleUiButtonVariant.Tertiary,
                () => OctopusScenarioSdk.SetAppearance(OctopusSampleNativeTheme.NavyTheme, appearance));
        }
        var preset = SampleUi.Card("appearance-theme-preset", content);
        SampleUi.FlexibleLabel(preset, "Theme preset", SampleUi.TextTitle, SampleUi.TitleColor);
        SampleUi.FlexibleLabel(preset, "SDK default restores the native SDK colours — what a fresh integration looks like.", SampleUi.TextCaption, SampleUi.Muted);
        SampleUi.Button("appearance-theme-navy", preset, "Octopus navy",
            OctopusSampleNativeTheme.NavyTheme ? SampleUiButtonVariant.Secondary : SampleUiButtonVariant.Tertiary,
            () => OctopusScenarioSdk.SetAppearance(true, OctopusSampleNativeTheme.CurrentAppearance));
        SampleUi.Button("appearance-theme-default", preset, "SDK default",
            !OctopusSampleNativeTheme.NavyTheme ? SampleUiButtonVariant.Secondary : SampleUiButtonVariant.Tertiary,
            () => OctopusScenarioSdk.SetAppearance(false, OctopusSampleNativeTheme.CurrentAppearance));
        SampleUi.FlexibleLabel(content, "Changing the preset re-themes the native community immediately — no restart.", SampleUi.TextCaption, SampleUi.Muted);
    }
}
