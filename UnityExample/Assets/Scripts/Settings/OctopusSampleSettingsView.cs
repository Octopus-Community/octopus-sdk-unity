using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Settings is the index; SDK state below is labelled as the sample's record of calls.
public class OctopusSampleSettingsView : MonoBehaviour
{
    public const string ScreenId = "settings-screen";
    public const string IntroCardId = "settings-intro-card";
    public const string SupportCardId = "settings-support-card";
    public const string SupportTitleId = "settings-support-title";
    public const string AboutRowId = "settings-about-row";
    public const string DevToolsRowId = "settings-devtools-row";
    public const string SdkTitleId = "settings-sdk-title";
    public const string DemoGroupId = "settings-demo-group";
    public const string ResetTitleId = "settings-reset-title";
    public const string ResetCardId = "settings-reset-card";
    public const string ResetStateSectionId = "settings-reset-state";
    public const string ResetButtonId = "settings-reset-button";
    public const string ResetConfirmId = "settings-reset-confirm";
    public const string ResetCancelId = "settings-reset-cancel";
    public const string DemoGameRowId = "settings-demo-game-row";
    public const string AccountRowId = "settings-account-row";
    public const string ConfigRowId = "settings-reconfigure-button";
    public const string ResetConfigCardId = "settings-reset-config-card";
    public const string ResetConfigButtonId = "settings-reset-config-button";
    public const string ResetConfigConfirmId = "settings-reset-config-confirm";
    public const string ResetConfigCancelId = "settings-reset-config-cancel";
    public const string ResetConfigResultId = "settings-reset-config-result";
    public const string ResetResultId = "settings-reset-result";
    public const string VersionLabelId = "settings-version-label";
    public const string AppearanceRowId = "settings-appearance-row";
    public const string LanguageRowId = "settings-language-row";
    public const string LanguageLinkId = "settings-language-locale-link";
    public const string LocaleScenarioId = "locale";
    private bool _listening, _confirming, _resetting;
    private IOctopusScenarioSdk _observedSdk;
    private string _resetResult = "";
    private RectTransform _supportCard;
    private TMP_Text _appearance, _language, _status, _community, _server;
    private GameObject _dialog;
    public int RowsEventCount { get; private set; }
    public bool IsConfirmingReset { get { return _confirming; } }
    public string ResetResult { get { return _resetResult; } }
    public sealed class ViewState
    {
        public bool ConfirmingReset, ConfirmingConfigReset;
        public string ResetResult, ConfigResetResult;
    }
    public ViewState CaptureState() { return new ViewState { ResetResult = _resetResult }; }
    public void RestoreState(ViewState state) { if (state != null) _resetResult = state.ResetResult; }
    public static string AppearanceText()
    {
        var type = OctopusSampleNativeTheme.CurrentAppearance;
        return (OctopusSampleNativeTheme.NavyTheme ? "Octopus navy" : "SDK default") + " · " +
            (type == 0 ? "Follow system" : type == 1 ? "Light" : "Dark");
    }
    public static string LanguageText() { return OctopusSampleLanguage.Label(OctopusSampleState.LocaleOverride); }
    public static string VersionLabelText()
    {
        return "Sample " + OctopusSampleAboutView.SampleVersionText() + " (" + OctopusSampleAboutView.BuildText() +
            ") · SDK " + OctopusSampleAboutView.SdkVersionText();
    }
    /// <summary>
    /// The About row's subtitle; it leads with the update when Play offers one, as the Android
    /// sample's Settings does, so a tester sees it without opening About.
    /// </summary>
    public static string AboutSubtitle()
    {
        var versions = "Sample " + OctopusSampleAboutView.SampleVersionText() + " · SDK " + OctopusSDK.Version;
        return OctopusSampleAppUpdate.Status.State == OctopusSampleUpdateState.UpdateAvailable
            ? "Update available · " + versions : versions;
    }
    public static OctopusSampleSettingsView BuildInto(RectTransform host)
    {
        var view = host.gameObject.AddComponent<OctopusSampleSettingsView>();
        // Rows are full-bleed like Android's: the scroll has no side inset, and only the community
        // card carries the 16dp page margin.
        var content = SampleUi.VerticalScroll(host, new RectOffset());
        content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing = 0f;
        view.Build(content);
        view.Listen();
        return view;
    }
    public IList<string> SupportRowIds
    {
        get
        {
            var ids = new List<string>();
            if (_supportCard == null) return ids;
            for (var i = 0; i < _supportCard.childCount; i++)
            {
                var name = _supportCard.GetChild(i).name;
                if (name != SupportTitleId && name != SampleUi.StrokeName && name != SampleUi.DividerName)
                    ids.Add(name);
            }
            return ids;
        }
    }
    public static string StatusText()
    {
        return OctopusSampleState.IsInitialized
            ? "SDK · Initialized (sample record) · " + OctopusSampleHomeView.ConnectionStatusLabel()
            : "SDK · Not initialized (sample record)";
    }
    private void Build(RectTransform content)
    {
        content.name = ScreenId;
        int margin = Mathf.RoundToInt(OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceLg));
        var summaryHolder = SampleUi.Panel("settings-community-holder", content, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(summaryHolder, 0f, new RectOffset(margin, margin, margin,
            Mathf.RoundToInt(OctopusSampleBranding.Dp(24f))), false);
        var summary = SampleUi.Card("settings-community-block", summaryHolder);
        SampleUi.FlexibleLabel(summary, "Community", SampleUi.TextTitle, SampleUi.TitleColor);
        var draft = OctopusScenarioSdk.ConfigurationDraft();
        _community = Fact(summary, "Community", draft.Community == 5 ? "Custom community" : "Demo community");
        _server = Fact(summary, "Server", draft.CustomServer ? "Custom" : "Demo");
        _status = SampleUi.FlexibleLabel(summary, StatusText(), SampleUi.TextCaption, SampleUi.Muted);
        _status.name = "settings-sdk-status";
        SampleUi.FlexibleLabel(summary, "Initialization and connection are recorded calls; native session state is not exposed.", SampleUi.TextCaption, SampleUi.Muted);
        Row(AccountRowId, content, "Account", OctopusSampleAccountView.ConnectionText(), () => OctopusSampleAccountView.Open());
        var sdk = Group(IntroCardId, content, SdkTitleId, "SDK");
        var appearance = Row(AppearanceRowId, sdk, "Appearance", AppearanceText(), () => OctopusSampleAppearanceView.Open());
        _appearance = appearance.Find("Copy").GetChild(1).GetComponent<TMP_Text>();
        _appearance.name = "settings-appearance-value";
        SampleUi.SummaryDivider(sdk);
        var language = Row(LanguageRowId, sdk, "Language", LanguageText(), () => OctopusSampleLanguageView.Open());
        _language = language.Find("Copy").GetChild(1).GetComponent<TMP_Text>();
        _language.name = "settings-language-value";
        SampleUi.SummaryDivider(sdk);
        Row(ConfigRowId, sdk, "Configuration", "Community, server, SSO user, theme", () => OctopusSampleConfigView.Open());
        _supportCard = Group(SupportCardId, content, SupportTitleId, "Support");
        RebuildSupport();
        // Unity-specific game remains reachable during the migration of legacy examples.
        var game = Group(DemoGroupId, content, "settings-demo-title", "Demo");
        Row(DemoGameRowId, game, "Demo game", "Play Reef Run · share a score through the SDK", () => OctopusReefRunView.Open());
        var reset = Group(ResetCardId, content, ResetTitleId, "Reset");
        SampleUi.SummaryRow(ResetButtonId, reset, SampleUiIcons.ForRow(ResetButtonId), "Reset Configuration",
            "Local demo setup only — nothing is deleted on the server", RequestReset, true);
        var versionHolder = SampleUi.Panel("settings-version-holder", content, OctopusSampleBranding.Clear);
        int gap = Mathf.RoundToInt(OctopusSampleBranding.Dp(24f));
        SampleUi.VerticalStack(versionHolder, 0f, new RectOffset(margin, margin, gap, gap), false);
        var version = SampleUi.FlexibleLabel(versionHolder, VersionLabelText(), SampleUi.TextCaption, SampleUi.Muted);
        version.name = VersionLabelId;
        version.alignment = TextAlignmentOptions.Top;
    }
    /// <summary>A flat section: Android's group header over full-width rows, no card.</summary>
    private static RectTransform Group(string id, RectTransform content, string titleId, string title)
    {
        var group = SampleUi.Panel(id, content, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(group, 0f, new RectOffset(), false);
        SampleUi.SummaryGroupHeader(titleId, group, title);
        return group;
    }
    private static RectTransform Row(string id, Transform parent, string title, string subtitle,
                                     UnityEngine.Events.UnityAction action)
    {
        return SampleUi.SummaryRow(id, parent, SampleUiIcons.ForRow(id), title, subtitle, action);
    }
    private static TMP_Text Fact(Transform parent, string label, string value)
    {
        var text = SampleUi.FlexibleLabel(parent, label + " · " + value, SampleUi.TextCaption, SampleUi.TitleColor);
        text.richText = false;
        return text;
    }
    private void RebuildSupport()
    {
        if (_supportCard == null) return;
        for (var i = _supportCard.childCount - 1; i >= 0; i--)
        {
            var child = _supportCard.GetChild(i).gameObject;
            if (child.name == SupportTitleId || child.name == SampleUi.StrokeName) continue;
            child.SetActive(false); child.transform.SetParent(null, false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        bool tools = false;
        foreach (var row in OctopusSampleSettingsRows.Support) if (row.Id == DevToolsRowId) tools = true;
        if (!tools) Row(DevToolsRowId, _supportCard, "Developer tools",
            "Host app integration · Events log · Debug info · Debug console", () => OctopusSampleDeveloperToolsView.Open());
        foreach (var row in OctopusSampleSettingsRows.Support)
        {
            var action = row.Action;
            if (_supportCard.childCount > 1) SampleUi.SummaryDivider(_supportCard);
            Row(row.Id, _supportCard, row.Title, row.Subtitle, action == null ? null : new UnityEngine.Events.UnityAction(action));
        }
        SampleUi.SummaryDivider(_supportCard);
        Row(AboutRowId, _supportCard, "About", AboutSubtitle(), () => OctopusSampleAboutView.Open());
    }
    public void RequestReset()
    {
        if (_resetting) return;
        _resetResult = ""; _confirming = true;
        if (_dialog != null) return;
        _dialog = OctopusSampleResetDialog.Open(CancelReset, ConfirmReset);
    }
    public void CancelReset() { _confirming = false; CloseDialog(); }
    public void ConfirmReset()
    {
        if (!_confirming || _resetting) return;
        _confirming = false; _resetting = true; CloseDialog();
        OctopusScenarioSdk.ResetLocalSetup((ok, message) =>
        {
            // Reset refreshes the theme and can rebuild the shell before this callback. A Stop
            // can also complete after a tab change: navigation belongs to the operation.
            if (this != null) { _resetting = false; _resetResult = message; }
            if (ok) OctopusSampleConfigView.Open();
            else OctopusSampleHostCallbackView.Open("Reset Configuration", message);
        });
    }
    // Preserve callable names used by existing QA and the configuration tests.
    public void RequestConfigurationReset() { RequestReset(); }
    public void CancelConfigurationReset() { CancelReset(); }
    public void ConfirmConfigurationReset() { ConfirmReset(); }
    public void OpenLocaleScenario() { OctopusSampleLanguageView.Open(); }
    private void CloseDialog()
    {
        if (_dialog == null) return;
        _dialog.SetActive(false);
        if (Application.isPlaying) Destroy(_dialog); else DestroyImmediate(_dialog);
        _dialog = null;
    }
    public void SetTabActive(bool active) { if (active) { Listen(); OnStateChanged(); RebuildSupport(); } else { StopListening(); CloseDialog(); _confirming = false; } }
    private void Listen()
    {
        if (_listening) return;
        _listening = true;
        OctopusSampleSettingsRows.Changed += OnRowsChanged;
        OctopusSampleState.Changed += OnStateChanged;
        OctopusSampleBranding.ThemeChanged += OnStateChanged;
        OctopusSampleAppUpdate.Changed += OnUpdateChanged;
        _observedSdk = OctopusScenarioSdk.Current;
        _observedSdk.ProfileChanged += OnProfileChanged;
    }
    private void StopListening()
    {
        if (!_listening) return;
        _listening = false;
        OctopusSampleSettingsRows.Changed -= OnRowsChanged;
        OctopusSampleState.Changed -= OnStateChanged;
        OctopusSampleBranding.ThemeChanged -= OnStateChanged;
        OctopusSampleAppUpdate.Changed -= OnUpdateChanged;
        if (_observedSdk != null) _observedSdk.ProfileChanged -= OnProfileChanged;
    }
    private void OnProfileChanged(OctopusProfile profile) { OnStateChanged(); }
    private void OnUpdateChanged() { if (this == null) { StopListening(); return; } RebuildSupport(); }
    private void OnRowsChanged() { RowsEventCount++; if (this == null) { StopListening(); return; } RebuildSupport(); }
    private void OnStateChanged()
    {
        if (this == null) { StopListening(); return; }
        if (_appearance != null) _appearance.text = AppearanceText();
        if (_language != null) _language.text = LanguageText();
        if (_status != null) _status.text = StatusText();
        var draft = OctopusScenarioSdk.ConfigurationDraft();
        if (_community != null) _community.text = "Community · " + (draft.Community == 5 ? "Custom community" : "Demo community");
        if (_server != null) _server.text = "Server · " + (draft.CustomServer ? "Custom" : "Demo");
        var row = transform.Find(AccountRowId);
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == AccountRowId) row = t;
        if (row != null) row.Find("Copy").GetChild(1).GetComponent<TMP_Text>().text = OctopusSampleAccountView.ConnectionText();
    }
    private void OnDisable() { StopListening(); CloseDialog(); }
    private void OnDestroy() { StopListening(); CloseDialog(); }
}
