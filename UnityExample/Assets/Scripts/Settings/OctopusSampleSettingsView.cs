using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings is an index of account, configuration, preferences, support and local reset.
/// Reset sample state clears observations, not SDK configuration;
/// legacy scenes stay reachable until their scenarios migrate.
/// </summary>
public class OctopusSampleSettingsView : MonoBehaviour
{
    /// <summary>The tab's root container, and the id a QA script waits on.</summary>
    public const string ScreenId = "settings-screen";

    /// <summary>The card explaining where the SDK read-out lives.</summary>
    public const string IntroCardId = "settings-intro-card";

    /// <summary>The Support group.</summary>
    public const string SupportCardId = "settings-support-card";

    /// <summary>
    /// The Support group's own heading. Named rather than left anonymous because
    /// <see cref="RebuildSupport"/> clears the card's children around it and
    /// <see cref="SupportRowIds"/> skips it — both need one name to test against.
    /// </summary>
    public const string SupportTitleId = "settings-support-title";

    /// <summary>The built-in row opening <see cref="OctopusSampleAboutView"/>.</summary>
    public const string AboutRowId = "settings-about-row";

    /// <summary>
    /// The Reset group: one card holding both resets — the sample's recorded state
    /// (<see cref="ResetStateSectionId"/>) and the saved configuration
    /// (<see cref="ResetConfigCardId"/>). Two cards both headed "Reset" read as one action drawn
    /// twice.
    /// </summary>
    public const string ResetCardId = "settings-reset-card";

    /// <summary>The recorded-state half of the Reset card.</summary>
    public const string ResetStateSectionId = "settings-reset-state";

    /// <summary>The control that asks for a reset. The catalogue's id, kept across the rename.</summary>
    public const string ResetButtonId = "settings-reset-button";

    /// <summary>The control that carries the reset out, shown only while confirming.</summary>
    public const string ResetConfirmId = "settings-reset-confirm";

    /// <summary>The control that backs out of a reset.</summary>
    public const string ResetCancelId = "settings-reset-cancel";
    public const string AccountRowId = "settings-account-row";
    public const string ConfigRowId = "settings-reconfigure-button";
    /// <summary>
    /// The configuration half of the Reset card. A section inside <see cref="ResetCardId"/> since
    /// the two cards merged; the name is kept so a QA step that scoped itself to it still resolves.
    /// </summary>
    public const string ResetConfigCardId = "settings-reset-config-card";
    public const string ResetConfigButtonId = "settings-reset-config-button";
    public const string ResetConfigConfirmId = "settings-reset-config-confirm";
    public const string ResetConfigCancelId = "settings-reset-config-cancel";
    public const string ResetConfigResultId = "settings-reset-config-result";

    /// <summary>The footer stating what this build is.</summary>
    public const string VersionLabelId = "settings-version-label";

    /// <summary>
    /// Clearance for the shell's bottom-anchored `Legacy demo scenes` button: its own 40 units of
    /// margin, its height, and 24 of air above it. Without this the last card scrolls underneath a
    /// button that looks like part of the list.
    ///
    /// Derived rather than written as 208, so raising the touch-target floor moves this with it.
    /// </summary>
    private static int LegacyRouteClearance
    {
        get { return 40 + (int)OctopusSampleBranding.MinTouchUnits + 24; }
    }

    private RectTransform _content;
    private RectTransform _supportCard;
    private RectTransform _resetState;
    private RectTransform _resetConfigCard;
    private bool _confirmingConfigReset;
    private string _configResetResult;
    private bool _confirmingReset;
    private string _resetResult = string.Empty;
    private TMP_Text _language;
    private TMP_Text _appearance;

    public const string AppearanceRowId = "settings-appearance-row";
    public const string LanguageRowId = "settings-language-row";

    /// <summary>The Language row's link to the Locale scenario, the one place the override is set.</summary>
    public const string LanguageLinkId = "settings-language-locale-link";

    /// <summary>The catalogue id of the scenario that sets the language override.</summary>
    public const string LocaleScenarioId = "locale";
    public const string ResetResultId = "settings-reset-result";
    public string ResetResult { get { return _resetResult; } }

    public static string AppearanceText()
    {
        return OctopusSampleBranding.Theme.ToString();
    }

    public static string LanguageText()
    {
        return string.IsNullOrEmpty(OctopusSampleState.LocaleOverride)
            ? "System default (no override)" : OctopusSampleState.LocaleOverride;
    }

    /// <summary>
    /// Fills <paramref name="host"/> — the shell's content area — with the tab.
    ///
    /// The shell keeps drawing its `Legacy demo scenes` route into the same host afterwards; this
    /// view leaves room for it rather than knowing about it.
    /// </summary>
    public static OctopusSampleSettingsView BuildInto(RectTransform host)
    {
        var view = host.gameObject.AddComponent<OctopusSampleSettingsView>();
        var padding = SampleUi.ContentPadding();
        padding.bottom = LegacyRouteClearance;
        view.Build(SampleUi.VerticalScroll(host, padding));
        return view;
    }

    /// <summary>
    /// The ids of the Support rows currently on screen, in order.
    ///
    /// Test-only reader. The EditMode assembly cannot name a `UnityEngine.UI` type, so a test has no
    /// way to walk buttons; this is how it checks that a registered row appears and that `About`
    /// stays last.
    /// </summary>
    public IList<string> SupportRowIds
    {
        get
        {
            var ids = new List<string>();
            if (_supportCard == null) return ids;
            for (var i = 0; i < _supportCard.childCount; i++)
            {
                var child = _supportCard.GetChild(i);
                if (child.name == SupportTitleId || child.name == SampleUi.StrokeName) continue;
                ids.Add(child.name);
            }
            return ids;
        }
    }

    /// <summary>Whether the reset row is showing its confirm/cancel pair rather than its trigger.</summary>
    public bool IsConfirmingReset { get { return _confirmingReset; } }

    /// <summary>
    /// The footer's exact wording. Pure and static so a test can pin it without a UI type.
    /// <para>
    /// It names the SDK since #116 gave the package a runtime version constant. Before that the
    /// footer stated only what it could actually read, because a number typed into the sample goes
    /// stale on the next package bump with nothing to catch it — which is now the guard's job, not
    /// this line's.
    /// </para>
    /// </summary>
    public static string VersionLabelText()
    {
        return "Sample " + OctopusSampleAboutView.SampleVersionText() +
               " · SDK " + OctopusSampleAboutView.SdkVersionText() +
               " · Unity " + OctopusSampleAboutView.UnityVersionText();
    }

    /// <summary>Arms the confirm/cancel pair. Wired to <see cref="ResetButtonId"/>.</summary>
    public void RequestReset()
    {
        _resetResult = string.Empty;
        _confirmingReset = true;
        RepaintReset();
    }

    /// <summary>Backs out, leaving the state untouched. Wired to <see cref="ResetCancelId"/>.</summary>
    public void CancelReset()
    {
        _resetResult = "Reset cancelled. Recorded results are unchanged.";
        _confirmingReset = false;
        RepaintReset();
    }

    /// <summary>
    /// Clears the sample's record of what the SDK reported, and disarms. Wired to
    /// <see cref="ResetConfirmId"/>.
    ///
    /// <see cref="OctopusSampleState.ResetObservations"/> rather than
    /// <see cref="OctopusSampleState.Reset"/>, and the difference matters: `Reset` also clears the
    /// initialisation flag, which <see cref="OctopusScenarioSdk.EnsureInitialized"/> reads as the
    /// guard against initialising an already-initialised SDK. Wiring `Reset` to a button would make
    /// "open Community, reset, open Community" call `Initialize` twice, which the native side
    /// answers with a second channel and a second set of collectors. A reset that quietly changes
    /// the SDK's lifecycle is not a reset of the sample's state. Found in review.
    ///
    /// So this makes no SDK call at all, and it undoes nothing the SDK is still configured with —
    /// not the initialisation, not a locale override.
    /// </summary>
    public void ConfirmReset()
    {
        if (!_confirmingReset) return;
        OctopusSampleState.ResetObservations();
        _resetResult = "Sample state cleared. No server data was deleted.";
        _confirmingReset = false;
        RepaintReset();
    }

    public void RequestConfigurationReset()
    {
        _confirmingConfigReset = true;
        _configResetResult = null;
        RepaintConfigurationReset();
    }

    public void CancelConfigurationReset()
    {
        _confirmingConfigReset = false;
        RepaintConfigurationReset();
    }

    public void ConfirmConfigurationReset()
    {
        if (!_confirmingConfigReset) return;
        var cleared = OctopusScenarioSdk.ResetConfiguration(out _configResetResult);
        _confirmingConfigReset = false;
        RepaintConfigurationReset();
        if (cleared) OctopusSampleConfigView.Open();
    }

    private void OnDestroy()
    {
        OctopusSampleSettingsRows.Changed -= OnRowsChanged;
        OctopusSampleState.Changed -= OnStateChanged;
        OctopusSampleBranding.ThemeChanged -= OnStateChanged;
    }

    // A destroyed view whose handler is still on the static event: unhook it from inside the
    // callback, the one place that still runs. Edit mode delivers neither OnDisable nor OnDestroy to
    // a plain MonoBehaviour, so this is not belt-and-braces there — it is the only path. Home and
    // the Community tab carry the same guard, for the same reason.
    private bool Gone()
    {
        if (this != null) return false;
        OctopusSampleSettingsRows.Changed -= OnRowsChanged;
        OctopusSampleState.Changed -= OnStateChanged;
        OctopusSampleBranding.ThemeChanged -= OnStateChanged;
        return true;
    }

    /// <summary>
    /// How many times the row registry's event has reached this view. Test-only observable, and the
    /// only one there is: a handler left on a destroyed view returns quietly, so without a count a
    /// test cannot tell "unsubscribed" from "still subscribed and silent".
    /// </summary>
    public int RowsEventCount { get; private set; }

    private void OnRowsChanged()
    {
        RowsEventCount++;
        if (Gone()) return;
        RebuildSupport();
    }

    private void Build(RectTransform content)
    {
        _content = content;
        _content.gameObject.name = ScreenId;

        _content.GetComponent<VerticalLayoutGroup>().spacing = OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceLg);
        var intro = SampleUi.Card(IntroCardId, _content);
        SampleUi.FlexibleLabel(intro, "Sample", SampleUi.TextBody, SampleUi.TitleColor);
        SampleUi.ListRow(AccountRowId, intro, "Account", "SSO sign-in and connection state",
            () => OctopusSampleAccountView.Open());
        SampleUi.ListRow(ConfigRowId, intro, "Change configuration", "Choose a configured sample profile",
            () => OctopusSampleConfigView.Open());
        var appearance = SampleUi.ListRow(AppearanceRowId, intro, "Appearance", AppearanceText(),
            () => OctopusSampleAppearanceView.Open());
        // ListRow's Copy contains the title followed by its current-value line.
        _appearance = appearance.Find("Copy").GetChild(1).GetComponent<TMP_Text>();
        _appearance.gameObject.name = "settings-appearance-value";
        var language = SampleUi.Panel(LanguageRowId, intro, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(language, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceXs), SampleUi.ContentPadding(), false);
        SampleUiIcons.Row(language, "language");
        SampleUi.FlexibleLabel(language, "Language", SampleUi.TextBody, SampleUi.TitleColor);
        _language = SampleUi.FlexibleLabel(language, LanguageText(), SampleUi.TextCaption, SampleUi.Muted);
        _language.gameObject.name = "settings-language-value";
        SampleUi.FlexibleLabel(language, "Read-only here · set through the Locale scenario.", SampleUi.TextCaption, SampleUi.Muted);
        SampleUi.Button(LanguageLinkId, language, "Open Locale scenario", SampleUiButtonVariant.Tertiary,
            OpenLocaleScenario);

        _supportCard = SampleUi.Card(SupportCardId, _content);
        SampleUi.FlexibleLabel(_supportCard, "Support", SampleUi.TextTitle, SampleUi.TitleColor)
                .gameObject.name = SupportTitleId;
        RebuildSupport();

        BuildResetCard();

        var version = SampleUi.FlexibleLabel(_content, VersionLabelText(), SampleUi.TextCaption,
                                             SampleUi.Muted);
        version.gameObject.name = VersionLabelId;

        // Subscribed from the build path rather than OnEnable: edit mode never delivers OnEnable to
        // a plain MonoBehaviour, and the EditMode tests are the only gate this screen has.
        OctopusSampleSettingsRows.Changed += OnRowsChanged;
        OctopusSampleState.Changed += OnStateChanged;
        OctopusSampleBranding.ThemeChanged += OnStateChanged;
    }

    private void RebuildSupport()
    {
        var card = _supportCard;
        if (card == null) return;

        for (var i = card.childCount - 1; i >= 0; i--)
        {
            var child = card.GetChild(i).gameObject;
            if (child.name == SupportTitleId || child.name == SampleUi.StrokeName) continue;
            child.SetActive(false);
            child.transform.SetParent(null, false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }

        foreach (var row in OctopusSampleSettingsRows.Support)
        {
            var action = row.Action;
            BuildRow(card, row.Id, row.Title, row.Subtitle,
                     action == null ? null : new UnityEngine.Events.UnityAction(action));
        }

        // Built in, and last: About is the one row that exists whatever else is compiled in.
        BuildRow(card, AboutRowId, "About",
                 "Versions, licences and links",
                 () => OctopusSampleAboutView.Open());
    }

    private static void BuildRow(RectTransform card, string id, string title, string subtitle,
                                 UnityEngine.Events.UnityAction action)
    {
        if (action != null)
        {
            SampleUi.ListRow(id, card, title, subtitle, action);
            return;
        }
        var row = SampleUi.Panel(id, card, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(row, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceXs), SampleUi.ContentPadding(), false);
        SampleUiIcons.Row(row, SampleUiIcons.ForRow(id));
        SampleUi.FlexibleLabel(row, title, SampleUi.TextBody, SampleUi.TitleColor);
        if (!string.IsNullOrEmpty(subtitle))
            SampleUi.FlexibleLabel(row, subtitle, SampleUi.TextCaption, SampleUi.Muted);
    }

    private void OnStateChanged()
    {
        if (Gone()) return;
        if (_language != null) _language.text = LanguageText();
        if (_appearance != null) _appearance.text = AppearanceText();
    }

    /// <summary>Opens the Locale scenario over the Scenarios tab. Wired to <see cref="LanguageLinkId"/>.</summary>
    public void OpenLocaleScenario()
    {
        var shell = GetComponentInParent<OctopusSampleShell>();
        if (shell != null) shell.OpenScenarioScreen(LocaleScenarioId);
    }

    private void BuildResetCard()
    {
        var card = SampleUi.Card(ResetCardId, _content);
        SampleUi.FlexibleLabel(card, "Reset", SampleUi.TextTitle, SampleUi.TitleColor);
        _resetState = ResetSection(ResetStateSectionId, card);
        _resetConfigCard = ResetSection(ResetConfigCardId, card);
        RepaintReset();
        RepaintConfigurationReset();
    }

    // A plain stack inside the card, not a nested card: one frame for the group, one repaint
    // target per reset.
    private static RectTransform ResetSection(string id, RectTransform card)
    {
        var section = SampleUi.Panel(id, card, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(section, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceSm), new RectOffset(), false);
        return section;
    }

    private void RepaintConfigurationReset()
    {
        if (_resetConfigCard == null) return;
        for (var i = _resetConfigCard.childCount - 1; i >= 0; i--)
        {
            var child = _resetConfigCard.GetChild(i).gameObject;
            if (child.name == SampleUi.StrokeName) continue;
            child.SetActive(false);
            child.transform.SetParent(null, false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        SampleUi.FlexibleLabel(_resetConfigCard, "Configuration", SampleUi.TextBody, SampleUi.TitleColor);
        SampleUi.FlexibleLabel(_resetConfigCard,
            "Clears the saved profile choice and returns to Configuration. Recorded results, appearance and the current SDK session are kept. Nothing is deleted on the server.",
            SampleUi.TextCaption, SampleUi.Muted);
        if (!string.IsNullOrEmpty(_configResetResult))
            SampleUi.FlexibleLabel(_resetConfigCard, _configResetResult, SampleUi.TextCaption, SampleUi.Attention)
                .gameObject.name = ResetConfigResultId;
        if (_confirmingConfigReset)
        {
            SampleUiBackCancel.Mark(SampleUi.Button(ResetConfigCancelId, _resetConfigCard, "Cancel", SampleUiButtonVariant.Secondary, CancelConfigurationReset));
            SampleUi.Button(ResetConfigConfirmId, _resetConfigCard, "Reset configuration", SampleUiButtonVariant.Destructive, ConfirmConfigurationReset);
        }
        else SampleUi.Button(ResetConfigButtonId, _resetConfigCard, "Reset configuration", SampleUiButtonVariant.Destructive, RequestConfigurationReset);
    }

    private void RepaintReset()
    {
        if (_resetState == null) return;

        for (var i = _resetState.childCount - 1; i >= 0; i--)
        {
            var child = _resetState.GetChild(i).gameObject;
            if (child.name == SampleUi.StrokeName) continue;
            child.SetActive(false);
            child.transform.SetParent(null, false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }

        SampleUi.FlexibleLabel(_resetState, "Sample state", SampleUi.TextBody, SampleUi.TitleColor);
        SampleUi.FlexibleLabel(_resetState,
            "Clears recorded connection results and unseen counts; refreshes community access from the SDK. " +
            "SDK configuration, " +
            "language and appearance stay as they are. No server data is deleted.",
            SampleUi.TextCaption, SampleUi.Muted);

        if (!string.IsNullOrEmpty(_resetResult))
            SampleUi.FlexibleLabel(_resetState, _resetResult, SampleUi.TextCaption, SampleUi.TitleColor)
                .gameObject.name = ResetResultId;

        if (!_confirmingReset)
        {
            SampleUi.Button(ResetButtonId, _resetState, "Reset sample state", SampleUiButtonVariant.Destructive, RequestReset);
            return;
        }

        SampleUi.FlexibleLabel(_resetState, "Reset the sample's recorded state?", SampleUi.TextBody, SampleUi.Attention);
        SampleUiBackCancel.Mark(SampleUi.Button(ResetCancelId, _resetState, "Cancel", SampleUiButtonVariant.Secondary, CancelReset));
        SampleUi.Button(ResetConfirmId, _resetState, "Reset", SampleUiButtonVariant.Destructive, ConfirmReset);
    }
}
