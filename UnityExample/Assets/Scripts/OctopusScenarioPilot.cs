using System;
using System.Collections.Generic;

/// <summary>
/// One named parameter of a scenario, as the scenario screen shows it.
///
/// Fields are not how a scenario is driven — presets are (SDK_STANDARDS §5.2). A field exists so
/// a human reader can see what a preset put in, and so nothing on the screen is ever blank.
/// </summary>
public sealed class OctopusScenarioField
{
    /// <summary>Stable key a preset addresses the field by. Also the GameObject name suffix.</summary>
    public readonly string Key;

    /// <summary>Human-readable caption shown above the input.</summary>
    public readonly string Label;

    /// <summary>Current value. Written by a preset, and by a human typing into the input.</summary>
    public string Value { get; set; }

    public OctopusScenarioField(string key, string label)
    {
        Key = key;
        Label = label;
        Value = string.Empty;
    }
}

/// <summary>
/// A scenario's whole parameter set, addressed by key.
///
/// <see cref="Set"/> throws on an unknown key on purpose: a preset that misspells a field would
/// otherwise silently leave that field on its previous value, which is exactly the "one field
/// left for the Tester to type into" §5.2 forbids.
/// </summary>
public sealed class OctopusScenarioFields
{
    private readonly List<OctopusScenarioField> _fields;

    public OctopusScenarioFields(params OctopusScenarioField[] fields)
    {
        _fields = new List<OctopusScenarioField>(fields);
    }

    /// <summary>The fields, in declaration order — the order the screen renders them in.</summary>
    public IReadOnlyList<OctopusScenarioField> All { get { return _fields; } }

    /// <summary>Writes a field's value.</summary>
    /// <exception cref="ArgumentException">No field carries that key.</exception>
    public void Set(string key, string value)
    {
        var field = Find(key);
        if (field == null)
        {
            throw new ArgumentException("No scenario field with key '" + key + "'.", "key");
        }
        field.Value = value;
    }

    /// <summary>Reads a field's value.</summary>
    /// <exception cref="ArgumentException">No field carries that key.</exception>
    public string Get(string key)
    {
        var field = Find(key);
        if (field == null)
        {
            throw new ArgumentException("No scenario field with key '" + key + "'.", "key");
        }
        return field.Value;
    }

    private OctopusScenarioField Find(string key)
    {
        foreach (var field in _fields)
        {
            if (field.Key == key) return field;
        }
        return null;
    }
}

/// <summary>
/// One preset button on a scenario screen.
///
/// The two halves are separate on purpose. <see cref="Fill"/> is pure — it writes field values and
/// touches nothing else — so a test can assert "this preset leaves no field blank" without the SDK
/// being involved at all. <see cref="Run"/> is the half that talks to the SDK, and it only ever
/// happens on a tap: nothing on a scenario screen reaches the SDK without a user action.
/// </summary>
public sealed class OctopusScenarioPreset
{
    private readonly Action<OctopusScenarioFields> _fill;
    private readonly Action<OctopusScenarioFields> _run;

    /// <summary>The catalogue's `presets[].test_id`, verbatim. Also the button's GameObject name.</summary>
    public readonly string TestId;

    /// <summary>The catalogue's preset label, verbatim. Always starts with "Preset N · ".</summary>
    public readonly string Label;

    public OctopusScenarioPreset(string testId, string label,
                                 Action<OctopusScenarioFields> fill,
                                 Action<OctopusScenarioFields> run)
    {
        TestId = testId;
        Label = label;
        _fill = fill;
        _run = run;
    }

    /// <summary>Pre-fills every field this preset drives. Makes no SDK call.</summary>
    public void Fill(OctopusScenarioFields fields)
    {
        _fill(fields);
    }

    /// <summary>Performs the preset's SDK call, from the current field values.</summary>
    public void Run(OctopusScenarioFields fields)
    {
        _run(fields);
    }
}

/// <summary>
/// A scenario the Unity sample actually drives, rendered by <see cref="OctopusScenarioScreenView"/>.
///
/// Ids are never spelled here: <see cref="ResultTestId"/> and <see cref="PresetTestId"/> read the
/// matching row of <see cref="OctopusScenarioCatalog"/>, which is itself the verbatim mirror of
/// `shared/config/scenarios-catalog.yaml` in pm-tools. One transcription, one place to drift, one
/// test guarding it — rather than the same string typed into a pilot, a screen and a test.
///
/// Nothing in a pilot's construction reaches the SDK. The SDK is touched from
/// <see cref="OctopusScenarioPreset.Run"/> only, i.e. only after a tap, and every such call is
/// announced through <see cref="OctopusSampleLog"/> first — which is what lets a test prove the
/// "no mutation on entry" half of SDK_STANDARDS §5.2 instead of asserting it in prose.
/// </summary>
public abstract class OctopusScenarioPilot
{
    private readonly OctopusScenario _row;

    protected OctopusScenarioPilot(string scenarioId)
    {
        foreach (var row in OctopusScenarioCatalog.All)
        {
            if (row.Id != scenarioId) continue;
            _row = row;
            break;
        }
        if (_row == null)
        {
            throw new ArgumentException(
                "No scenario '" + scenarioId + "' in OctopusScenarioCatalog.", "scenarioId");
        }
        Result = "Ready — no SDK call made yet. Tap a preset to run one.";
    }

    /// <summary>The catalogue id, e.g. "connection".</summary>
    public string Id { get { return _row.Id; } }

    /// <summary>The catalogue title, e.g. "Connection".</summary>
    public string Title { get { return _row.Title; } }

    /// <summary>The catalogue's (abridged) capability line.</summary>
    public string Capability { get { return _row.Capability; } }

    /// <summary>The catalogue's `result_test_id`, verbatim. The result panel's GameObject name.</summary>
    public string ResultTestId { get { return _row.ResultTestId; } }

    /// <summary>The scenario's parameters, in the order the screen renders them.</summary>
    public abstract OctopusScenarioFields Fields { get; }

    /// <summary>The preset buttons, in catalogue order.</summary>
    public abstract IReadOnlyList<OctopusScenarioPreset> Presets { get; }

    /// <summary>The live result line, mirrored into the result panel.</summary>
    public string Result { get; private set; }

    /// <summary>Raised whenever <see cref="Result"/> changes, on the Unity main thread.</summary>
    public event Action<string> ResultChanged;

    /// <summary>The catalogue's nth preset test id, 1-based, exactly as pm-tools spells it.</summary>
    protected string PresetTestId(int oneBasedIndex)
    {
        var ids = _row.PresetTestIds;
        if (ids == null || oneBasedIndex < 1 || oneBasedIndex > ids.Length)
        {
            throw new ArgumentOutOfRangeException(
                "oneBasedIndex",
                "Scenario '" + _row.Id + "' has no preset #" + oneBasedIndex + " in the catalogue.");
        }
        return ids[oneBasedIndex - 1];
    }

    /// <summary>
    /// The catalogue's preset label shape: "Preset N · &lt;description&gt;". The separator is
    /// U+00B7 MIDDLE DOT, as spelled in `scenarios-catalog.yaml` — the QA Tester still matches on
    /// visible text today, so the character is part of the contract, not decoration.
    /// </summary>
    protected static string PresetLabel(int oneBasedIndex, string description)
    {
        return "Preset " + oneBasedIndex + " · " + description;
    }

    /// <summary>Publishes a new result line.</summary>
    protected void Report(string line)
    {
        Result = line;
        var handler = ResultChanged;
        if (handler != null) handler(line);
    }
}

/// <summary>
/// The scenarios this sample drives, by catalogue id.
///
/// Deliberately not derived from <see cref="OctopusScenarioCatalog"/>: the catalogue is the list of
/// scenarios that EXIST, this is the list Unity has built. Keeping them separate is what makes the
/// gap countable — the scenario list renders both numbers side by side.
/// </summary>
public static class OctopusScenarioPilots
{
    /// <summary>The catalogue ids this sample has a scenario screen for.</summary>
    public static readonly IReadOnlyList<string> Ids = new List<string>
    {
        "connection",
        "customEvents",
        "locale",
    };

    /// <summary>Whether <paramref name="scenarioId"/> has a scenario screen in this sample.</summary>
    public static bool Has(string scenarioId)
    {
        foreach (var id in Ids)
        {
            if (id == scenarioId) return true;
        }
        return false;
    }

    /// <summary>
    /// A fresh pilot for <paramref name="scenarioId"/>, or null when this sample has no screen for
    /// it. Fresh on purpose: a pilot holds the live field values and result of one visit.
    /// </summary>
    public static OctopusScenarioPilot Create(string scenarioId)
    {
        switch (scenarioId)
        {
            case "connection": return new ConnectionScenario();
            case "customEvents": return new CustomEventsScenario();
            case "locale": return new LocaleScenario();
            default: return null;
        }
    }
}
