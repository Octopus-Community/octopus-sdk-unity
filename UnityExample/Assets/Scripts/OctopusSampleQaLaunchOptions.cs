using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>Pure parsing of the sample's Android launch extras; never reads configuration.</summary>
public sealed class OctopusSampleQaLaunchOptions
{
    public string Tab { get; private set; }
    public string Scenario { get; private set; }

    /// <summary>
    /// A sample-owned screen from <see cref="OctopusSampleQaDestinations"/>, when `qaScenario` named
    /// one instead of a catalogue scenario. The two are mutually exclusive and share the extra, so
    /// QA has one flag to learn rather than two.
    /// </summary>
    public string Destination { get; private set; }
    public int? Preset { get; private set; }
    public bool AutoStart { get; private set; }
    public string Error { get; private set; }

    public string Summary
    {
        get
        {
            return "tab=" + (Tab ?? "none") + " scenario=" + (Scenario ?? Destination ?? "none") +
                " preset=" + (Preset.HasValue ? Preset.Value.ToString(CultureInfo.InvariantCulture) : "none") +
                " autoStart=" + (AutoStart ? "true" : "false");
        }
    }

    public static OctopusSampleQaLaunchOptions Parse(IReadOnlyDictionary<string, string> extras)
    {
        var options = new OctopusSampleQaLaunchOptions();
        if (extras == null) return options;
        string value;
        if (extras.TryGetValue("qaTab", out value))
        {
            if (value != "home" && value != "scenarios" && value != "community" && value != "settings")
                return options.Fail("unknown tab");
            options.Tab = value;
        }
        if (extras.TryGetValue("qaScenario", out value))
        {
            foreach (var scenario in OctopusScenarioCatalog.All)
                if (scenario.Id == value) options.Scenario = value;
            if (options.Scenario == null && OctopusSampleQaDestinations.Has(value))
                options.Destination = value;
            if (options.Scenario == null && options.Destination == null)
                return options.Fail("unknown scenario");
        }
        if (extras.TryGetValue("qaPreset", out value))
        {
            int preset;
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out preset) || preset < 1)
                return options.Fail("unknown preset");
            // A destination is a screen, not a scenario: it has no presets to number.
            if (options.Scenario == null) return options.Fail("preset requires scenario");
            options.Preset = preset;
        }
        if (extras.TryGetValue("qaAutoStart", out value))
        {
            bool autoStart;
            if (!bool.TryParse(value, out autoStart)) return options.Fail("invalid autoStart");
            options.AutoStart = autoStart;
        }
        return options;
    }

    private OctopusSampleQaLaunchOptions Fail(string error)
    {
        Error = error;
        return this;
    }
}
