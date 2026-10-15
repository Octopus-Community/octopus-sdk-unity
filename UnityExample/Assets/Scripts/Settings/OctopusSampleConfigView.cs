using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OctopusSampleConfigView : OctopusSampleSettingsDetailView
{
    public const string ScreenId = "config-screen";
    public const string BackId = "config-back";
    public const string DefaultId = "config-default-profile";
    public const string ForcedLoginId = "config-forced-login-profile";
    public const string StartId = "config-start-button";
    public const string ResultId = "config-result";
    public const string AccountLinkId = "config-account-link";
    public const string CancelId = "config-cancel-button";
    public const string ServerId = "config-section-server";
    public const string CommunityId = "config-section-community";
    public const string AuthId = "config-section-auth";
    public const string ThemeId = "config-section-theme";
    public const string IntegrationId = "config-section-integration";

    public OctopusSampleConfigurationDraft Draft { get; private set; }
    public int Selection { get { return Draft.Community; } }
    public string Result { get; private set; }
    public bool IsApplying { get; private set; }
    private readonly bool[] _expanded = new bool[4];
    private DateTime _startedAt;
    private int _applyToken;
    private Action _returnTo;
    private ScrollRect _scroll;
    private RectTransform _dock;
    private float _contentBottomInset;
    private float _scrollPosition = 1f;
    private bool _restoreScroll;

    public static OctopusSampleConfigView Open() { return Open(null); }

    // Back returns to the referring page; Apply keeps the result visible on this page.
    public static OctopusSampleConfigView Open(Action returnTo)
    {
        var existing = FindAnyObjectByType<OctopusSampleConfigView>();
        if (existing != null) return existing;
        var view = new GameObject("OctopusSampleConfigView").AddComponent<OctopusSampleConfigView>();
        view._returnTo = returnTo;
        view.Draft = OctopusScenarioSdk.ConfigurationDraft();
        view.Result = OctopusScenarioSdk.StartupFailureReason;
        if (string.IsNullOrWhiteSpace(OctopusScenarioSdk.ConfigurationKey(view.Draft))) view._expanded[1] = true;
        view.InitializeView();
        OctopusSampleQaLaunch.Log("screen=config");
        OctopusSampleQaLaunch.ConfigShown(view.Apply);
        return view;
    }

    public static int InitialSelection() { return OctopusScenarioSdk.ConfigurationDraft().Community; }

    public void ToggleSection(int section)
    {
        if (section < 0 || section >= _expanded.Length || IsApplying) return;
        _expanded[section] = !_expanded[section];
        Redraw();
    }

    public void SelectProfile(int selection)
    {
        if (IsApplying || selection < 1 || selection > 5) return;
        Draft.Community = selection;
        Result = null;
        Redraw();
    }

    public void Apply()
    {
        if (IsApplying) return;
        IsApplying = true;
        _startedAt = OctopusScenarioSdk.UtcNow();
        Result = "Applying configuration…";
        Redraw();
        OctopusScenarioSdk.ApplyConfiguration(Draft, (success, message) =>
        {
            if (this == null) return;
            IsApplying = false;
            Result = message;
            if (success)
            {
                Draft = OctopusScenarioSdk.ConfigurationDraft();
                OctopusSampleQaLaunch.ConfigStarted();
            }
            Redraw();
        });
        if (IsApplying) _applyToken = OctopusScenarioSdk.OperationToken;
    }

    protected override void LateUpdate()
    {
        CheckApplyTimeout();
        base.LateUpdate();
        if (_scroll != null && _dock != null)
        {
            var viewport = _scroll.viewport;
            var bottom = _contentBottomInset + _dock.rect.height;
            if (!Mathf.Approximately(viewport.offsetMin.y, bottom))
                viewport.offsetMin = new Vector2(viewport.offsetMin.x, bottom);
        }
        if (_restoreScroll && _scroll != null)
        {
            _restoreScroll = false;
            Canvas.ForceUpdateCanvases();
            _scroll.verticalNormalizedPosition = _scrollPosition;
        }
    }

    // Also callable by EditMode tests without dispatching a Unity player-loop message.
    public void CheckApplyTimeout()
    {
        if (IsApplying && OctopusScenarioSdk.UtcNow() - _startedAt >= OctopusScenarioSdk.OperationTimeout)
        {
            OctopusScenarioSdk.TimeoutConfigurationApply(_applyToken);
            IsApplying = false;
            Result = "No response yet. You can retry applying the configuration.";
            Redraw();
        }
    }

    private void Redraw()
    {
        if (_scroll != null) _scrollPosition = _scroll.verticalNormalizedPosition;
        _restoreScroll = true;
        Refresh();
    }

    public bool CanLeave { get { return !IsApplying; } }
    public override void Close() { if (CanLeave) Leave(); }
    protected override void Dismiss()
    {
        if (!CanLeave) return;
        _returnTo = null;
        base.Close();
    }

    public void OpenAccount()
    {
        if (!CanLeave) return;
        _returnTo = null;
        base.Close();
        OctopusSampleAccountView.Open();
    }

    private void Leave()
    {
        var returnTo = _returnTo;
        _returnTo = null;
        Draft.CustomApiKey = "";
        base.Close();
        if (returnTo != null) returnTo();
    }

    protected override void Build()
    {
        var page = SampleUi.DetailPage(ScreenId, transform, Dismiss);
        var header = SampleUi.AppBar("Header", page, "Configuration", Close, BackId, "Line");
        header.GetComponent<SampleUiBackTarget>().Back.interactable = CanLeave;
        SampleUiDebugEntryHost.Attach(header);
        // Actions stay outside the scroll; the result sizes itself for wrapped text.
        var dockHeight = OctopusSampleBranding.Dp(72f + (string.IsNullOrEmpty(Result) ? 0f : 72f) * OctopusSampleTextScale.Current);
        var insets = SampleUi.OverlayPadding();
        _contentBottomInset = insets.bottom;
        insets.bottom += Mathf.CeilToInt(dockHeight);
        var content = SampleUi.VerticalScroll(page, insets);
        _scroll = content.parent.GetComponent<ScrollRect>();

        var server = Section(content, 0, ServerId, "Server environment", null);
        if (server != null)
        {
            Text(server, "Keep the demo server unless a ticket says otherwise. Prod and custom hosts are for internal testing — never publish test content on a client community.");
            var choices = Group(server, "config-serverEnv-select");
            Choice(choices, "config-server-demo", !Draft.CustomServer, "Demo server (default)", () => { Draft.CustomServer = false; Redraw(); });
            Choice(choices, "config-server-custom", Draft.CustomServer, "Custom host", () => { Draft.CustomServer = true; Redraw(); });
            if (Draft.CustomServer)
                Field(server, "config-customHost-input", "Host", Draft.CustomHost, value => Draft.CustomHost = value);
        }

        var community = Section(content, 1, CommunityId, "Community", Draft.CommunityLabel);
        if (community != null)
        {
            Text(community, "Pick the community to open. The preselected one fits most tests.");
            var choices = Group(community, "config-apiKeySource-select");
            for (var i = 1; i <= 5; i++)
            {
                var selection = i;
                var id = i == 1 ? DefaultId : i == 2 ? ForcedLoginId : "config-community-" + i;
                var probe = Draft.Copy(); probe.Community = i;
                bool available = i == 5 || !string.IsNullOrWhiteSpace(OctopusScenarioSdk.ConfigurationKey(probe));
                Choice(choices, id, Selection == i, OctopusSampleConfigurationDraft.CommunityLabels[i - 1] +
                    (available ? "" : " · Demo key not configured"), () => SelectProfile(selection));
                choices.Find(id).GetComponent<Button>().interactable = !IsApplying && available;
            }
            if (Selection == 5)
            {
                // Never construct a label with the raw key, including during its first frame.
                var input = SampleUi.LabeledField("config-customApiKey-input", community, "API Key", "");
                input.contentType = TMP_InputField.ContentType.Password;
                input.SetTextWithoutNotify(Draft.CustomApiKey);
                input.ForceLabelUpdate();
                input.interactable = !IsApplying;
                input.onValueChanged.AddListener(value => Draft.CustomApiKey = value);
            }
            Text(community, "Resolved key: " + OctopusSampleConfigurationDraft.MaskApiKey(OctopusScenarioSdk.ConfigurationKey(Draft)));
        }

        var auth = Section(content, 2, AuthId, "Connect your users (SSO)", null);
        if (auth != null)
        {
            var users = Group(auth, "config-sso-user-select");
            foreach (var id in OctopusSampleConfigurationDraft.DemoUsers) UserChoice(users, id);
            if (!string.IsNullOrEmpty(Draft.UserId) && Array.IndexOf(OctopusSampleConfigurationDraft.DemoUsers, Draft.UserId) < 0)
                UserChoice(users, Draft.UserId);
            Text(auth, "These settings pick the matching demo community above.");
            Text(auth, "Force login — " + (Draft.ForceLogin ? "on" : "off") + " · set in Scenarios");
            Text(auth, OctopusSampleFeatureToggles.ForceLoginEffect(Draft.ForceLogin));
            SampleUi.Button(AccountLinkId, auth, "Open Account", SampleUiButtonVariant.Tertiary, OpenAccount).GetComponent<Button>().interactable = CanLeave;
        }

        var theme = Section(content, 3, ThemeId, "Theme", null);
        if (theme != null)
        {
            var appearances = Group(theme, "config-appearance-select");
            for (var i = 0; i <= 2; i++)
            {
                var appearance = i;
                Choice(appearances, "config-appearance-" + i, (Draft.Appearance ?? OctopusSampleNativeTheme.SchemeTypeFor(OctopusSampleBranding.Theme)) == i,
                    i == 0 ? "System" : i == 1 ? "Light" : "Dark", () => { Draft.Appearance = appearance; Redraw(); });
            }
            var presets = Group(theme, "config-theme-preset-select");
            Choice(presets, "config-theme-navy", Draft.NavyTheme, "Octopus navy", () => { Draft.NavyTheme = true; Redraw(); });
            Choice(presets, "config-theme-default", !Draft.NavyTheme, "SDK default", () => { Draft.NavyTheme = false; Redraw(); });
            Text(theme, "\"SDK default\" hands the SDK no theme — what a fresh integration looks like.");
            if (Draft.Appearance == 0) Text(theme, "System follows the device appearance in the sample and native community.");
        }

        SampleUi.ListRow(IntegrationId, content, "Host app integration",
            "Profile, sign-in, links: now in Settings › Developer tools", OpenHostIntegration);

        var dock = SampleUi.Panel("config-actions", page, SampleUi.RowBackground);
        _dock = dock;
        SampleUi.Stretch(dock, Vector2.zero, new Vector2(1f, 0f));
        dock.pivot = new Vector2(0.5f, 0f);
        dock.sizeDelta = new Vector2(0f, dockHeight);
        SampleUi.VerticalStack(dock, OctopusSampleBranding.Dp(4f), SampleUi.Padding(16f, 8f), true);
        if (!string.IsNullOrEmpty(Result))
        {
            var result = SampleUi.FlexibleLabel(dock, Result, SampleUi.TextBody, SampleUi.Attention);
            result.gameObject.name = ResultId;
            result.richText = false;
        }
        var actions = SampleUi.Panel("Actions", dock, OctopusSampleBranding.Clear);
        var layout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = OctopusSampleBranding.Dp(8f);
        SampleUi.Button(CancelId, actions, "Cancel", SampleUiButtonVariant.Tertiary, Close).GetComponent<Button>().interactable = CanLeave;
        SampleUi.Button(StartId, actions, IsApplying ? "Applying…" : "Apply", SampleUiButtonVariant.Primary, Apply).GetComponent<Button>().interactable = !IsApplying;
    }

    public void OpenHostIntegration()
    {
        if (IsApplying) return;
        // Keep the unsaved draft and its scroll position underneath the child route.
        OctopusSampleHostIntegrationView.Open();
    }

    private RectTransform Section(RectTransform content, int index, string id, string title, string summary)
    {
        var card = SampleUi.Card(id + "-card", content);
        SampleUi.Button(id, card, title + (_expanded[index] ? "  −" : "  +"), SampleUiButtonVariant.Tertiary,
            () => ToggleSection(index)).GetComponent<Button>().interactable = !IsApplying;
        if (!string.IsNullOrEmpty(summary)) Text(card, summary);
        return _expanded[index] ? card : null;
    }

    private static RectTransform Group(Transform parent, string id)
    {
        var group = SampleUi.Panel(id, parent, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(group, OctopusSampleBranding.Dp(4f), new RectOffset(), false);
        return group;
    }

    private void UserChoice(Transform parent, string id)
    {
        Choice(parent, "config-sso-user-" + id, Draft.UserId == id, id, () => { Draft.UserId = id; Draft.UserSelected = true; Redraw(); });
    }

    private void Choice(Transform parent, string id, bool selected, string title, UnityEngine.Events.UnityAction action)
    {
        SampleUi.Button(id, parent, (selected ? "Selected · " : "") + title,
            selected ? SampleUiButtonVariant.Secondary : SampleUiButtonVariant.Tertiary, action).GetComponent<Button>().interactable = !IsApplying;
    }

    private void Field(Transform parent, string id, string title, string value, Action<string> changed)
    {
        var field = SampleUi.LabeledField(id, parent, title, value);
        field.interactable = !IsApplying;
        field.onValueChanged.AddListener(text => changed(text));
    }

    private static void Text(Transform parent, string text)
    {
        SampleUi.FlexibleLabel(parent, text, SampleUi.TextCaption, SampleUi.Muted).richText = false;
    }
}
