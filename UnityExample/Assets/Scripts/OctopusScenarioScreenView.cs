using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The generic scenario screen: one <see cref="OctopusScenarioPilot"/>, rendered as the contract in
/// SDK_STANDARDS §5.2 describes it.
///
/// What that contract asks for, and where each half lives here:
///
/// - **Single-tap presets, labelled `Preset N · &lt;description&gt;`, that pre-fill every field.**
///   One button per catalogue preset, each carrying the catalogue's `test_id` as its
///   <c>GameObject.name</c> so the QA pipeline can address it. A tap fills the fields and then
///   runs — no second confirm button, nothing left to type into.
/// - **A live result panel**, named with the catalogue's `result_test_id`, updated from the
///   pilot's own result line.
/// - **Locked parameters with opt-in Customize.** Custom Run uses the edited values. Reset
///   restores the last preset without a call; tapping a preset restores, locks and runs it.
///   Controls use §5.4's `<section>-<element>[-<action>]` shape: `<id>-customize` (also Reset
///   while editing) and `<id>-run` inside Parameters. Informational fields stay read-only in
///   a separate context card. Existing preset and result ids are unchanged.
/// - **Stale results**, as on Android: when fields differ from the last run's snapshot, replace
///   the result with the muted line "Not run yet — values changed" until Run or a value restore.
/// - **A usable state on entry**: the first preset's values are filled in as the screen is built,
///   so no field is ever blank and the result panel says what state the scenario is in.
/// - **No automatic mutation**: building this screen makes no SDK call at all. Filling fields is
///   pure; the SDK is reached from preset Run or custom Run only, i.e. behind a
///   tap or an explicit QA launch request. `OctopusScenarioScreenViewTests` counts API calls on the sample log seam
///   while the screen is built.
/// - **Theme and safe-area changes** rebuild only the presentation, preserving the pilot, the
///   selected preset, custom values, run snapshot and scroll position without another SDK call.
///
/// Built in code with no prefab and no scene YAML, like <see cref="OctopusScenariosListView"/>, and
/// through the same <see cref="SampleUi"/> builders so the two screens cannot drift apart.
///
/// Presets stack vertically so their full labels can wrap on narrow screens.
/// </summary>
public class OctopusScenarioScreenView : MonoBehaviour, IOctopusSampleQaScenario
{
    /// <summary>Detail layer above the sample shell; Debug console and feedback can open above it.</summary>
    private const int SortingOrder = SampleUi.DetailSortingOrder;

    private OctopusScenarioPilot _pilot;
    private TMP_Text _resultLabel;
    private string _lastQaResult;
    private bool _qaBound;
    private GameObject _resultPanel;
    private GameObject _runningPlaceholder;
    private TMP_Text _customizeLabel;
    private GameObject _customRun;
    private bool _customizing;
    private RectTransform _root;
    private ScrollRect _scroll;
    private readonly List<Button> _runButtons = new List<Button>();
    private OctopusScenarioPreset _selectedPreset;
    private readonly Dictionary<string, string> _lastRunValues = new Dictionary<string, string>();
    private bool _hasRun;
    private readonly List<KeyValuePair<string, TMP_InputField>> _inputs =
        new List<KeyValuePair<string, TMP_InputField>>();

    /// <summary>
    /// Opens the screen for <paramref name="pilot"/> over the current scene. A second call while
    /// one is open is a no-op and returns the open screen.
    /// </summary>
    public static OctopusScenarioScreenView Open(OctopusScenarioPilot pilot)
    {
        var existing = FindAnyObjectByType<OctopusScenarioScreenView>();
        if (existing != null) return existing;

        var host = new GameObject("OctopusScenarioScreenView");
        var view = host.AddComponent<OctopusScenarioScreenView>();
        // Built here rather than from Start(): the screen has to exist fully formed the moment
        // Open returns, both for the caller and for an EditMode test, where Start never runs.
        view.Bind(pilot);
        return view;
    }

    /// <summary>
    /// Closes the screen exactly as its own Back button does.
    ///
    /// Deactivating before destroying is what makes it usable by a caller that opens another
    /// scenario in the same frame: in play mode <c>Destroy</c> only takes effect at the end of the
    /// frame, and <see cref="Open"/> answers with whatever screen it finds — an inactive one is not
    /// found, a merely doomed one is.
    /// </summary>
    public void Dismiss()
    {
        // A host profile page opened from this screen has no meaning without it.
        var profilePage = FindAnyObjectByType<OctopusSampleClientProfileView>();
        if (profilePage != null) profilePage.Dismiss();
        // The entry rides in this header: hand it back before the hierarchy goes away.
        SampleUiDebugEntryHost.ReleaseAll();
        gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }

    /// <summary>
    /// The catalogue id of the scenario on screen, or null before <see cref="Bind"/>.
    ///
    /// Read-only and id-only on purpose: a caller that wants to know *where the reader is* gets the
    /// one stable string QA scripts and the scenario catalogue already agree on, and cannot reach
    /// the pilot to drive it. The bug-report row is the caller — a defect is only reproducible if
    /// the report says which scenario produced it.
    /// </summary>
    public string ScenarioId
    {
        get { return _pilot == null ? null : _pilot.Id; }
    }

    /// <summary>Attaches a pilot and builds the screen. Called once, by <see cref="Open"/>.</summary>
    public void Bind(OctopusScenarioPilot pilot)
    {
        _pilot = pilot;
        SampleUi.OverlayCanvas(gameObject, SortingOrder);
        _selectedPreset = _pilot.Presets[0];
        _selectedPreset.Fill(_pilot.Fields);
        Build();
        OctopusSampleBranding.ThemeChanged += OnThemeChanged;
        _pilot.ResultChanged += OnResultChanged;
        _pilot.HostProfileRequested += OnHostProfileRequested;
        var shell = FindAnyObjectByType<OctopusSampleShell>();
        if (shell != null) shell.Location.OpenScenario(pilot.Id);
        _qaBound = true;
        OctopusSampleQaLaunch.Log("scenario=" + ScenarioId + " state=opened");
        RefreshResult();
    }

    private void OnDestroy()
    {
        if (_pilot != null)
        {
            _pilot.ResultChanged -= OnResultChanged;
            _pilot.HostProfileRequested -= OnHostProfileRequested;
        }
        // Pilots that subscribed to SDK events release them here instead of waiting for the next event.
        var disposable = _pilot as System.IDisposable;
        if (disposable != null) disposable.Dispose();
        OctopusSampleBranding.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged()
    {
        // The theme event is static and OnDestroy does not run for a component that never woke up
        // (EditMode), so a destroyed view can still hold this handler: drop it instead of rebuilding.
        if (this == null)
        {
            OctopusSampleBranding.ThemeChanged -= OnThemeChanged;
            return;
        }
        if (Application.isPlaying) _rebuildRequested = true;
        else Rebuild();
    }

    private bool _rebuildRequested;
    private RectTransform _topBleed, _bottomBleed;

    private void LateUpdate()
    {
        SampleUi.ResizeBleed(_topBleed, true);
        SampleUi.ResizeBleed(_bottomBleed, false);
        // Safe-area components update anchors in place, retaining keyboard focus and scroll.
        if (!_rebuildRequested) return;
        _rebuildRequested = false;
        Rebuild();
    }

    private void Rebuild()
    {
        var position = _scroll == null ? 1f : _scroll.verticalNormalizedPosition;
        if (_root != null)
        {
            _root.gameObject.SetActive(false);
            _root.SetParent(null, false);
            // The entry rides in this header: hand it back before the hierarchy goes away.
            SampleUiDebugEntryHost.ReleaseAll();
            if (Application.isPlaying) Destroy(_root.gameObject);
            else DestroyImmediate(_root.gameObject);
        }
        _inputs.Clear();
        _runButtons.Clear();
        _resultLabel = null;
        _customizeLabel = null;
        _customRun = null;
        Build();
        Canvas.ForceUpdateCanvases();
        _scroll.verticalNormalizedPosition = position;
    }

    private void OnResultChanged(string result)
    {
        RefreshResult();
    }

    /// <summary>
    /// The catalogue's one preset that leaves the scenario screen (`communityData` preset 6): the
    /// pilot asks for a host-owned profile page and this renderer presents the sample's, over
    /// this screen, bound to the destination the pilot keeps updating.
    /// </summary>
    private void OnHostProfileRequested(OctopusScenarioHostProfile profile)
    {
        OctopusSampleClientProfileView.Open(profile);
    }

    private void RefreshResult()
    {
        if (_resultLabel == null) return;
        _runningPlaceholder.SetActive(_pilot.IsRunning);
        _resultPanel.SetActive(!_pilot.IsRunning);
        var stale = false;
        if (_hasRun)
        {
            foreach (var field in _pilot.Fields.All)
            {
                string previous;
                if (!_lastRunValues.TryGetValue(field.Key, out previous) || previous != field.Value) stale = true;
            }
        }
        _resultLabel.text = stale ? "Not run yet — values changed\nRun these values to refresh the result." : _pilot.Result;
        _resultLabel.color = stale ? SampleUi.Muted : SampleUi.TitleColor;
        foreach (var button in _runButtons) button.interactable = !_pilot.IsRunning;
        var displayed = _pilot.IsRunning ? "Running…" : _resultLabel.text;
        if (_qaBound && displayed != _lastQaResult)
        {
            _lastQaResult = displayed;
            OctopusSampleQaLaunch.Log("scenario=" + ScenarioId + " result=" + displayed);
        }
    }

    private void RememberRunValues()
    {
        _lastRunValues.Clear();
        foreach (var field in _pilot.Fields.All) _lastRunValues.Add(field.Key, field.Value);
        _hasRun = true;
        RefreshResult();
    }

    private void Build()
    {
        GetComponent<CanvasScaler>().referenceResolution = new Vector2(
            SampleUi.ReferenceWidthFor(SampleUi.ScreenDpWidth()), SampleUi.CanvasReference.y);

        // The scene supplies the EventSystem, exactly as the scenario list documents: the project
        // is Input-System-only (ProjectSettings activeInputHandler: 1), so a hand-rolled
        // EventSystem's legacy StandaloneInputModule would throw every frame.
        _root = SampleUi.Panel("Root", transform, SampleUi.Background);
        SampleUi.Stretch(_root, Vector2.zero, Vector2.one);
        _topBleed = SampleUi.BuildBleed(_root, "TopBleed", OctopusSampleBranding.Palette.Chrome, true);
        _bottomBleed = SampleUi.BuildBleed(_root, "BottomBleed", OctopusSampleBranding.Palette.Surface, false);
        var root = SampleUi.SafeArea("scenario-safe-area", _root);

        BuildHeader(root);

        var content = SampleUi.VerticalScroll(root, SampleUi.OverlayPadding());
        content.GetComponent<VerticalLayoutGroup>().spacing = OctopusSampleBranding.Dp(16f);
        _scroll = content.GetComponentInParent<ScrollRect>();
        BuildDescription(content);
        BuildFeatureState(content);
        BuildFields(content);
        var guidance = SampleUi.FlexibleLabel(content, "You will see: " + _pilot.YouWillSee,
                                              SampleUi.TextBody, SampleUi.TitleColor);
        guidance.name = "scenario-you-will-see";
        BuildPresets(content);
        BuildResult(content);
        SetCustomizing(_customizing);
        RefreshResult();
    }

    private static int Units(float dp)
    {
        return Mathf.RoundToInt(OctopusSampleBranding.Dp(dp));
    }

    private void BuildHeader(RectTransform root)
    {
        var header = SampleUi.AppBar("Header", root, _pilot.Title, Dismiss);
        SampleUiDebugEntryHost.Attach(header);
    }

    private void BuildDescription(RectTransform content)
    {
        var card = SampleUi.Card("scenario-description", content);
        var capability = SampleUi.FlexibleLabel(card, _pilot.Capability,
            SampleUi.TextBody, SampleUi.TitleColor);
        capability.name = "Capability";
        foreach (var symbol in _pilot.ApiSymbols)
        {
            var chip = SampleUi.Panel("scenario-api-" + symbol, card, SampleUi.FieldBackground,
                OctopusSampleBranding.FieldRadius, OctopusSampleBranding.Palette.Border);
            SampleUi.VerticalStack(chip, 0f, new RectOffset(Units(10f), Units(10f), Units(4f), Units(4f)), false);
            var label = SampleUi.FlexibleLabel(chip, symbol, SampleUi.TextCaption, SampleUi.Muted);
            label.fontStyle = FontStyles.Bold;
        }
    }

    private void BuildFields(RectTransform content)
    {
        var section = Section(content, "Parameters");
        if (_pilot.CanCustomize)
        {
            var customize = SampleUi.Button(_pilot.Id + "-customize", section, "Customize", SampleUiButtonVariant.Tertiary, () =>
            {
                if (_customizing) ApplyPreset(_selectedPreset, false);
                else SetCustomizing(true);
            });
            _customizeLabel = customize.GetComponentInChildren<TMP_Text>();
        }
        RectTransform information = null;
        foreach (var field in _pilot.Fields.All)
        {
            // Informative context stays visible with its existing QA ids, outside Customize.
            if (field.IsInformational && information == null)
                information = Section(content, "Preset context");
            var parent = field.IsInformational ? information : section;
            var input = SampleUi.LabeledField(_pilot.Id + "-field-" + field.Key, parent,
                field.Label + (field.IsInformational ? " · Read only" : ""), field.Value);
            input.readOnly = true;
            input.interactable = false;
            ((SampleUiInputField)input).RefreshVisuals();

            var key = field.Key;
            input.onValueChanged.AddListener(value =>
            {
                if (!_customizing || field.IsInformational) return;
                _pilot.Fields.Set(key, value);
                RefreshResult();
            });
            _inputs.Add(new KeyValuePair<string, TMP_InputField>(key, input));
        }
        BuildCustomRun(section);
        if (!string.IsNullOrEmpty(_pilot.ParameterNotice))
        {
            var notice = SampleUi.Card("scenario-parameter-notice", content);
            SampleUi.FlexibleLabel(notice, _pilot.ParameterNotice, SampleUi.TextCaption, SampleUi.Muted);
        }
    }

    private void BuildCustomRun(RectTransform content)
    {
        if (!_pilot.CanCustomize) return;
        _customRun = SampleUi.Button(_pilot.Id + "-run", content, "Run", () =>
        {
            if (!_customizing || _pilot.IsRunning) return;
            RememberRunValues();
            _pilot.Execute(_pilot.RunCustom);
        }).gameObject;
        _runButtons.Add(_customRun.GetComponent<Button>());
        _customRun.SetActive(false);
    }

    private void BuildFeatureState(RectTransform content)
    {
        var section = OctopusScenarioSections.SectionOf(_pilot.Id);
        string label;
        bool enabled;
        if (section == ScenarioSection.SignIn)
        {
            label = OctopusSampleFeatureToggles.ForceLoginLabel;
            enabled = OctopusSampleFeatureToggles.ForceLogin;
        }
        else if (section == ScenarioSection.Notifications)
        {
            label = OctopusSampleFeatureToggles.PushRegistrationLabel;
            enabled = OctopusSampleFeatureToggles.PushRegistration;
        }
        else return;

        SampleUi.Button("scenario-feature-state", content,
            "Feature: " + label + " · " + (enabled ? "On" : "Off"), SampleUiButtonVariant.Tertiary, () =>
            {
                var shell = FindAnyObjectByType<OctopusSampleShell>();
                if (shell != null) shell.Select(OctopusSampleTab.Scenarios);
                Dismiss();
            });
    }

    private void SetCustomizing(bool customizing)
    {
        _customizing = customizing;
        foreach (var input in _inputs)
        {
            var editable = false;
            foreach (var field in _pilot.Fields.All)
                if (field.Key == input.Key) editable = customizing && !field.IsInformational;
            input.Value.readOnly = !editable;
            input.Value.interactable = editable;
            ((SampleUiInputField)input.Value).RefreshVisuals();
        }
        if (_customizeLabel != null)
            _customizeLabel.text = customizing ? "Reset to preset" : "Customize";
        if (_customRun != null) _customRun.SetActive(customizing);
    }

    private void BuildPresets(RectTransform content)
    {
        var section = Section(content, "Presets");
        SampleUi.FlexibleLabel(section, "Tap once to fill parameters and run.", SampleUi.TextCaption, SampleUi.Muted);
        foreach (var preset in _pilot.Presets)
        {
            var current = preset;
            // GameObject.name IS the catalogue's test_id, verbatim — this is the handle the QA
            // pipeline taps.
            var button = SampleUi.Button(current.TestId, section, current.Label,
                SampleUiButtonVariant.Secondary, () => ApplyPreset(current, true),
                OctopusSampleBranding.FieldRadius);
            _runButtons.Add(button.GetComponent<Button>());
        }
    }

    private void BuildResult(RectTransform content)
    {
        SampleUi.FlexibleLabel(content, "Result", SampleUi.TextCaption, SampleUi.Muted);

        // A placeholder uses the same panel and text styles as the existing result.
        var running = SampleUi.Card("scenario-result-skeleton", content);
        SampleUi.FlexibleLabel(running, "Running…", SampleUi.TextBody, SampleUi.Muted);
        _runningPlaceholder = running.gameObject;
        _runningPlaceholder.SetActive(false);

        // GameObject.name IS the catalogue's result_test_id, verbatim.
        var panel = SampleUi.Card(_pilot.ResultTestId, content);
        _resultPanel = panel.gameObject;
        _resultLabel = SampleUi.FlexibleLabel(panel, _pilot.Result, SampleUi.TextBody,
                                              SampleUi.TitleColor);
        _resultLabel.richText = false;
    }

    private RectTransform Section(RectTransform content, string title)
    {
        var section = SampleUi.Card(title + "Section", content);
        var label = SampleUi.FlexibleLabel(section, title, SampleUi.TextTitle, SampleUi.TitleColor);
        label.fontStyle = FontStyles.Bold;
        return section;
    }

    bool IOctopusSampleQaScenario.RunPreset(int number)
    {
        if (!_qaBound || _pilot.IsRunning || number < 1 || number > _pilot.Presets.Count) return false;
        ApplyPreset(_pilot.Presets[number - 1], true);
        return true;
    }

    /// <summary>
    /// Fills every field and mirrors it into the inputs. Taps and explicit QA requests share this
    /// path; restoring values without running makes no SDK call.
    /// </summary>
    private void ApplyPreset(OctopusScenarioPreset preset, bool run)
    {
        if (run && _pilot.IsRunning) return;
        _selectedPreset = preset;
        preset.Fill(_pilot.Fields);
        foreach (var input in _inputs)
        {
            // SetTextWithoutNotify, not `.text`: the listener installed in BuildFields would
            // otherwise write the value straight back into the field it just came from.
            input.Value.SetTextWithoutNotify(_pilot.Fields.Get(input.Key));
        }
        SetCustomizing(false);
        if (run)
        {
            RememberRunValues();
            for (var i = 0; i < _pilot.Presets.Count; i++)
            {
                if (_pilot.Presets[i] != preset) continue;
                OctopusSampleQaLaunch.Log("scenario=" + ScenarioId + " preset=" +
                    (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + " state=running");
                break;
            }
            _pilot.Execute(() => preset.Run(_pilot.Fields));
        }
        RefreshResult();
    }
}
