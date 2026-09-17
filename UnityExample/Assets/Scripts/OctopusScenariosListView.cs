using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Scenarios tab: the searchable, sectioned list of scenario cards SDK_STANDARDS §5.1 asks
/// every sample for, rendered into <see cref="OctopusSampleShell"/>'s content area.
///
/// Built entirely in code, with no prefab and no scene wiring — through the shared
/// <see cref="SampleUi"/> builders, which this screen's own styles became. That is deliberate: a
/// list that has to track a catalogue living in another repo is the last thing that should be
/// maintained as serialized YAML. Anything that only exists in code can be diffed and reviewed.
///
/// **The list holds only what this sample can demonstrate.** Which scenarios those are, how they
/// are grouped and how a query filters them is <see cref="OctopusScenarioSections"/>, as data, so
/// the grammar of this screen is testable without touching a `UnityEngine.UI` type. Read the rule
/// and its source (TOKENS §1) there. The visible consequence: until this PR the tab rendered all 27
/// catalogue rows, 24 of them saying "Not implemented" with a shortcut to a legacy `*Example`
/// scene; it now renders the three that have a scenario screen, and grows by one card per scenario
/// PR.
///
/// Losing those shortcuts loses no route and fixes a hazard. The legacy scenes are still reachable
/// from the Settings tab (`BuildLegacyScenesRoute`, which goes away with the last of them, issue
/// #100), and a shortcut on a scenario card was a one-way door: `SceneManager.LoadScene` replaced
/// the whole shell — with no way back short of relaunching — and re-initialised the SDK in whatever
/// mode that scene chose, behind the pilots' backs, which is issue #100 itself.
///
/// What each part of the screen is named, and why it is named that:
/// - the search field is `scenarios-search-input`, the shared catalogue's own id;
/// - a card is `scenarios-&lt;id&gt;-card`, likewise;
/// - a section head is `scenarios-section-&lt;name&gt;`, which the catalogue does not define — see
///   <see cref="OctopusScenarioSections.HeaderIdOf"/>;
/// - the Sign-in section's feature switch is `scenarios-toggle-force-login`, which the catalogue
///   does not define either — it is Android's own test tag, reused verbatim so one QA step reads
///   the same on both platforms;
/// - the Notifications switch is `scenarios-toggle-push-registration`, also Android's test tag;
/// - the empty state is `scenarios-empty`.
/// Unity renders into one opaque surface with no accessibility tree, so a name that moves takes the
/// QA script with it.
/// </summary>
public class OctopusScenariosListView : MonoBehaviour
{
    /// <summary>The shared catalogue's id for the search field.</summary>
    public const string SearchInputId = "scenarios-search-input";

    /// <summary>The "nothing matched" card, present only when the query filters everything out.</summary>
    public const string EmptyStateId = "scenarios-empty";

    /// <summary>The Sign-in section's feature-toggle row, which holds the `Force login` switch.</summary>
    public const string ForceLoginRowId = "scenarios-toggle-force-login-row";

    /// <summary>The line that appears under the switch once the SDK is up and it decides nothing.</summary>
    public const string ForceLoginLockedNoteId = "scenarios-toggle-force-login-locked";

    public const string PushRegistrationRowId = "scenarios-toggle-push-registration-row";
    public const string PushRegistrationLockedNoteId = "scenarios-toggle-push-registration-locked";

    private RectTransform _list;
    private bool _listening;
    private bool _lockedAsDrawn;
    private string _query = "";
    private TMP_InputField _searchField;

    public sealed class ViewState
    {
        public string Query = "";
        public readonly Dictionary<ScenarioSection, bool> Open = new Dictionary<ScenarioSection, bool>();
    }

    public ViewState CaptureState()
    {
        var state = new ViewState { Query = _query };
        foreach (var pair in _open) state.Open[pair.Key] = pair.Value;
        return state;
    }
    private readonly Dictionary<ScenarioSection, bool> _open = new Dictionary<ScenarioSection, bool>();

    /// <summary>
    /// Fills <paramref name="host"/> — the shell's content area — with the scrollable list.
    /// </summary>
    public static OctopusScenariosListView BuildInto(RectTransform host, ViewState state = null)
    {
        var view = host.gameObject.AddComponent<OctopusScenariosListView>();
        if (state != null)
        {
            view._query = state.Query;
            foreach (var pair in state.Open) view._open[pair.Key] = pair.Value;
        }
        var content = SampleUi.VerticalScroll(host, SampleUi.ContentPadding());
        content.GetComponent<VerticalLayoutGroup>().spacing = OctopusSampleBranding.Dp(16f);
        view.Build(content);
        return view;
    }

    /// <summary>The live query, as the search field last reported it.</summary>
    public string Query { get { return _query; } }

    /// <summary>
    /// Whether <paramref name="section"/> shows its cards. A non-blank query opens every section
    /// regardless — a card hidden by a collapse the reader set ten taps ago reads as "no result".
    /// </summary>
    public bool IsSectionOpen(ScenarioSection section)
    {
        if (Searching) return true;
        bool open;
        return !_open.TryGetValue(section, out open) || open;
    }

    /// <summary>Collapses an open section, or opens a collapsed one, and repaints the list.</summary>
    public void ToggleSection(ScenarioSection section)
    {
        // Reads through IsSectionOpen so the default (open) is stated in one place, and writes the
        // remembered position rather than the effective one: a toggle made while searching would
        // otherwise persist "open" for every section it touched.
        _open[section] = !IsSectionOpen(section);
        Repaint();
    }

    /// <summary>Applies a new search query and repaints. Wired to the search field's own events.</summary>
    public void SetQuery(string query)
    {
        _query = query ?? "";
        if (_searchField != null) _searchField.SetTextWithoutNotify(_query);
        Repaint();
    }

    /// <summary>The empty state's exact wording, echoing the query the way React Native's does.</summary>
    public static string EmptyStateText(string query)
    {
        return "No scenario matches \"" + (query ?? "").Trim() + "\".";
    }

    private bool Searching { get { return _query.Trim().Length > 0; } }

    private void Build(RectTransform content)
    {
        SampleUi.FlexibleLabel(content, "Search scenarios", SampleUi.TextCaption, SampleUi.Muted);
        _searchField = SampleUi.Field(SearchInputId, content, _query, placeholder: "Search scenarios");
        _searchField.onValueChanged.AddListener(SetQuery);
        SampleUi.FlexibleLabel(content, "Explore the capabilities available in this sample.",
            SampleUi.TextCaption, SampleUi.Muted);

        // Its own container, so a query repaints the list without touching the search field — which
        // would take the caret and the text with it on every keystroke.
        _list = SampleUi.Panel("SectionList", content, Color.clear);
        SampleUi.VerticalStack(_list, OctopusSampleBranding.Dp(16f), new RectOffset(), false);

        // Subscribed from here rather than only from OnEnable, for the reason the Home dashboard
        // gives: in edit mode Unity never calls OnEnable, so a test that initialises the SDK after
        // building would watch a list that never hears about it. Listen() is idempotent.
        Listen();
        Repaint();
    }

    private void OnEnable() { Listen(); }

    private void OnDisable() { StopListening(); }

    private void OnDestroy()
    {
        // Edit mode delivers no OnDisable either, and a handler left on a static event would fire
        // on a destroyed view in the next test.
        StopListening();
    }

    private void Listen()
    {
        if (_listening) return;
        OctopusSampleState.Changed += OnStateChanged;
        _listening = true;
    }

    private void StopListening()
    {
        if (!_listening) return;
        OctopusSampleState.Changed -= OnStateChanged;
        _listening = false;
    }

    /// <summary>
    /// Repaints when — and only when — Force login becomes locked.
    ///
    /// The list itself reads a static catalogue, so nothing else here depends on the sample state,
    /// and rebuilding every card each time a notification count arrives would be waste. The lock
    /// does depend on it, and on a change nothing else would deliver: a scenario initialises the
    /// SDK from a screen drawn *over* this list, so returning from it would otherwise leave a live
    /// switch on a header built before the SDK came up.
    /// </summary>
    private void OnStateChanged()
    {
        if (Locked() == _lockedAsDrawn) return;
        Repaint();
    }

    private static bool Locked()
    {
        return OctopusSampleFeatureToggles.ForceLoginLockedNote().Length > 0;
    }

    private void Repaint()
    {
        if (_list == null) return;

        // Recorded here rather than where the row is built: a query that filters the Sign-in
        // section out draws no switch at all, and a stale value would then keep OnStateChanged
        // from repainting once the query is cleared.
        _lockedAsDrawn = Locked();

        for (var i = _list.childCount - 1; i >= 0; i--)
        {
            var child = _list.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }

        var groups = OctopusScenarioSections.Filter(_query);
        if (groups.Count == 0)
        {
            var empty = SampleUi.Card(EmptyStateId, _list);
            SampleUi.FlexibleLabel(empty, "No scenarios found", SampleUi.TextTitle, SampleUi.TitleColor);
            SampleUi.FlexibleLabel(empty, EmptyStateText(_query), SampleUi.TextBody, SampleUi.Muted);
            SampleUi.FlexibleLabel(empty, "Try a capability or API name.", SampleUi.TextCaption, SampleUi.Muted);
            return;
        }

        foreach (var group in groups)
        {
            var section = SampleUi.Card("Section", _list);
            BuildSectionHead(group, section);
            BuildFeatureToggle(group, section);
            if (!IsSectionOpen(group.Section)) continue;
            foreach (var scenario in group.Scenarios) BuildCard(scenario, section);
        }
    }

    // Header and toggle share a framed section, but their hit regions remain separate.
    private void BuildSectionHead(ScenarioSectionGroup group, RectTransform parent)
    {
        var head = SampleUi.Panel(group.HeaderId, parent, OctopusSampleBranding.Clear);
        var layout = head.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = OctopusSampleBranding.Dp(8f);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        head.gameObject.AddComponent<LayoutElement>().minHeight = OctopusSampleBranding.MinTouchUnits;

        var title = SampleUi.Label("Title", head, group.Title, SampleUi.TextBody,
            SampleUi.TitleColor, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyles.Bold;
        var titleSize = title.gameObject.AddComponent<LayoutElement>();
        titleSize.minWidth = 0f;
        titleSize.preferredWidth = 0f;
        titleSize.flexibleWidth = 1f;

        // Keep Count on the action as well as its existing wording for QA readers.
        var count = SampleUi.Label("Count", head, CountText(group), SampleUi.TextCaption,
            SampleUi.Muted, TextAnchor.MiddleRight);
        var countSize = count.gameObject.AddComponent<LayoutElement>();
        countSize.minWidth = countSize.preferredWidth = OctopusSampleBranding.Dp(88f);
        if (Searching || group.Scenarios.Count == 0) return;

        head.GetComponent<Image>().raycastTarget = true;
        head.gameObject.AddComponent<SampleUiTouchTarget>();
        var button = head.gameObject.AddComponent<SampleUiButton>();
        button.targetGraphic = head.GetComponent<Image>();
        var palette = OctopusSampleBranding.Palette;
        button.Configure(head.GetComponent<Image>(), null, title, palette.Surface, palette.Title,
            palette.Border, OctopusSampleBranding.CardRadius, detail: count);
        var section = group.Section;
        button.onClick.AddListener(() => ToggleSection(section));
    }

    /// <summary>
    /// The section's feature switch, drawn under its head — and nowhere else, which is the shared
    /// design contract's rule for a toggle. Sign-in and Notifications carry switches;
    /// every other section returns without adding a row.
    ///
    /// What the switch does, why it is not a package API, and when it stops applying are all in
    /// <see cref="OctopusSampleFeatureToggles"/>. Two things are this screen's own:
    ///
    /// - **The row states the effect in product words, under the label**, in both positions —
    ///   Android's `FeatureToggleRow` renders the same sentence from the same source. A switch that
    ///   flips with nothing on screen saying what changed is the defect the contract names.
    /// - **Force login locks once the SDK is up**, with a restart note beneath its effect.
    ///   Push registration stays interactable: enabling forwards the cached device token and
    ///   disabling stops forwarding new tokens. The view listens to
    ///   <see cref="OctopusSampleState.Changed"/> so Force login also locks when a scenario
    ///   initialises the SDK over this list.
    /// </summary>
    private void BuildFeatureToggle(ScenarioSectionGroup group, RectTransform parent)
    {
        if (group.Section != ScenarioSection.SignIn && group.Section != ScenarioSection.Notifications) return;

        var push = group.Section == ScenarioSection.Notifications;
        var id = push ? OctopusSampleFeatureToggles.PushRegistrationId : OctopusSampleFeatureToggles.ForceLoginId;
        var label = push ? OctopusSampleFeatureToggles.PushRegistrationLabel : OctopusSampleFeatureToggles.ForceLoginLabel;
        var on = push ? OctopusSampleFeatureToggles.PushRegistration : OctopusSampleFeatureToggles.ForceLogin;
        var effect = push ? OctopusSampleFeatureToggles.PushRegistrationEffect(on) : OctopusSampleFeatureToggles.ForceLoginEffect(on);
        var locked = push ? string.Empty : OctopusSampleFeatureToggles.ForceLoginLockedNote();

        var row = SampleUi.Panel(push ? PushRegistrationRowId : ForceLoginRowId, parent, OctopusSampleBranding.Clear);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 24f;
        layout.padding = new RectOffset(0, 0, 24, 24);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        // The copy takes whatever width the switch leaves, rather than a guessed constant: the
        // effect sentences are two lines on a narrow phone and one on a tablet.
        var copy = SampleUi.Panel("Copy", row, Color.clear);
        SampleUi.VerticalStack(copy, 6f, new RectOffset(0, 0, 0, 0), false);
        copy.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        SampleUi.FlexibleLabel(copy, label, SampleUi.TextBody,
                               SampleUi.TitleColor);
        SampleUi.FlexibleLabel(copy, effect,
                               SampleUi.TextCaption, SampleUi.Muted);
        if (locked.Length > 0)
        {
            var note = SampleUi.Label(ForceLoginLockedNoteId, copy, locked, SampleUi.TextCaption,
                                      SampleUi.Attention, TextAnchor.UpperLeft);
            note.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        var next = !on;
        SampleUi.Switch(id, row, on, locked.Length == 0,
                        () => FlipFeatureToggle(push, next));
    }

    /// <summary>
    /// Moves the switch, then repaints — the row draws its own position and its own effect line, so
    /// a flip that did not repaint would leave both stating the position the tester just left.
    /// </summary>
    private void FlipFeatureToggle(bool push, bool enabled)
    {
        var changed = push ? OctopusSampleFeatureToggles.SetPushRegistration(enabled)
                           : OctopusSampleFeatureToggles.SetForceLogin(enabled);
        if (changed) Repaint();
    }

    /// <summary>"3 (hide)" — the count, and what a tap would do to it.</summary>
    private string CountText(ScenarioSectionGroup group)
    {
        if (group.Scenarios.Count == 0) return string.Empty;
        var count = group.Scenarios.Count.ToString();
        if (Searching) return count;
        return count + (IsSectionOpen(group.Section) ? " (hide)" : " (show)");
    }

    internal OctopusScenarioScreenView OpenScenario(string id)
    {
        var pilot = OctopusScenarioPilots.Create(id);
        return pilot == null ? null : OctopusScenarioScreenView.Open(pilot);
    }

    private void BuildCard(OctopusScenario scenario, RectTransform parent)
    {
        var card = SampleUi.Panel("Card", parent, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(card, 0f, new RectOffset(), false);
        var id = scenario.Id;
        SampleUi.ListRow(scenario.CardTestId, card, scenario.Title, scenario.Capability,
            () => OpenScenario(id));
    }
}
