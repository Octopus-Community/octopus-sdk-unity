using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One scenario screen: reference identity, parameters, fill-only presets and Execute/Result.</summary>
public partial class OctopusScenarioScreenView : MonoBehaviour, IOctopusSampleQaScenario
{
    /// <summary>Detail layer above the sample shell; Debug console and feedback can open above it.</summary>
    private const int SortingOrder = SampleUi.DetailSortingOrder;

    private OctopusScenarioPilot _pilot;
    private TMP_Text _resultLabel;
    private TMP_Text _unifiedProfileHint;
    private bool _observingUnifiedProfile;

    /// <summary>The `communityData` hint naming the Unified Profile flag and its effective value.</summary>
    public const string UnifiedProfileHintId = "scenario-unified-profile-hint";
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
    private bool _dismissed;
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
    ///
    /// Idempotent: the shell's <see cref="SampleUiDetailPage.CloseAll"/> and this screen's own
    /// controls can both reach it, and a second call must not touch a destroyed hierarchy nor
    /// release the debug entry or the profile page twice.
    /// </summary>
    public void Dismiss()
    {
        if (_dismissed || this == null) return;
        _dismissed = true;
        // A host profile page opened from this screen has no meaning without it.
        var profilePage = FindAnyObjectByType<OctopusSampleClientProfileView>();
        if (profilePage != null) profilePage.Dismiss();
        // The entry rides in this header: hand it back before the hierarchy goes away.
        SampleUiDebugEntryHost.ReleaseFor(gameObject);
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
        if (_pilot.Presets.Count > 0)
        {
            _selectedPreset = _pilot.Presets[0];
            _selectedPreset.Fill(_pilot.Fields);
        }
        _customizing = _pilot.Id == "communityData";
        Build();
        OctopusSampleBranding.ThemeChanged += OnThemeChanged;
        _pilot.ResultChanged += OnResultChanged;
        OctopusScenarioSdk.Current.ProfileChanged += OnConnectionProfileChanged;
        _pilot.HostProfileRequested += OnHostProfileRequested;
        if (ShowsUnifiedProfileHint || _pilot.Id == "unifiedProfile")
        {
            _observingUnifiedProfile = true;
            OctopusSampleUnifiedProfile.Changed += OnUnifiedProfileChanged;
            OctopusSampleUnifiedProfile.RefreshEffective();
        }
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
            OctopusScenarioSdk.Current.ProfileChanged -= OnConnectionProfileChanged;
        }
        if (_observingUnifiedProfile) OctopusSampleUnifiedProfile.Changed -= OnUnifiedProfileChanged;
        _observingUnifiedProfile = false;
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

    private void LateUpdate()
    {
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
            // Release before detaching; other tabs may also have a suspended detail.
            SampleUiDebugEntryHost.ReleaseFor(gameObject);
            _root.gameObject.SetActive(false);
            _root.SetParent(null, false);
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

    private bool ShowsUnifiedProfileHint
    {
        get { return _pilot != null && _pilot.Id == "communityData"; }
    }

    private void OnUnifiedProfileChanged()
    {
        // Same guard as the theme handler: a view that never woke up keeps its static handler.
        if (this == null)
        {
            OctopusSampleUnifiedProfile.Changed -= OnUnifiedProfileChanged;
            return;
        }
        if (_unifiedProfileHint != null) _unifiedProfileHint.text = OctopusSampleUnifiedProfile.ScenarioHint;
        RefreshUnifiedControls();
    }

    private void OnResultChanged(string result)
    {
        RefreshResult();
    }

    /// <summary>
    /// The catalogue's action that leaves the scenario screen (`communityData` preset 6): the
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
        _resultLabel.text = stale ? "Not run yet — values changed" : _pilot.Result;
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
        _root = SampleUi.Panel("Root", transform, Color.clear);
        SampleUi.Stretch(_root, Vector2.zero, Vector2.one);
        var root = SampleUi.DetailPage("scenario-safe-area", _root, Dismiss);

        BuildHeader(root);

        var content = SampleUi.VerticalScroll(root, SampleUi.OverlayPadding());
        content.GetComponent<VerticalLayoutGroup>().spacing = OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceLg);
        _scroll = content.GetComponentInParent<ScrollRect>(true);
        var title = SampleUi.FlexibleLabel(content, _pilot.Title, SampleUi.TextTitleXl, SampleUi.TitleColor);
        title.name = "scenario-title";
        title.fontStyle = FontStyles.Bold;
        BuildDescription(content);
        BuildFeatureState(content);
        BuildUnifiedProfileHint(content);
        var bench = Section(content, "Try it");
        bench.name = "scenario-try-it";
        var parameters = OctopusScenarioPresentation.ParametersFor(_pilot.Id);
        if (parameters.Length > 0)
        {
            var signature = Section(bench, "Parameters");
            foreach (var parameter in parameters)
                SampleUi.FlexibleLabel(signature, parameter, SampleUi.TextCaption, SampleUi.Muted).richText = false;
        }
        var guidance = SampleUi.FlexibleLabel(bench, "You will see: " + _pilot.YouWillSee,
                                              SampleUi.TextBody, SampleUi.TitleColor);
        guidance.name = "scenario-you-will-see";
        if (_pilot.Id == "unifiedProfile") BuildUnifiedControls(bench);
        else
        {
            BuildPresets(bench);
            BuildFields(bench);
            if (_pilot.Id == "connection") BuildConnectionControls(bench);
            BuildResult(bench);
        }
        if (OctopusScenarioPresentation.CanVerify(_pilot.Id))
            SampleUi.Button("scenario-verify-community", content, "Verify in Community", VerifyInCommunity);
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
        var card = SampleUi.Panel("scenario-description", content, OctopusSampleBranding.Clear);
        SampleUi.VerticalStack(card, OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceSm), new RectOffset(), false);
        var chip = SampleUi.Card("scenario-api-" + _pilot.Row.ReferenceApi, card);
        SampleUi.FlexibleLabel(chip, _pilot.Row.ReferenceApi, SampleUi.TextCaption, SampleUi.Muted);
        SampleUi.FlexibleLabel(card, _pilot.Row.Subtitle, SampleUi.TextBody, SampleUi.Muted);
    }

    private void BuildFields(RectTransform content)
    {
        var section = Section(content, "Controls");
        if (_pilot.CanCustomize)
        {
            var customize = SampleUi.Button(_pilot.Id + "-customize", section, "Customize", SampleUiButtonVariant.Tertiary, () =>
            {
                if (_customizing) ApplyPreset(_selectedPreset, false);
                else SetCustomizing(true);
            });
            _customizeLabel = customize.GetComponentInChildren<TMP_Text>(true);
        }
        RectTransform information = null;
        foreach (var field in _pilot.Fields.All)
        {
            if (_pilot.Id == "communityData" && field.IsInformational) continue;
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
                if (field.IsInformational || (!_customizing && _pilot.Id != "communityData")) return;
                if (_pilot.Id == "communityData") _customizing = true;
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
        _customRun = SampleUi.Button(_pilot.Id + "-run", content, "Execute", ExecuteCurrent).gameObject;
        _runButtons.Add(_customRun.GetComponent<Button>());

    }

    /// <summary>
    /// `communityData` only: the host profile route needs the server-driven exposeClientUserId flag,
    /// whose value depends on the community and backend host. Read-only here; the switch is in
    /// Configuration.
    /// </summary>
    private void BuildUnifiedProfileHint(RectTransform content)
    {
        _unifiedProfileHint = null;
        if (!ShowsUnifiedProfileHint) return;
        var card = SampleUi.Card(UnifiedProfileHintId, content);
        _unifiedProfileHint = SampleUi.FlexibleLabel(card, OctopusSampleUnifiedProfile.ScenarioHint,
            SampleUi.TextCaption, SampleUi.Muted);
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

        var banner = SampleUi.Card("scenario-feature-state", content);
        SampleUi.FlexibleLabel(banner, label + " is " + (enabled ? "on" : "off"), SampleUi.TextBody, SampleUi.TitleColor);
        SampleUi.Button("scenario-feature-change", banner, "Change", SampleUiButtonVariant.Tertiary, () =>
        {
            var shell = FindAnyObjectByType<OctopusSampleShell>();
            Dismiss();
            if (section == ScenarioSection.SignIn)
            {
                if (shell != null) shell.Select(OctopusSampleTab.Settings);
                OctopusSampleConfigView.Open();
            }
            else if (shell != null) shell.ShowScenarioSection(section);
        });
    }

    private void SetCustomizing(bool customizing)
    {
        _customizing = customizing;
        foreach (var input in _inputs)
        {
            var editable = false;
            foreach (var field in _pilot.Fields.All)
                if (field.Key == input.Key) editable = (customizing || _pilot.Id == "communityData") && !field.IsInformational;
            input.Value.readOnly = !editable;
            input.Value.interactable = editable;
            ((SampleUiInputField)input.Value).RefreshVisuals();
        }
        if (_customizeLabel != null)
            _customizeLabel.text = customizing ? "Reset to preset" : "Customize";
        if (_customRun != null) _customRun.SetActive(true);
    }

    private void BuildPresets(RectTransform content)
    {
        var section = Section(content, "Presets");
        SampleUi.FlexibleLabel(section, "Choose a preset to fill parameters, then Execute.", SampleUi.TextCaption, SampleUi.Muted);
        foreach (var preset in _pilot.Presets)
        {
            var current = preset;
            // GameObject.name IS the catalogue's test_id, verbatim — this is the handle the QA
            // pipeline taps.
            var button = SampleUi.Button(current.TestId, section, current.Label,
                SampleUiButtonVariant.Secondary, () => ApplyPreset(current, false),
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

    private void ExecuteCurrent()
    {
        if (_pilot.IsRunning) return;
        RememberRunValues();
        _pilot.Execute(_customizing && _pilot.CanCustomize ? (System.Action)_pilot.RunCustom :
            () => _selectedPreset.Run(_pilot.Fields));
    }

    /// <summary>
    /// Fills every field and mirrors it into the inputs. Taps and explicit QA requests share this
    /// path; restoring values without running makes no SDK call.
    /// </summary>
    private void ApplyPreset(OctopusScenarioPreset preset, bool run)
    {
        if (_pilot.IsRunning) return;
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
