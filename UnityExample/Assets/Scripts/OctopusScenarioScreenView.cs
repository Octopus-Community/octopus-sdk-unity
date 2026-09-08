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
/// - **A usable state on entry**: the first preset's values are filled in as the screen is built,
///   so no field is ever blank and the result panel says what state the scenario is in.
/// - **No automatic mutation**: building this screen makes no SDK call at all. Filling fields is
///   pure; the SDK is reached from <see cref="OctopusScenarioPreset.Run"/> only, i.e. behind a
///   tap. `OctopusScenarioScreenViewTests` proves it by counting API calls on the sample log seam
///   while the screen is built.
///
/// Built in code with no prefab and no scene YAML, like <see cref="OctopusScenariosListView"/>, and
/// through the same <see cref="SampleUi"/> builders so the two screens cannot drift apart.
///
/// Presets are stacked vertically rather than laid out in one horizontal row: at the 1080-wide
/// canvas reference, five buttons abreast leave ~200 units each, and
/// "Preset 4 · Connect as Premium + Moderator" is not legible in that. The canonical part of a
/// preset is its label text and its test id, not the axis they are arranged on.
/// </summary>
public class OctopusScenarioScreenView : MonoBehaviour
{
    /// <summary>Above <see cref="OctopusScenariosListView"/>'s 1000, which stays open behind it.</summary>
    private const int SortingOrder = 1100;

    private OctopusScenarioPilot _pilot;
    private Text _resultLabel;
    private readonly List<KeyValuePair<string, InputField>> _inputs =
        new List<KeyValuePair<string, InputField>>();

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

    /// <summary>Attaches a pilot and builds the screen. Called once, by <see cref="Open"/>.</summary>
    public void Bind(OctopusScenarioPilot pilot)
    {
        _pilot = pilot;
        Build();
        _pilot.ResultChanged += OnResultChanged;
    }

    private void OnDestroy()
    {
        if (_pilot != null) _pilot.ResultChanged -= OnResultChanged;
    }

    private void OnResultChanged(string result)
    {
        if (_resultLabel != null) _resultLabel.text = result;
    }

    private void Build()
    {
        SampleUi.OverlayCanvas(gameObject, SortingOrder);

        // The scene supplies the EventSystem, exactly as the scenario list documents: the project
        // is Input-System-only (ProjectSettings activeInputHandler: 1), so a hand-rolled
        // EventSystem's legacy StandaloneInputModule would throw every frame.
        var root = SampleUi.Panel("Root", transform, SampleUi.Background);
        SampleUi.Stretch(root, Vector2.zero, Vector2.one);

        BuildHeader(root);

        var content = SampleUi.VerticalScroll(root, new RectOffset(40, 40, 220, 40));
        BuildFields(content);
        BuildPresets(content);
        BuildResult(content);

        // Usable on entry: the first preset's values, filled purely — no SDK call, no mutation.
        ApplyPreset(_pilot.Presets[0], false);
    }

    private void BuildHeader(RectTransform root)
    {
        var header = SampleUi.Panel("Header", root, Color.clear);
        SampleUi.Stretch(header, new Vector2(0f, 1f), Vector2.one);
        header.sizeDelta = new Vector2(0f, 220f);
        header.anchoredPosition = new Vector2(0f, -110f);

        // Left inset 70, right inset 260: clear of the Back button, and on the same left edge as
        // the capability line under it.
        var title = SampleUi.Label("Title", header, _pilot.Title, 46, SampleUi.PlatformSlot,
                                   TextAnchor.UpperLeft);
        SampleUi.Stretch(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f));
        title.rectTransform.sizeDelta = new Vector2(-330f, 60f);
        title.rectTransform.anchoredPosition = new Vector2(-95f, -50f);

        var capability = SampleUi.Label("Capability", header, _pilot.Capability, 26, SampleUi.Muted,
                                        TextAnchor.UpperLeft);
        SampleUi.Stretch(capability.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f));
        capability.rectTransform.sizeDelta = new Vector2(-330f, 70f);
        capability.rectTransform.anchoredPosition = new Vector2(-95f, -125f);

        var back = SampleUi.Button("Back", header, "Back", () => Destroy(gameObject));
        back.anchorMin = back.anchorMax = new Vector2(1f, 1f);
        back.pivot = new Vector2(1f, 1f);
        back.sizeDelta = new Vector2(180f, 80f);
        back.anchoredPosition = new Vector2(-40f, -30f);
    }

    private void BuildFields(RectTransform content)
    {
        var section = Section(content, "Parameters");
        foreach (var field in _pilot.Fields.All)
        {
            SampleUi.FlexibleLabel(section, field.Label, 22, SampleUi.Muted);

            // <scenario>-field-<key>: the catalogue names presets and results, not fields, so this
            // follows §5.4's `<section>-<element>` shape without claiming to be catalogued.
            var input = SampleUi.Field(_pilot.Id + "-field-" + field.Key, section, field.Value);
            input.gameObject.AddComponent<LayoutElement>().preferredHeight = 72f;

            var key = field.Key;
            input.onValueChanged.AddListener(value => _pilot.Fields.Set(key, value));
            _inputs.Add(new KeyValuePair<string, InputField>(key, input));
        }
    }

    private void BuildPresets(RectTransform content)
    {
        var section = Section(content, "Presets");
        foreach (var preset in _pilot.Presets)
        {
            var current = preset;
            // GameObject.name IS the catalogue's test_id, verbatim — this is the handle the QA
            // pipeline taps.
            var button = SampleUi.Button(current.TestId, section, current.Label,
                                         () => ApplyPreset(current, true));
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 88f;
        }
    }

    private void BuildResult(RectTransform content)
    {
        SampleUi.FlexibleLabel(content, "Result", 22, SampleUi.Muted);

        // GameObject.name IS the catalogue's result_test_id, verbatim.
        var panel = SampleUi.Panel(_pilot.ResultTestId, content, SampleUi.RowBackground);
        SampleUi.VerticalStack(panel, 8f, new RectOffset(24, 24, 20, 20), false);
        _resultLabel = SampleUi.FlexibleLabel(panel, _pilot.Result, 24, SampleUi.TitleColor);
    }

    private RectTransform Section(RectTransform content, string title)
    {
        var section = SampleUi.Panel(title + "Section", content, SampleUi.RowBackground);
        SampleUi.VerticalStack(section, 12f, new RectOffset(24, 24, 20, 20), false);
        SampleUi.FlexibleLabel(section, title, 28, SampleUi.TitleColor);
        return section;
    }

    /// <summary>
    /// Fills every field from <paramref name="preset"/> and mirrors the values into the inputs.
    /// Runs the preset's SDK call only when <paramref name="run"/> is true — which is only ever on
    /// a tap.
    /// </summary>
    private void ApplyPreset(OctopusScenarioPreset preset, bool run)
    {
        preset.Fill(_pilot.Fields);
        foreach (var input in _inputs)
        {
            // SetTextWithoutNotify, not `.text`: the listener installed in BuildFields would
            // otherwise write the value straight back into the field it just came from.
            input.Value.SetTextWithoutNotify(_pilot.Fields.Get(input.Key));
        }
        if (run) preset.Run(_pilot.Fields);
    }
}
