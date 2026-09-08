using System.Collections.Generic;

/// <summary>
/// The `customEvents` scenario: `OctopusSDK.Track(name, properties)`.
///
/// The SDK call is the one `EventsExample.TrackCustomEvent` makes. Two things change here, both
/// required by the QA contract:
///
/// - **The payload is deterministic.** `EventsExample` sends a random price and `DateTime.UtcNow`;
///   SDK_STANDARDS §5.4 asks a preset to be re-runnable from any prior state with the same
///   observable outcome, so both presets here send fixed values.
/// - **The no-props preset sends an empty dictionary, not null.** `OctopusSDK.Track` reads
///   `properties.Keys` and `properties.Count` on both native paths, so a null would throw on
///   device while passing in the editor's mock backend.
/// </summary>
public sealed class CustomEventsScenario : OctopusScenarioPilot
{
    private const string NameKey = "eventName";
    private const string PropertiesKey = "properties";

    // The catalogue calls these "sample event" presets; the name is a fixture, not a real event
    // the backend is expected to know.
    private const string SampleEventName = "qa_sample_event";

    // Rendered into the field as "k=v; k=v" and parsed back out of it at tap time, so a human can
    // edit the field and re-run without the pilot holding a second, hidden copy of the payload.
    private const string SampleProperties = "price=59.90; currency=USD; source=unity-sample";

    /// <summary>What the properties field says when a preset deliberately sends none.</summary>
    private const string NoProperties = "(none)";

    private readonly OctopusScenarioFields _fields = new OctopusScenarioFields(
        new OctopusScenarioField(NameKey, "Event name"),
        new OctopusScenarioField(PropertiesKey, "Properties (key=value; …)"));

    private readonly List<OctopusScenarioPreset> _presets;

    public CustomEventsScenario() : base("customEvents")
    {
        _presets = new List<OctopusScenarioPreset>
        {
            Track(1, "Track sample event (no props)", NoProperties),
            Track(2, "Track sample event (with props)", SampleProperties),
        };
    }

    public override OctopusScenarioFields Fields { get { return _fields; } }

    public override IReadOnlyList<OctopusScenarioPreset> Presets { get { return _presets; } }

    private OctopusScenarioPreset Track(int index, string description, string properties)
    {
        return new OctopusScenarioPreset(
            PresetTestId(index),
            PresetLabel(index, description),
            fields =>
            {
                fields.Set(NameKey, SampleEventName);
                fields.Set(PropertiesKey, properties);
            },
            fields => TrackNow(fields.Get(NameKey), fields.Get(PropertiesKey)));
    }

    private void TrackNow(string eventName, string properties)
    {
        string mode;
        var profile = OctopusScenarioSdk.EnsureInitialized(
            OctopusScenarioSdk.PilotMode(), OctopusScenarioSdk.PilotModeLabel, out mode);
        if (profile == null)
        {
            Report(mode);
            return;
        }

        var props = Parse(properties);
        var detail = "name=" + eventName + ", props=" + props.Count;
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.Track", detail);
        OctopusSDK.Track(eventName, props);

        var line = "Tracked '" + eventName + "' with " + props.Count + " propert" +
                   (props.Count == 1 ? "y" : "ies") + " (mode: " + mode + ").";
        foreach (var pair in props)
        {
            line += "\n  " + pair.Key + " = " + pair.Value;
        }
        Report(line);
    }

    /// <summary>
    /// Parses "k=v; k=v" into a dictionary. Never returns null — see the class comment. A pair
    /// without "=" is dropped rather than sent with an empty value, so a half-typed field cannot
    /// quietly ship a nonsense property.
    /// </summary>
    private static IDictionary<string, string> Parse(string properties)
    {
        var parsed = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(properties) || properties.Trim() == NoProperties) return parsed;

        foreach (var pair in properties.Split(';'))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0) continue;
            var key = pair.Substring(0, separator).Trim();
            var value = pair.Substring(separator + 1).Trim();
            if (key.Length == 0) continue;
            parsed[key] = value;
        }
        return parsed;
    }
}
