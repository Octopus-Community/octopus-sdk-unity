using System;
using System.Collections.Generic;

/// <summary>
/// One named parameter of a scenario, as the scenario screen shows it.
///
/// Presets drive QA (SDK_STANDARDS §5.2). Fields show their values, and an editable pilot can
/// opt into Customize so a human can run values outside those presets.
/// </summary>
public sealed class OctopusScenarioField
{
    /// <summary>Stable key a preset addresses the field by. Also the GameObject name suffix.</summary>
    public readonly string Key;

    /// <summary>Human-readable caption shown above the input.</summary>
    public readonly string Label;

    /// <summary>Informative context only; never offered as a custom action parameter.</summary>
    public readonly bool IsInformational;

    /// <summary>Current value. Written by a preset, and by a human typing into the input.</summary>
    public string Value { get; set; }

    public OctopusScenarioField(string key, string label, bool isInformational = false)
    {
        Key = key;
        Label = label;
        IsInformational = isInformational;
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

    /// <summary>The preset label, including the catalogue's unnumbered clear-override label.</summary>
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
/// preset Run or custom Run only, i.e. only after a tap, and every such call is
/// announced through <see cref="OctopusSampleLog"/> first — which is what lets a test prove the
/// "no mutation on entry" half of SDK_STANDARDS §5.2 instead of asserting it in prose.
/// </summary>
public abstract class OctopusScenarioPilot : IDisposable
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
    public virtual string Capability { get { return _row.Capability; } }

    private static readonly string[] NoApiSymbols = new string[0];

    /// <summary>SDK entry points demonstrated by this pilot.</summary>
    public virtual IReadOnlyList<string> ApiSymbols { get { return NoApiSymbols; } }

    /// <summary>Explains limits of illustrative or read-only parameters.</summary>
    public virtual string ParameterNotice { get { return string.Empty; } }

    public string YouWillSee
    {
        get
        {
            return string.IsNullOrWhiteSpace(_row.YouWillSee)
                ? "the outcome of the selected action in the result area."
                : _row.YouWillSee;
        }
    }

    /// <summary>The catalogue's `result_test_id`, verbatim. The result panel's GameObject name.</summary>
    public string ResultTestId { get { return _row.ResultTestId; } }

    /// <summary>The scenario's parameters, in the order the screen renders them.</summary>
    public abstract OctopusScenarioFields Fields { get; }

    /// <summary>The preset buttons, in catalogue order.</summary>
    public abstract IReadOnlyList<OctopusScenarioPreset> Presets { get; }

    /// <summary>
    /// Whether the pilot consumes custom values. Informational fields remain read-only and
    /// separate from the parameters of that action.
    /// Override this together with RunCustom to opt a pilot into the generic Customize screen.
    /// </summary>
    public virtual bool CanCustomize { get { return false; } }

    /// <summary>Runs the current fields without filling a preset. Only called on a custom Run tap.</summary>
    public virtual void RunCustom()
    {
        throw new InvalidOperationException("This scenario has no custom action.");
    }

    // Managed observation only: no SDK calls or strong references from the session to a view.
    internal virtual void ObservationsChanged() { }

    /// <summary>The live result line, mirrored into the result panel.</summary>
    public string Result { get; private set; }

    public bool IsRunning { get; private set; }

    // One entry point for view-driven presets and custom runs. Async pilots publish progress
    // with ReportRunning and finish with Report; synchronous failures also leave Running.
    public void Execute(Action action)
    {
        if (IsRunning) return;
        ReportRunning(Result);
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Report("Run failed: " + exception.Message);
        }
    }

    protected void ReportRunning(string line)
    {
        IsRunning = true;
        Publish(line);
    }

    private Action<string> _resultChanged;

    /// <summary>Raised whenever <see cref="Result"/> or its running state changes, on the Unity main thread.</summary>
    public event Action<string> ResultChanged
    {
        add { _resultChanged += value; }
        remove
        {
            _resultChanged -= value;
            // The existing renderer unsubscribes on destruction. Future renderers can also
            // dispose explicitly: native observations belong to this visit, not the process.
            if (_resultChanged == null) Dispose();
        }
    }

    public virtual void Dispose() { IsRunning = false; }

    /// <summary>Latest requested host profile destination; null before its preset runs.</summary>
    public OctopusScenarioHostProfile HostProfile { get; protected set; }

    /// <summary>A renderer may handle this to present a host-owned profile page.</summary>
    public event Action<OctopusScenarioHostProfile> HostProfileRequested;

    protected bool RequestHostProfile(OctopusScenarioHostProfile profile)
    {
        HostProfile = profile;
        var handler = HostProfileRequested;
        if (handler == null) return false;
        handler(profile);
        return true;
    }

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
        IsRunning = false;
        Publish(line);
    }

    private void Publish(string line)
    {
        Result = line;
        var handler = _resultChanged;
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
        "theme",
        "refreshEntitlements",
        "termsAcceptance",
        "profileFieldsLock",
        "communityData",
        "groups",
        "syncFollowGroups",
        "groupAccessDenied",
        "communityAccess",
        "contentOptions",
        "reactions",
        "bridge",
        "createPost",
        "events",
        "trackABTests",
        "forceOctopusABTests",
        "lifecycle",
        "notSeenNotifications",
        "pushNotifications",
        "initialScreen",
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
            case "initialScreen": return new InitialScreenScenario();
            case "connection": return new ConnectionScenario();
            case "customEvents": return new CustomEventsScenario();
            case "locale": return new LocaleScenario();
            case "communityData": return new CommunityDataScenario();
            case "profileFieldsLock": return new ProfileFieldsLockScenario();
            case "termsAcceptance": return new TermsAcceptanceScenario();
            case "refreshEntitlements": return new RefreshEntitlementsScenario();
            case "theme": return new ThemeScenario();
            case "groups": return new GroupsScenario();
            case "syncFollowGroups": return new SyncFollowGroupsScenario();
            case "groupAccessDenied": return new GroupAccessDeniedScenario();
            case "communityAccess": return new CommunityAccessScenario();
            case "contentOptions": return new ContentOptionsScenario();
            case "reactions": return new ReactionsScenario();
            case "bridge": return new BridgeScenario();
            case "createPost": return new CreatePostScenario();
            case "events": return new EventsScenario();
            case "trackABTests": return new TrackABTestsScenario();
            case "forceOctopusABTests": return new ForceOctopusABTestsScenario();
            case "lifecycle": return new LifecycleScenario();
            case "notSeenNotifications": return new NotSeenNotificationsScenario();
            case "pushNotifications": return new PushNotificationsScenario();
            default: return null;
        }
    }
}

/// <summary>View-agnostic destination data for the catalogue's host-rendered profile preset.</summary>
public sealed class OctopusScenarioHostProfile
{
    public string ClientUserId { get; private set; }
    public OctopusCommunityData Data { get; private set; }
    public string Error { get; private set; }
    public bool IsLoading { get; private set; }
    public string ResultTestId
    {
        get { return Error != null ? "clientProfile-error" : Data == null ? "clientProfile-unknown" : "clientProfile-data"; }
    }
    public event Action Changed;

    public OctopusScenarioHostProfile(string clientUserId)
    {
        ClientUserId = clientUserId;
        IsLoading = true;
    }

    public void Complete(OctopusCommunityData data, string error = null)
    {
        Data = data;
        Error = error;
        IsLoading = false;
        if (Changed != null) Changed();
    }
}
