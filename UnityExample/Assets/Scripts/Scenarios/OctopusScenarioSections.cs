using System.Collections.Generic;

/// <summary>
/// The six sections the Scenarios tab groups its cards into, in display order.
///
/// Titles and membership are the Android sample's, verbatim (`ScenarioCatalog.kt`), because
/// SDK_STANDARDS §5.1 asks every platform for the same sectioned list and Android is this port's
/// reference. One title is not unanimous across the family: Flutter and React Native both head the
/// first section "SSO &amp; user" where Android reads "Sign-in &amp; user". Unity follows Android —
/// a section head is read by a PO as much as by a tester, and "SSO" is the SDK's vocabulary, not
/// the product's.
/// </summary>
public enum ScenarioSection
{
    SignIn,
    Presentation,
    Community,
    Notifications,
    Theme,
    Host,
}

/// <summary>One section and the scenarios that survived the current search, in catalogue order.</summary>
public class ScenarioSectionGroup
{
    public readonly ScenarioSection Section;
    public readonly IReadOnlyList<OctopusScenario> Scenarios;

    public ScenarioSectionGroup(ScenarioSection section, IReadOnlyList<OctopusScenario> scenarios)
    {
        Section = section;
        Scenarios = scenarios;
    }

    /// <summary>The section head's title, e.g. "Sign-in &amp; user".</summary>
    public string Title { get { return OctopusScenarioSections.TitleOf(Section); } }

    /// <summary>The head's <c>GameObject.name</c>, e.g. `scenarios-section-sign-in`.</summary>
    public string HeaderId { get { return OctopusScenarioSections.HeaderIdOf(Section); } }
}

/// <summary>
/// Turns <see cref="OctopusScenarioCatalog"/> into the sectioned, searchable list SDK_STANDARDS
/// §5.1 describes — as data, so the whole grammar of the screen is testable without naming a single
/// `UnityEngine.UI` type (the EditMode assembly overrides its references down to nunit).
///
/// **A scenario without a screen is not listed.** That is TOKENS §1, which struck the
/// Built/Partial/Pending badge off every sample on 2026-08-26 and stated where the information goes
/// instead: *"A scenario that is not implemented at all is not listed. Filtering it out is the whole
/// point: the list is what the sample can demonstrate right now"*, with the porting status living in
/// the versioned catalogue. Android filters on `status != PENDING`; Flutter and React Native never
/// put an unimplemented scenario in their catalogue at all. Unity keeps all 27 entries — dropping a
/// row from a hand-kept copy of a catalogue that lives in another repo makes it indistinguishable
/// from a scenario nobody ever considered — and filters at the door instead.
/// The four permanently inapplicable scenarios are filtered by the same rule, since a Unity screen
/// for them will never exist.
///
/// The listed cards follow <see cref="OctopusScenarioPilots.Ids"/>, one per driven scenario.
/// Remaining platform coverage is tracked where TOKENS §1 puts it — in
/// `pm-tools/shared/config/scenarios-catalog.yaml`, whose `pending.unity` entries the QA analyzer
/// and `sample-coverage-guard` read.
/// </summary>
public static class OctopusScenarioSections
{
    /// <summary>Display order, which is the enum's own order.</summary>
    public static readonly IReadOnlyList<ScenarioSection> All = new[]
    {
        ScenarioSection.SignIn,
        ScenarioSection.Presentation,
        ScenarioSection.Community,
        ScenarioSection.Notifications,
        ScenarioSection.Theme,
        ScenarioSection.Host,
    };

    /// <summary>
    /// Which scenario ids sit under which head. Android's mapping, verbatim, plus `embeddedBack`,
    /// which Android's list omits entirely and React Native files under Presentation modes — the
    /// same place its fellow presentation scenarios sit here.
    /// </summary>
    private static readonly Dictionary<ScenarioSection, string[]> Members =
        new Dictionary<ScenarioSection, string[]>
        {
            {
                ScenarioSection.SignIn,
                new[] { "connection", "refreshEntitlements", "communityData", "profileFieldsLock", "termsAcceptance" }
            },
            {
                ScenarioSection.Presentation,
                new[] { "initialScreen", "fullscreen", "modal", "sheet", "embeddedBack" }
            },
            {
                ScenarioSection.Community,
                new[] { "groups", "syncFollowGroups", "communityAccess", "groupAccessDenied", "contentOptions", "reactions", "bridge", "createPost" }
            },
            {
                ScenarioSection.Notifications,
                new[] { "notSeenNotifications", "pushNotifications" }
            },
            {
                ScenarioSection.Theme,
                new[] { "theme", "locale" }
            },
            {
                ScenarioSection.Host,
                new[] { "customEvents", "events", "trackABTests", "forceOctopusABTests", "lifecycle" }
            },
        };

    /// <summary>The section head's title, as the tester and the PO read it.</summary>
    public static string TitleOf(ScenarioSection section)
    {
        switch (section)
        {
            case ScenarioSection.SignIn: return "Sign-in & user";
            case ScenarioSection.Presentation: return "Presentation modes";
            case ScenarioSection.Community: return "Community & groups";
            case ScenarioSection.Notifications: return "Notifications";
            case ScenarioSection.Theme: return "Theme & language";
            default: return "Host callbacks & events";
        }
    }

    /// <summary>
    /// The head's addressable name. The shared catalogue's `shell:` block names no section, so this
    /// spelling is ours — built the way the catalogue builds every other id
    /// (`&lt;section&gt;-&lt;element&gt;-&lt;action&gt;`) and the way Android spells its section
    /// controls (`scenarios-toggle-force-login`), so one QA script reads the same on both.
    /// </summary>
    public static string HeaderIdOf(ScenarioSection section)
    {
        switch (section)
        {
            case ScenarioSection.SignIn: return "scenarios-section-sign-in";
            case ScenarioSection.Presentation: return "scenarios-section-presentation";
            case ScenarioSection.Community: return "scenarios-section-community";
            case ScenarioSection.Notifications: return "scenarios-section-notifications";
            case ScenarioSection.Theme: return "scenarios-section-theme";
            default: return "scenarios-section-host";
        }
    }

    /// <summary>The section a scenario belongs to. Every catalogue id has exactly one.</summary>
    public static ScenarioSection SectionOf(string scenarioId)
    {
        foreach (var entry in Members)
        {
            foreach (var id in entry.Value)
            {
                if (id == scenarioId) return entry.Key;
            }
        }

        // Unreachable while EveryCatalogScenarioSitsInExactlyOneSection passes, which is the point
        // of that test: a scenario added to the catalogue and forgotten here fails a gate rather
        // than landing in whichever section happens to be first.
        throw new KeyNotFoundException(
            "Scenario '" + scenarioId + "' belongs to no section. Add it to " +
            "OctopusScenarioSections.Members, in the same section its Android counterpart uses.");
    }

    /// <summary>
    /// Whether this sample can actually demonstrate <paramref name="scenario"/> — i.e. whether a
    /// pilot drives it. The one condition for appearing in the list at all.
    /// </summary>
    public static bool IsListed(OctopusScenario scenario)
    {
        return OctopusScenarioPilots.Has(scenario.Id);
    }

    /// <summary>
    /// Whether <paramref name="scenario"/> survives <paramref name="query"/>: a blank query keeps
    /// everything, and otherwise the title, the capability line and the id are matched
    /// case-insensitively — the same three fields Android, Flutter and React Native match, minus
    /// the subtitle this sample folds into its capability line. The section title is deliberately
    /// not matched: a query that hit a head would return cards that do not contain it.
    /// </summary>
    public static bool Matches(OctopusScenario scenario, string query)
    {
        if (string.IsNullOrEmpty(query) || query.Trim().Length == 0) return true;
        var needle = query.Trim().ToLowerInvariant();
        return scenario.Title.ToLowerInvariant().Contains(needle)
               || scenario.Capability.ToLowerInvariant().Contains(needle)
               || scenario.Id.ToLowerInvariant().Contains(needle);
    }

    /// <summary>
    /// The sections to render for <paramref name="query"/>, in display order, each holding its
    /// surviving scenarios in catalogue order. Notifications also exposes its working registration
    /// toggle while its scenario pilots are pending; no unimplemented card is listed.
    /// </summary>
    public static IReadOnlyList<ScenarioSectionGroup> Filter(string query)
    {
        var groups = new List<ScenarioSectionGroup>();
        foreach (var section in All)
        {
            var scenarios = new List<OctopusScenario>();
            foreach (var scenario in OctopusScenarioCatalog.All)
            {
                if (SectionOf(scenario.Id) != section) continue;
                if (!IsListed(scenario)) continue;
                if (!Matches(scenario, query)) continue;
                scenarios.Add(scenario);
            }

            var pushToggleMatches = section == ScenarioSection.Notifications &&
                (string.IsNullOrWhiteSpace(query) ||
                 OctopusSampleFeatureToggles.PushRegistrationLabel.IndexOf(query.Trim(),
                     System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 TitleOf(section).IndexOf(query.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0);
            if (scenarios.Count > 0 || pushToggleMatches)
                groups.Add(new ScenarioSectionGroup(section, scenarios));
        }

        return groups;
    }
}
